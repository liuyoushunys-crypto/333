# reader.py
"""
模块：reader.py
职能：将 Scheme 纯文本代码进行词法化、S-Expression 语法树构建，以及解析 #{...} 内联中缀表达式。
"""

from fractions import Fraction
import re
from mtypes import (
    TRUE, FALSE, NIL, Sym, Cell, SchemeVector,
    SchemeString,
    SYM_QUOTE, SYM_QQ, SYM_UNQUOTE, SYM_UNSPLICE,
    SYM_SYNTAX, SYM_QS, SYM_USYNTAX, SYM_USPLICES,
    _lst
)

# 多行三引号块边界定义
_TRIPLE_DQ = '"""'
_TRIPLE_SQ = "'''"

# ═══════════════════════════════════════════════════════════════
# 1. 超高精确性通用分词正则设计 (Tokenization Regular Expression)
# ═══════════════════════════════════════════════════════════════
# 该正则一次性匹配所有 Scheme token 类型：
#   - 空白自动跳过（\s* 前缀）
#   - 注释（分号行注释/多行块注释）被匹配但不输出
#   - 字符串、字符、数值、符号、括号、宏缩写前缀各归各类
# 递归下降解析器（Recursive Descent Parser）基于该 token 流工作：
#   parse_reader → parse_list_reader（列表）/ _atom（标量）
#   parse_list_reader 递归处理 car/cdr 直到遇到 ')' 或 '.' 点列表
#   #{...} 转交独立的 Pratt Parser 处理中缀表达式
#   宏缩写（' ` , @ #' #` #, #,@）原地展开为 (quote x) 等 S-表达式
_TOKEN_RE = re.compile(r"""
    \s*
    ( ;[^\n]*                                # 匹配单行单分号注释
    | \#\|[\s\S]*?\|\#                       # 匹配非嵌套多行块注释 #| ... |#
    | \#;                                    # 匹配 Datum Comment #;
    | """ + _TRIPLE_DQ + r"""[\s\S]*?""" + _TRIPLE_DQ + r""" 
    | """ + _TRIPLE_SQ + r"""[\s\S]*?""" + _TRIPLE_SQ + r""" 
    | "(?:[^"\\]|\\.)*"                      # 匹配标准 Scheme 转义双引号字符串
    | \#\\(?:[a-zA-Z]+|.)                    # 匹配 Scheme 字符字面量 #\space or #\a
    | \#\(                                   # 匹配 Vector 向量符号前缀
    | \#\{[^}]*\}                            # 匹配 #{...} 中缀表达式容器
    | [\(\)]                                 # 匹配左右标准括号
    | \#\'|\#\`|\#,@|\#,|\'|`|,@|,           # 匹配各种宏缩写糖前缀如 #', #`, #, 等
    | \.\.\.                                 # 匹配语法规则省略号 ...
    | \#t|\#f                                # 匹配布尔真假
    | [-+]?(?:0x[0-9a-fA-F]+|0o[0-7]+|0b[01]+ # 匹配带有进制前缀的数、非十进制数
             |[0-9]+/[0-9]+                  # 匹配有理数如 1/3
             |[0-9]+(?:\.[0-9]*)?(?:[eE][-+]?[0-9]+)? # 匹配带有指数标记浮点数
             |\.[0-9]+(?:[eE][-+]?[0-9]+)?
             )(?:i|[-+]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)i)? # 匹配复数虚部
             (?![a-zA-Z0-9!$%&*+\-./:<=>?@^_~])
    | \.                                     # 点列表语法点号 .
    | [^\s\(\)"',;`#]+                       # 匹配常规符号或不包含特殊分界符的标识符
    )
""", re.VERBOSE)

def _tokenize(s):
    """过滤无用空白与单行注释，输出分词列表"""
    res = []
    for m in _TOKEN_RE.finditer(s):
        g = m.group(1)
        if g and g[0] != ';': # 拦截注释：分号开头的不输出，块注释和字符串保留
            res.append(g)
    return res

class Reader:
    """词法状态机容器（无状态指针向后移动）"""
    # toks: 词法单元列表，pos: 当前读取位置，length: tokens 总数
    __slots__ = ('toks', 'pos', 'length')
    def __init__(self, toks):
        self.toks = toks
        self.pos = 0
        self.length = len(toks)

    def peek(self):
        """查看下一个 token 但不消费（超前看）"""
        if self.pos < self.length:
            return self.toks[self.pos]
        return None # EOF 返回 None

    def next(self):
        """消费并返回当前 token，到达末尾时抛 EOFError 异常"""
        if self.pos < self.length:
            t = self.toks[self.pos]
            self.pos += 1
            return t
        raise EOFError("unexpected EOF")

def read(s):
    """单表解析入口：从字符串 tokenize 后递归解析一个 S-表达式"""
    toks = _tokenize(s)
    if not toks: return None
    return parse_reader(Reader(toks))

# ═══════════════════════════════════════════════════════════════
# 2. 精确数据类型还原 (Scheme Type Unboxing)
# ═══════════════════════════════════════════════════════════════

def parse_number_scheme(s):
    """遵循 R7RS 规范的数解析器（支持有理数 Fraction、进制、复数 Complex）
       处理 #x(hex) #o(octal) #b(binary) #d(decimal) 前缀，
       内置有理数 a/b、浮点数、复数（含 i 虚部写法）"""
    s=s.strip()
    s_lower = s.lower()
    if s_lower in ('+inf.0', 'inf.0', '+inf'): return float('inf')
    if s_lower in ('-inf.0', '-inf'): return float('-inf')
    if s_lower in ('+nan.0', 'nan.0', 'nan', '-nan.0'): return float('nan')
    rad=10
    if s.startswith('#'):
        if len(s)>1:
            # 进制前缀识别：#x=16进制, #o=8进制, #b=2进制, #d=10进制
            if s[1]=='x': rad=16; s=s[2:]
            elif s[1]=='o': rad=8; s=s[2:]
            elif s[1]=='b': rad=2; s=s[2:]
            elif s[1]=='d': rad=10; s=s[2:]
    try:
        if rad!=10: return int(s,rad)
        # 有理数：a/b 形式，排除前缀符号 '+-' 和复数 'i'，+/i仅含纯数字与斜杠
        if '/' in s and '+' not in s and '-' not in s[1:] and 'i' not in s:
            parts=s.split('/')
            if len(parts)==2:
                return Fraction(int(parts[0]),int(parts[1]))
        # 复数：含 'i' 且至少一个数字，替换 i→j 后由 Python complex() 解析
        # 纯虚数如 3i、+5i；笛卡尔形式如 1+2i (用正则捕获 +号)
        if 'i' in s and any(c.isdigit() for c in s):
            cs=s.replace('i','j')
            if cs.endswith('j'):
                r=complex(cs)
                return int(r.real) if r.imag==0 and r.real==int(r.real) else (r.real if r.imag==0 else r)
        return int(s,rad)
    except:
        try: return float(s)
        except: return FALSE
        
def _atom(tok):
    """解析基础标量值：字符、字符串、数值或通用符号类型
       返回值类型：TRUE/FALSE(Sym)、char元组(str)、str、int/float/complex/Fraction、Sym"""
    if tok=='#t': return TRUE
    if tok=='#f': return FALSE
    # 字符字面量：##\a, #\space 等命名字符
    if tok.startswith('#'):
        # 字符串形式的字符（带引号）：#"a"（# 后紧跟引号）
        if len(tok)>1 and tok[1]=='"': return ('char',tok[2:-1])
        # 命名字符对照表：space/newline/tab/return/null/nul/alarm/backspace/escape/delete
        m={'space':' ','newline':'\n','tab':'\t','return':'\r','null':'\0','nul':'\0','alarm':'\a','backspace':'\b','escape':'\x1b','delete':'\x7f'}
        ch=tok[2:]; return ('char',m.get(ch,ch[0] if len(ch)==1 else ch))
    # 双引号字符串解析，处理反斜杠转义序列
    if tok.startswith('"'):
        # 转义序列翻译
        s=tok[1:-1]; r=[]; i=0
        # 标准转义映射表：\t→TAB, \n→换行, \r→回车, \\→反斜杠, \"→引号, \0→NUL, \a→警报, \b→退格, \f→换页, \v→垂直制表
        esc={'t':'\t','n':'\n','r':'\r','\\':'\\','"':'"','0':'\0','a':'\a','b':'\b','f':'\f','v':'\v'}
        while i<len(s):
            if s[i]=='\\' and i+1<len(s):
                ch=s[i+1]
                if ch in esc: r.append(esc[ch]); i+=2
                # \xHH; — 十六进制 Unicode 转义序列，如 \x3b; → ';'
                elif ch=='x':
                    h=''; i+=2
                    while i<len(s) and s[i] in '0123456789abcdefABCDEF': h+=s[i]; i+=1
                    if i<len(s) and s[i]==';': i+=1 # 可选分号终止符
                    r.append(chr(int(h,16)))
                # 非法转义：直接输出反斜杠后的字符（Scheme 宽松行为）
                else: r.append(ch); i+=2
            else: r.append(s[i]); i+=1
        return SchemeString(''.join(r))
    # 数值解析尝试：int/float/complex/Fraction；失败则作为符号
    # 如果 token 以字母开头，一定是符号，跳过数值解析
    if tok and tok[0].isalpha():
        if len(tok) > 1 and tok[0] in 'bBoOxXdD':
            prefix = '#' + tok[0].lower() + tok[1:]
            pn = parse_number_scheme(prefix)
            if isinstance(pn, int): return pn
        return Sym(tok)
    pn=parse_number_scheme(tok)
    if isinstance(pn,(int,float,complex)): return pn
    if pn is not FALSE and pn.__class__.__name__=='Fraction': return pn
    return Sym(tok)

def parse_reader(reader):
    """核心自顶向下递归下降（Recursive Descent）解析器入口
       根据第一个 token 分派到不同解析路径：
       - 块注释 #| 跳过直至 |#
       - 三引号字符串（连续三个双引号 / 连续三个单引号）→ _parse_triple 多行字符串
       - ( → parse_list_reader 列表/点列表
       - #( → 向量字面量 #(1 2 3)，收集列表并构造 SchemeVector
       - #{...} → parse_infix 中缀表达式（内联算术扩展）
       - 'x → (quote x) 宏缩写展开
       - `x → (quasiquote x)
       - ,x → (unquote x)
       - ,@x → (unquote-splicing x)
       - #'x → (syntax x)
       - #`x → (quasisyntax x)
       - #,x → (unsyntax x)
       - #,@x → (unsyntax-splicing x)
       - 否则 → _atom 标量（数字/字符/字符串/符号）"""
    t = reader.next()
    # 跳过块注释 #| ... |# （顶层和多行需循环处理）
    while t.startswith('#|'):
        t = reader.next()
    # 跳过 datum comment #; 及其后一个表达式
    if t == '#;':
        parse_reader(reader)  # skip next expression
        return parse_reader(reader)  # parse what follows
    # 三引号字符串处理
    if t.startswith('"""') or t.startswith("'''"):
        return _parse_triple(t)
    if t == '(':
        return parse_list_reader(reader)
    if t == "#(":
        items = parse_list_reader(reader)
        vec = SchemeVector([])
        cur = items
        while cur is not NIL:
            if isinstance(cur, Cell):
                vec.data.append(cur.car)
                cur = cur.cdr
            else:
                break
        return vec
    if t.startswith('#{') and t.endswith('}'):
        # 优化：提取 #{...} 内容直接交给中缀解析通道
        inner = t[2:-1].strip()
        return parse_infix(inner)
    
    # 宏展开糖解析，将其转换为等价的二元组嵌套：'x -> (quote x)
    if t == "'": return Cell(SYM_QUOTE, Cell(parse_reader(reader), NIL))
    if t == '`': return Cell(SYM_QQ, Cell(parse_reader(reader), NIL))
    if t == ',': return Cell(SYM_UNQUOTE, Cell(parse_reader(reader), NIL))
    if t == ',@': return Cell(SYM_UNSPLICE, Cell(parse_reader(reader), NIL))
    if t == "#'": return Cell(SYM_SYNTAX, Cell(parse_reader(reader), NIL))
    if t == '#`': return Cell(SYM_QS, Cell(parse_reader(reader), NIL))
    if t == '#,': return Cell(SYM_USYNTAX, Cell(parse_reader(reader), NIL))
    if t == '#,@': return Cell(SYM_USPLICES, Cell(parse_reader(reader), NIL))
    return _atom(t)

def _parse_triple(t):
    """去除外层三引号，将内容转义处理后返回"""
    content = t[3:-3]
    return _triple_to_value(content)

def _triple_to_value(content):
    """处理三引号字符串内部的转义序列（\n \t \\ 等），非常规字符串使用相同转义逻辑"""
    result = []
    i, n = 0, len(content)
    while i < n:
        c = content[i]
        if c == '\\' and i + 1 < n:
            nxt = content[i + 1]
            esc = {'n': '\n', 'r': '\r', 't': '\t', '\\': '\\', '"': '"', "'": "'"}
            result.append(esc.get(nxt, nxt))
            i += 2
        else:
            result.append(c)
            i += 1
    return ''.join(result)

def parse_list_reader(reader):
    """解析规范列表与非规范（点号）列表
       递归过程：
       1. 若遇到 ')' → 空列表 NIL
       2. 若遇到 '.' → 点列表语法 (a . b)，点后单一元素后必须紧跟 ')'
       3. 否则递归解析 car，再检查 cdr：
          - 若 cdr 以 '.' 开头 → (car . cdr) 点对，需确保后面有 ')'
          - 否则递归 parse_list_reader 继续解析剩余元素
       边缘情况：
       - 空列表 () → NIL
       - 不完整点列表 (a . b c) → SyntaxError
       - 缺少结束括号 → EOFError"""
    t = reader.peek()
    if t is None: raise EOFError("unterminated list")
    if t == ')':
        reader.next()
        return NIL
    # 点号在首位：( . b) → 点列表语法
    if t == '.':
        reader.next()
        ce = parse_reader(reader)
        nxt = reader.next()
        if nxt == ')': return ce
        raise SyntaxError("malformed dotted list")
    
    h = parse_reader(reader)
    nxt = reader.peek()
    # 点号在第二个位置：(a . b) → Cell(a, b)
    if nxt == '.':
        reader.next()
        de = parse_reader(reader)
        nxt2 = reader.next()
        if nxt2 == ')': return Cell(h, de)
        raise SyntaxError("malformed dotted list")
        
    # 普通列表：递归处理尾部 (a b c) → Cell(a, Cell(b, Cell(c, NIL)))
    d = parse_list_reader(reader)
    return Cell(h, d)

def read_all(s):
    """多表扫描输入：读取包含多个 S-表达式的输入流
       容错处理：捕获 EOFError 表示正常结束；其他异常跳过当前 token 继续"""
    toks = _tokenize(s)
    if not toks: return []
    reader = Reader(toks)
    r = []
    while reader.pos < reader.length:
        try:
            e = parse_reader(reader)
            if e is not NIL and e is not None:
                r.append(e)
        except EOFError:
            break
        except Exception:
            reader.pos += 1
    return r

# ═══════════════════════════════════════════════════════════════
# 3. 中缀算符优先级解析 (Pratt Parser / Infix Engine)
# ═══════════════════════════════════════════════════════════════
# 该扩展将 #{a + b * c} 解析为 Scheme 前缀表达式 (+ a (* b c))
# 使用 Vaughan Pratt 的"自顶向下算符优先级解析"算法：
#   _primary() 处理操作数（数字、符号、子表达式）
#   _expr(min_prec) 循环处理算符，当后续算符优先级 < min_prec 时停止
#   自赋值运算符 (+= -= *= /=) 展开为 (set! x (+ x 1))
#   等号 = 展开为 (set! x y)
#   幂运算 ^/** 映射为 (expt a b)
#   不等号 != 映射为 (not= a b)

_infix_ops = {
    # 优先级映射表：(优先级, 结合性)
    # 优先级 0-4，数值越大绑定越紧
    '=': (1, 'left'), '!=': (1, 'left'),
    '<': (1, 'left'), '>': (1, 'left'),
    '<=': (1, 'left'), '>=': (1, 'left'),
    '+': (2, 'left'), '-': (2, 'left'),
    '*': (3, 'left'), '/': (3, 'left'),
    '//': (3, 'left'), '%': (3, 'left'),
    '^': (4, 'right'), '**': (4, 'right'),
    # 自赋值运算符：优先级 0，右结合
    '+=': (0, 'right'), '-=': (0, 'right'),
    '*=': (0, 'right'), '/=': (0, 'right'),
}

def _infix_prec(name):
    info = _infix_ops.get(name)
    return info[0] if info else None

def _infix_assoc(name):
    info = _infix_ops.get(name)
    return info[1] if info else 'left'

def parse_infix(src):
    """中缀表达式解析入口：词法分析 → Pratt 解析 → Scheme S-表达式"""
    toks = _infix_lex(src)
    if not toks:
        return NIL
    return _infix_parse(toks)

def _infix_lex(src):
    """中缀算符定制词法分词器
       不同于顶层 tokenizer，此分词器专为中缀语法设计：
       - 识别多字符算符 ** // <= >= != += -= *= /=
       - 数字（含浮点、科学计数法）
       - 标识符（字母数字及符号字符 _*?!$%&+-@^~）
       - 括号 () 分组
       - 单独 + - 作为操作符而非数字前缀时的特殊处理（上文中运算符情形）
       - 布尔字面量 #t #f"""
    tokens = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c in ' \t\n\r':
            i += 1; continue
        if c == ';':
            while i < n and src[i] != '\n': i += 1
            continue
        # 双字符操作符检查
        if i + 1 < n and src[i:i+2] in ('**', '//', '<=', '>=', '!=', '+=', '-=', '*=', '/='):
            tokens.append(src[i:i+2]); i += 2; continue
        # 数字或小数点开头的数字（.5, 3.14, 1e10）
        if c.isdigit() or (c == '.' and i + 1 < n and src[i+1].isdigit()):
            j = i; has_dot = False
            while j < n and (src[j].isdigit() or (src[j] == '.' and not has_dot)):
                if src[j] == '.': has_dot = True
                j += 1
            # 科学计数法：指数部分 e/E[+-]digits
            if j < n and src[j] in 'eE':
                j += 1
                if j < n and src[j] in '+-': j += 1
                while j < n and src[j].isdigit(): j += 1
            tokens.append(src[i:j]); i = j; continue
        # 标识符：字母或特殊符号开头，后续可包含字母数字及运算符字符
        if c.isalpha() or c in '_*?!$%&':
            j = i
            # 单独的 + 或 - 符号（非数字前缀用法）立即作为操作符输出
            if c in '+-' and (j + 1 >= n or not (src[j+1].isalnum() or src[j+1] in '_.*?!$%&+-@^~')):
                tokens.append(c); i += 1; continue
            while j < n and (src[j].isalnum() or src[j] in '_.*?!$%&+-@^~'): j += 1
            tokens.append(src[i:j]); i = j; continue
        # 单字符操作符
        if c in '+-*/^%=!<>,()=':
            tokens.append(c); i += 1; continue
        # #t/#f 布尔
        if c == '#':
            j = i + 1
            if j < n and src[j] in 'tf': j += 1
            tokens.append(src[i:j]); i = j; continue
        i += 1
    return tokens

def _infix_parse(tokens):
    p = _InfixParser(tokens)
    return p.parse()

class _InfixParser:
    """基于 Pratt 优先级算法的中缀解析引擎"""
    def __init__(self, tokens):
        self.tokens = tokens
        self.pos = 0

    def peek(self):
        return self.tokens[self.pos] if self.pos < len(self.tokens) else None

    def next(self):
        t = self.tokens[self.pos]
        self.pos += 1
        return t

    def parse(self):
        return self._expr(0)

    def _expr(self, min_prec):
        """Pratt 表达式解析：不断读取操作符直到优先级不足
            min_prec: 最小允许优先级，低于此值则停止"""
        left = self._primary()
        while True:
            tok = self.peek()
            if tok is None or tok in (')', '}', ','):
                break
            op = tok
            prec = _infix_prec(op)
            if prec is None or prec < min_prec:
                break
            self.next()
            # 右结合算符：相同优先级允许嵌套（如 a ^ b ^ c → (expt a (expt b c))）
            # 左结合算符：提升 1 级防止右嵌套
            nxt = prec if _infix_assoc(op) == 'right' else prec + 1
            right = self._expr(nxt)
            # = 等号赋值展开为 (set! x y)
            if op == '=':
                if not isinstance(left, Sym):
                    raise SyntaxError(f"Invalid lvalue in assignment: {left}")
                left = _lst([Sym('set!'), left, right])
                continue
            # += -= *= /= 自赋值展开为 (set! x (op x y))
            for assign_op, scheme_op in [('+=', '+'), ('-=', '-'), ('*=', '*'), ('/=', '/')]:
                if op == assign_op:
                    if not isinstance(left, Sym):
                        raise SyntaxError(f"Invalid lvalue in assignment: {left}")
                    left = _lst([Sym('set!'), left, _lst([Sym(scheme_op), left, right])])
                    break
            else:
                # 通用算符映射：^ → expt, ** → expt, // → quotient, % → modulo, != → not=
                scheme_map = {'^': 'expt', '**': 'expt', '//': 'quotient',
                              '%': 'modulo', '!=': 'not='}
                sop = scheme_map.get(op, op)
                left = _lst([Sym(sop), left, right])
        return left

    def _primary(self):
        """解析操作数：数字常量、符号、括号子表达式、布尔、一元 +/-"""
        tok = self.peek()
        if tok is None:
            raise SyntaxError("Unexpected end of infix expression")
        if tok == '(':
            self.next()
            expr = self._expr(0)
            if self.next() != ')':
                raise SyntaxError("Expected ')' in infix expression")
            return expr
        # 一元负号：-x → (- x)，优先级 5 高于所有二元算符
        if tok == '-':
            self.next()
            return _lst([Sym('-'), self._expr(5)])
        # 一元正号：+x → x（恒等）
        if tok == '+':
            self.next()
            return self._expr(5)
        raw = self.next()
        # 尝试解析为数值：含 . 或 e/E 的为 float，否则 int（base自动识别 0x/0o/0b）
        try:
            if '.' in raw or 'e' in raw or 'E' in raw:
                return float(raw)
            return int(raw, 0)
        except (ValueError, TypeError):
            pass
        if raw == '#t': return TRUE
        if raw == '#f': return FALSE
        return Sym(raw)
