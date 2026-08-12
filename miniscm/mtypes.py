# mtypes.py
"""
模块：mtypes.py
职能：定义 Scheme 运行时的核心数据结构、符号池管理、环境（Env）寻址机制以及通用对象格式化。
"""
# Python 标准库导入 —— 所有 Scheme 数值类型基于 Python 原生类型
from fractions import Fraction

# ═══════════════════════════════════════════════════════════════
# 1. 符号池（Symbol Interning）与常量定义
# ═══════════════════════════════════════════════════════════════

# Box: 闭包捕获的可变引用单元（set! 后闭包内可见新值，支持 named-let JIT 编译）
class Box:
    __slots__ = ('value',)
    def __init__(self, value=None):
        self.value = value
    def __repr__(self):
        return f'<box {self.value!r}>'

class Sym:
    """
    Scheme 符号类型（Symbol）。
    利用 __new__ 拦截实例化，在全局私有字典 _intern 中维持符号的唯一实例（Interning 模式）。
    这保证了在整个运行期中，相同名字的符号具有完全一致的内存地址（id），从而将后续的符号比较优化为 O(1) 的指针比较。

    设计意图：Scheme 中符号是原子性标识符，大量用于变量名、关键字、枚举标签。
    Interning 确保 (eq? a b) 等价于 Python 的 (a is b)，而不需要字符串比较。
    这在 eval 循环的 dispatch 路径中是关键性能优化 —— 特殊形式的分发依赖 Sym 的恒等比较。

    已知缺陷：_intern 字典随程序运行无限增长，无 GC 回收机制。长时间运行的 REPL 可能内存泄漏。
    __eq__ 实现中先用 __class__ 检查再比较 name，防止与字符串等非 Sym 类型误判相等。
    __bool__ 返回 self is not FALSE —— 这意味着 Sym('#f') 本身是 falsy，但所有其他 Sym 都是 truthy。
    这一设计让 (if #f ...) 正常工作，因为 FALSE 是 Sym('#f') 且 __bool__ 返回 False。
    """
    _intern = {}
    def __new__(cls, s):
        # 拦截 __new__ 实现 interning：若已有实例则直接返回，否则创建并注册
        try:
            return cls._intern[s]
        except KeyError:
            obj = super().__new__(cls)
            obj.name = s
            cls._intern[s] = obj
            return obj
    def __repr__(self): return self.name
    def __eq__(self, o): return isinstance(o, Sym) and self.name == o.name
    def __hash__(self): return hash(self.name)
    def __bool__(self): return self is not FALSE

# 符号快捷构造函数
S = lambda n: Sym(n)

# 预实例化核心语法关键词符号，避免运行期动态哈希分配
# 这些符号在 eval 循环的 dispatch 中通过 is 恒等比较分发，因此必须在模块加载时完成 interning。
SYM_APPLY = S('apply')
SYM_ARGS = S('args')
SYM_BEGIN = S('begin')
SYM_DEFINE = S('define')
SYM_DM = S('define-macro')
SYM_DS = S('define-syntax')
SYM_ELLIPSIS = S('...')
SYM_IF = S('if')
SYM_LAMBDA = S('lambda')
SYM_LS = S('let-syntax')
SYM_LRS = S('letrec-syntax')
SYM_LT = S('<>')
SYM_LT3 = S('<...>')
SYM_QQ = S('quasiquote')
SYM_QS = S('quasisyntax')
SYM_QUOTE = S('quote')
SYM_SETF = S('set!-form')
SYM_SETBANG = S('set!')
SYM_SR = S('syntax-rules')
SYM_SYNTAX = S('syntax')
SYM_UNQUOTE = S('unquote')
SYM_UNSPLICE = S('unquote-splicing')
SYM_USCORE = S('_')
SYM_USPLICES = S('unsyntax-splicing')
SYM_USYNTAX = S('unsyntax')
# SYM_VOID 故意使用一个永远不会在正常代码中出现的表达式字符串 '(if #f #f)'，
# 这样在 Scheme 层面 (eq? x (if #f #f)) 永远为假 —— 这是 R7RS 未指定值的惯用表示。
SYM_VOID = S('if #f #f')
SYM_IMPORT = S('import')
SYM_SC = S('syntax-case')
SYM_WS = S('with-syntax')
SYM_GT = S('generate-temporaries')
SYM_DEBUG = S('%break')
SYM_DBGTRACE = S('debug-trace')
SYM_THE_ENVIRONMENT = S('the-environment')

class _Nil:
    """代表 Scheme 的空列表常量 '()"""
    # __hash__ 返回常数 0：所有 _Nil 实例（实际上永远只有一个）哈希值相同。
    # 由于 _Nil 是单例（NIL），此设计没有性能问题。
    # 但若未来有人创建多个 _Nil 实例，它们在 set/dict 中会发生哈希冲突。
    def __hash__(self): return 0
    def __bool__(self): return True
class _Void:
    """代表未定义或无返回值的副作用行为
    设计意图：Scheme 中很多副作用形式（如 set!、define）不返回有意义的值。
    VOID 单例在 eval 循环中被检测到并抑制打印输出（REPL 不显示 #<void>）。
    与 Scheme 标准中的 "unspecified" 对应，但不等于某个可被 Scheme 代码捕获的值。"""
    pass
class _Eof:
    """代表流输入的结束符
    由 reader 在遇到文件结束时返回。EOF 对象会传播到 eval 循环，
    用于终止 REPL 的读取-求值-打印循环。"""
    pass

class Cell:
    """
    Scheme 核心双子节点（Cons Cell）数据结构。
    __slots__ 声明限制了该类实例不能动态创建其字典，大幅度缩减了内存开销并提升了 CPU 缓存局部性。

    设计意图：Cell 是 Scheme 列表和点对（pair）的唯一底层结构。
    (a . d) 表示为 Cell(a, d)，标准列表是以 NIL 结尾的 Cell 链。
    __slots__ 避免了每个 Cell 实例的 __dict__ 开销（约 56 字节/对象），
    在存储大型列表（50k+ 元素）时内存节省显著。

    与 eval 循环的关系：所有 S-表达式均解析为 Cell/Sym/NIL 树。
    特殊形式的分发通过检测 (car expr) 是否为特定 Sym 来完成。

    与 JIT 的关系：JIT 编译器将 (car x) 编译为 Python 属性访问 x.car，
    将 (null? x) 编译为 x is NIL，避免了函数调用开销。

    已知缺陷：
    __len__ 是 O(N) 操作 —— 在需要频繁获取长度的场景可能成为性能瓶颈。
    没有环状列表保护 —— 如果传入循环 Cell 链，__len__ 将无限循环。
    __getitem__ 同样是 O(N) 且没有边界预检。

    注意：NIL 不是 Cell 的实例 —— 它是 _Nil 类的单例。
    因此判断一个值是否为 pair 应使用 isinstance(x, Cell) 而非 type(x) is Cell 的 is 比较。
    """
    __slots__ = ('car','cdr')
    def __init__(self,a,d):
        self.car, self.cdr = a, d
    def __hash__(self):
        # Cell 的哈希使用对象身份（id）而非内容哈希。
        # 这一选择使得 Cell 可以作为字典键，但 (equal? a b) 不等价于 (hash a) == (hash b)。
        # 这是有意为之：Scheme 中 pair 的默认哈希行为未指定，使用 id(self) 避免循环引用哈希的无限递归。
        return id(self)
    def __len__(self):
        # 迭代遍历计算标准列表长度，规避递归引起的 Python 栈帧消耗
        # 使用 while 循环而非递归，确保对 100k+ 长度的列表也不会栈溢出。
        n=0; cur=self
        while isinstance(cur, Cell):
            n+=1
            cur=cur.cdr
        return n
    def __getitem__(self,i):
        # O(N) 时间复杂度的索引访问
        # 从当前 Cell 开始沿 cdr 链前进 i 步。
        # 若在到达目标索引前遇到非 Cell 对象（如 NIL 或非列表的点对），抛出 IndexError。
        cur=self
        for _ in range(i):
            if not isinstance(cur, Cell): raise IndexError
            cur=cur.cdr
        return cur.car
    def __iter__(self):
        cur = self
        while isinstance(cur, Cell):
            yield cur.car
            cur = cur.cdr

    def __repr__(self):
        r=_rep(self)
        return f'({r})' if r else '()'

class SchemeString:
    """Scheme 专用可变字符串
    设计意图：Python 的 str 是不可变的，但 Scheme 的 string 支持 string-set! 原地修改。
    SchemeString 内部使用 Python list 存储字符，通过 __setitem__ 实现可变性。
    
    与 reader/primitives 的关系：reader 遇到字符串字面量时创建 SchemeString 实例。
    string-append、string-copy、string-set!、string-ref 等过程操作 SchemeString。
    _pr 函数处理 SchemeString 时调用 __repr__ 自动转义特殊字符。
    
    已知缺陷：
    - __repr__ 中的转义不完整（缺少 \n、\t、\r 等转义序列的字面表示 -> 显示原始换行符）
    - 与 Python str 的隐式转换界限模糊：SchemeString 和 Python str 在代码中是两种不同的类型，
      某些内部函数可能期望 Python str 而收到 SchemeString 导致意外行为。
    - 由于 SchemeString 的定义晚于 Sym 的 interning 缓存，symbol->string 返回 SchemeString，
      但 Sym._intern 的 key 是 Python str —— 这意味着用 SchemeString 作为键查 _intern 会 miss。
    """
    __slots__ = ('data',)
    def __init__(self,s): self.data=list(str(s))
    def __repr__(self): return '"'+''.join(self.data).replace('\\','\\\\').replace('"','\\"')+'"'
    def __str__(self): return ''.join(self.data)
    def __len__(self): return len(self.data)
    def __getitem__(self,i): return SchemeChar(self.data[i])
    def __setitem__(self,i,c): self.data[i]=c.char if isinstance(c,SchemeChar) else c
    def __bool__(self): return True
    def __hash__(self): return hash(str(self))
    def __eq__(self, other):
        return str(self) == (str(other) if isinstance(other, (str, SchemeString)) else other)

class SchemeChar:
    """Scheme 字符类型
    设计意图：Scheme 中字符是独立类型，不等同于长度为 1 的字符串。
    #\a 和 "a" 在 Scheme 中是不同类型的值（char? vs string?）。
    Python 没有对应的原生字符类型，因此使用封装类。
    
    注意：__eq__ 实现使用 isinstance 检查，但 __hash__ 未定义 ——
    这意味着 SchemeChar 实例不可作为字典键或在 set 中使用。
    如果 SchemeChar 需要哈希，应基于 self.char 定义 __hash__。
    """
    __slots__ = ('char',)
    def __init__(self,c): self.char=c
    def __hash__(self): return hash(self.char)
    def __repr__(self): return '#\\'+('space' if self.char==' ' else self.char)
    def __eq__(self,o): return isinstance(o, SchemeChar) and self.char==o.char

class SchemeVector:
    """Scheme 向量（数组）类型，底层基于 Python list
    向量是 Scheme 中 O(1) 随机访问的线性数据结构。
    vector-ref 和 vector-set! 直接映射到 Python 的 list 索引。
    
    与 reader 的关系：#(1 2 3) 被解析为 SchemeVector([1, 2, 3])。
    
    已知缺陷：__repr__ 中的元素之间使用空格分隔，但 Scheme 标准要求
    元素间可包含任意空白。当前实现符合常见打印规范。
    """
    __slots__ = ('data',)
    def __init__(self,d): self.data=list(d)
    def __repr__(self): return '#('+' '.join(_pr(x) for x in self.data)+')'
    def __len__(self): return len(self.data)
    def __getitem__(self,i): return self.data[i]
    def __setitem__(self,i,v): self.data[i]=v
    def __bool__(self): return True

class SchemeBytevector:
    """Scheme 字节向量（U8 数组）
    设计意图：用于二进制数据的高效存储，每个元素是 0-255 的整数。
    底层基于 Python 的 bytearray，支持 bytes、list、tuple、bytearray 多种构造方式。
    
    __init__ 的多分支构造逻辑：先尝试直接使用 bytes/bytearray（零拷贝），
    然后尝试 list/tuple（逐元素转换），最后兜底使用 str.encode。
    这种 fallback 链确保了对各种 Python 类型的兼容性。
    
    与 primitives 的关系：bytevector 过程（bytevector-u8-ref、bytevector-length 等）
    直接操作 .data 属性。
    """
    __slots__ = ('data',)
    def __init__(self,d):
        if isinstance(d,bytes): self.data=bytearray(d)
        elif isinstance(d,(list,tuple)): self.data=bytearray(d)
        elif isinstance(d,bytearray): self.data=d
        else: self.data=bytearray(str(d).encode())
    def __repr__(self): return '#u8('+','.join(str(b) for b in self.data)+')'
    def __len__(self): return len(self.data)
    def __getitem__(self,i): return self.data[i]
    def __bool__(self): return True

# 全局单例定义
# 这些单例在模块加载时创建，贯穿整个解释器生命周期。
NIL   = _Nil()
VOID  = _Void()
EOF   = _Eof()
# TRUE 和 FALSE 是 Sym 的实例，利用 Sym.__bool__ 机制：
# FALSE.__bool__() 返回 False（因为它是 FALSE 自身）；
# TRUE.__bool__() 返回 True（因为 TRUE is not FALSE）。
TRUE  = Sym('#t')
FALSE = Sym('#f')
# _cont_id 用于为每次 call/cc 捕获的延续生成唯一标识符
_cont_id=0
# _gensym_ctr 用于 generate-temporaries 宏辅助函数生成唯一符号名
# 使用 list 包装是为了在闭包中可修改（Python 2 兼容模式）
_gensym_ctr = [0]

class _ContinuationEscape(BaseException):
    """用于实现 call/cc 机制的控制流非局部异常逃逸控制符
    设计意图：call/cc 捕获当前续延（continuation）后，通过抛出 _ContinuationEscape
    异常并携带恢复值来实现非局部退出。这类似于 Scheme 中 call/cc 的 "escape procedure" 语义。
    
    工作原理：
    1. eval 循环在 call/cc 调用点创建一个捕获了当前 eval 状态的闭包作为续延
    2. 当续延被调用时，将值封装在 _ContinuationEscape 中抛出
    3. 上层 try/except 捕获异常并提取值，作为 call/cc 调用的结果
    4. 这种基于异常的续延实现无法保存完整的调用栈状态 —— 仅支持 "escape" 语义，
       不支持 Scheme 完整的多续延（multi-shot continuation）。
    """
    pass

class Promise:
    """Scheme 延迟求值 Promise（用于 delay/force）
    设计意图：delay 创建一个 Promise，force 触发其求值并缓存结果（记忆化）。
    forced 标志位确保 thunk 最多被执行一次 —— 即使 force 多次调用也只计算一次。
    
    与 eval 循环的关系：(delay expr) 编译为 Promise(lambda: expr)。
    (force p) 检查 p.forced：若已求值则直接返回 p.val，否则执行 p.thunk()，
    将结果存入 p.val 并设置 p.forced = True。
    
    已知缺陷：非线程安全。多个线程同时 force 同一 Promise 可能导致竞态条件。
    """
    __slots__=('forced','val','thunk')
    def __init__(self,thunk): self.forced=False; self.val=None; self.thunk=thunk

class SyntaxObject:
    """卫生宏系统（Syntax-case）专用的语法对象包装，携带着词法范围上下文
    设计意图：在宏展开过程中，SyntaxObject 包裹了带有词法信息的 S-表达式片段。
    展开器通过 SyntaxObject 跟踪每个标识符的原始定义位置，防止宏生成的标识符
    意外捕获宏使用处的绑定（卫生性）。
    
    __slots__ 只存储 expr（内部 S-表达式），词法信息由宏展开器通过
    transformer environment（语法环境）单独维护。
    
    与 eval 循环的关系：eval 在遇到 SyntaxObject 时调用 _so(x) 解包，
    以内部 S-表达式进行求值。define-syntax 创建语法转换器，
    其返回值必须是 SyntaxObject 或 NIL。
    
    与 _sn/_so 辅助函数的关系：
    - _so(x) 解包 SyntaxObject，在需要 expr 原始值的上下文中使用
    - _sn(x) 从 Sym 提取 .name 字符串，或在 SyntaxObject 包裹 Sym 时先解包再提取
    
    注意：SyntaxObject 不参与 _eq 或哈希 —— 两个 SyntaxObject 即使 expr 相同也不被视为 eqv?。
    """
    __slots__=('expr',)
    def __init__(self,expr): self.expr=expr
    def __repr__(self): return f"#<syntax {_pr(self.expr)}>"

class ErrorObject:
    """Scheme 错误对象，存放错误信息与触发异常的干扰源
    设计意图：R7RS 的 error 过程创建一个封装备注消息和 irritants（激发值）的错误对象。
    raise-continuable 可以捕获并检查这个对象。
    
    irritants 是 Scheme 列表（Cell/NIL），不是 Python list。
    __repr__ 返回消息的 Scheme 表示，与 Scheme 的 display 语义一致。
    """
    __slots__ = ('message','irritants')
    def __init__(self,message,irritants=NIL):
        self.message=message
        self.irritants=irritants
    def __repr__(self): return _pr(self.message)

class SchemeException(Exception):
    """Scheme 异常 — 将 Scheme 值包装为 Python 异常
    设计意图：当 Scheme 代码调用 error、raise 或触发类型错误时，
    需要将 Scheme 值（可能是任意 Scheme 对象）通过 Python 异常机制传播。
    SchemeException(self.val) 将 Scheme 值包装在标准 Python 异常中，
    使得上层 Python 的 try/except 可以捕获它。
    
    super().__init__(str(val)) 确保 Python 层面的异常消息可读。
    """
    def __init__(self,val): self.val=val; super().__init__(str(val))
    def __repr__(self): return f"SchemeException({_pr(self.val)})"

class TailCall:
    """虚拟机蹦床（Trampoline）核心，用于在非 JIT 模式下传递尾部未完成的调用帧
    设计意图：Python 不支持尾调用优化（TCO），直接递归的 Scheme 函数会耗尽 Python 调用栈。
    TailCall 机制将尾调用转换为 "返回一个待执行调用的描述" 而非实际递归调用，
    由 eval 循环的主 while True 循环（蹦床）持续解包执行。
    
    与 eval 循环的关系（见 AGENTS.md Layer 1 & Layer 2）：
    - 当 _eval 发现函数体最后一个表达式求值结果为 TailCall 时，不递归调用 _eval，
      而是从 TailCall 提取 expr 和 env，在 while True 循环中继续 dispatch
    - TailCall(expr, env) 中的 env 是尾调用位置的词法环境（已绑定参数的帧）
    
    与 LambdaProc/JIT 的关系：
    - 编译后的 lambda 对跨函数尾调用使用 __mscm_make_tail_call__(func, args)，
      返回 TailCall(func, args_env) 给 _eval 循环处理
    - 自我递归尾调用在编译代码内部用 while/continue 处理，不生成 TailCall
    - 内置函数（builtins）尾调用使用 __mscm_invoke__ 直接调用，不经过 TailCall
    
    注意：TailCall 只用于用户定义函数之间的尾调用。内置函数列表中的函数
    （如 car、cons、+ 等）由 _call 直接执行，不创建 TailCall。
    """
    __slots__ = ('expr', 'env')
    def __init__(self, expr, env):
        self.expr = expr
        self.env = env

# ═══════════════════════════════════════════════════════════════
# 2. 词法环境（Lexical Environment）作用域与静态寻址加速
# ═══════════════════════════════════════════════════════════════

_UNBOUND = object()

class Env:
    """
    词法范围环境帧。
    通过 parent 向上串联形成静态词法链（Scope Chain）。
    
    设计意图：Scheme 的词法作用域通过 Env 帧的链式结构实现。
    每个函数调用创建一个新的 Env 帧，parent 指向定义时的词法环境。
    变量查找沿 parent 链自内向外进行。
    
    __slots__ 减少了每个 Env 实例的内存开销 —— 在深递归或大量闭包场景中至关重要。
    
    与 eval 循环的关系：eval 在调用函数前通过 _bind_params 或循环内建绑定创建 Env 帧。
    lambda 的 body 在所有参数绑定到新 Env 后在扩展后的作用域中求值。
    
    与 JIT 的关系：编译后的 lambda 直接操作 env.data 字典进行变量绑定和查找。
    对于不可变内置函数（_IMMUTABLE_PRIMITIVES），JIT 在编译时直接嵌入函数引用，
    避免运行时 env.lookup 的开销。
    
    与宏系统的关系：syntax-case 宏展开器创建多个临时 Env 帧用于模式变量绑定。
    宏生成代码中的标识符引用通过语法环境解析。
    
    已知缺陷：Env 没有 "冻结" 机制 —— 理论上任何代码都可以修改任意帧的绑定。
    这可能导致意外的副作用（如 (set! car ...) 修改全局环境）。
    """
    __slots__=('data','parent')
    def __init__(self,parent=None):
        self.data={}
        self.parent=parent

    def lookup(self, k):
        """
        根据符号键查找绑定的值。
        性能优化：引入穿透路径快速通道（Fast Global Scope Shortcut）。
        当第一层 Frame 未命中时，若父环境是全局大环境 be，直接跳过逐级向上的动态向上查找，
        利用 be.data 的 O(1) 字典查找返回结果，从而缩减了环境寻址中的链式深度寻址。

        参数 k：可以是 Sym、SyntaxObject（被 _sn 提取 name）或 str。
        返回值：绑定的值。
        查找失败时：抛出 NameError("unbound: <name>")。

        查找顺序：
        1. 当前帧 self.data（O(1) 字典查询）
        2. 若 parent is be，直接查 be.data（跳过链遍历）
        3. 沿 parent 链遍历，逐个检查 e.data（O(N) 链深度）

        性能优化设计：大多数变量访问要么在当前帧找到（局部变量），
        要么在全局帧找到（全局变量）。直接检查 parent is be 避免了
        不必要的链遍历，是常见的 "快速全局短路" 优化模式。

        注意：k 可以是 Python str！_sn(x) 可能在入口处已被调用，
        因此 lookup 内部也处理 k 不是 Sym 的情况。
        """
        name = k.name if isinstance(k, Sym) else k
        data = self.data
        if name in data:
            v = data[name]
            return v.value if isinstance(v, Box) else v

        parent = self.parent
        if parent is be:
            try:
                v = be.data[name]
                return v.value if isinstance(v, Box) else v
            except KeyError: pass

        e = parent
        while e is not None:
            if name in e.data:
                v = e.data[name]
                return v.value if isinstance(v, Box) else v
            e = e.parent
        raise NameError(f"unbound: {k}")

    def lookup_silent(self, k, sentinel=_UNBOUND):
        """静默寻址接口，当变量未绑定时不抛出异常而是返回哨兵对象
        用于宏展开器和分析器（如需要检查变量是否已定义但不希望中断流程的上下文）。
        查找逻辑与 lookup 相同，但使用 .get(name, sentinel) 而非直接索引。
        
        sentinel 默认是 _UNBOUND 哨兵对象（模块级唯一的 object() 实例），
        调用者可以通过 sentinel is _UNBOUND 判断变量是否未绑定。
        """
        name = k.name if isinstance(k, Sym) else k
        val = self.data.get(name, sentinel)
        if val is not sentinel:
            return val.value if isinstance(val, Box) else val

        e = self.parent
        if e is None:
            return sentinel
        if e is be:
            val = be.data.get(name, sentinel)
            return val.value if isinstance(val, Box) else val

        while e is not None:
            val = e.data.get(name, sentinel)
            if val is not sentinel:
                return val.value if isinstance(val, Box) else val
            e = e.parent
        return sentinel

    def define(self,k,v):
        """在当前环境帧（最内层作用域）定义或重写绑定
        等效于 Scheme 的 (define name val) 在当前作用域创建新绑定。
        即使外层已有同名绑定，define 也在最内层创建新变量（而非修改外层绑定）。
        
        参数 k：Sym 或 str。v：任意 Scheme 值。
        
        注意：define 不沿链查找 —— 它总是在 self.data 上直接设置。
        这与 set! 的语义不同。
        
        关于 Sym 和 str 的说明：Env.data 的键始终是字符串 (Python str)。
        即使传入 Sym，define 也会提取 .name 作为字典键。
        这是为与 _sn 辅助函数保持一致 —— 所有环境操作最终以字符串为键。
        """
        name = k.name if isinstance(k, Sym) else k
        self.data[name]=v

    def set_val(self, k, v):
        """
        Scheme set! 语义。
        自内向外沿词法范围链查找首个绑定了该符号的 Frame 并修改其值。
        如果在当前和祖先环境中均未找到，则在本地最内层环境进行定义。

        参数 k：Sym 或 str。v：任意 Scheme 值。
        返回值：VOID（Scheme 中 set! 返回未指定值）。

        边界情况：
        - 如果在整个链中找不到绑定，行为等同于 define（在 self.data 新建绑定）。
          这是 Scheme R7RS 允许的行为 —— (set! undefined-var val) 可能定义新变量。
        - 若找到绑定但该帧的 parent 链在后续遍历中被修改，set_val 已返回，
          不会重新查找。

        注意：set_val 返回 VOID 而非 None，以确保 REPL 不打印 #<void>。
        """
        name = k.name if isinstance(k, Sym) else k
        e = self
        while e is not None:
            if name in e.data:
                e.data[name] = v
                return VOID
            e = e.parent
        self.define(name, v)
        return VOID

# 全局唯一内建作用域帧
# be（Built-in Environment）是解释器的全局环境根帧。
# 所有内建函数（在 primitives.py 中用 @_b 注册）和 scm/ 库文件定义的绑定都存储在这里。
# be 没有 parent（初始化为 Env(None) 但后面的 parent 赋值为 be 自身？实际上 Env() 时 parent=None，
# 然后通过后续赋值保证 be 在 lookup 中被特殊处理。
# 注意：be 在 lookup 中通过 "if parent is be" 的短路逻辑被特殊对待，
# 因为它是唯一且固定的全局帧，不需要沿链遍历。
be=Env()

def builtin(name, fn=None):
    """辅助修饰器：将 Python 函数直接作为 Scheme 基础过程注入 be 帧
    使用方式：
    1. 作为装饰器：@builtin 将函数以其 __name__ 注册到 be
    2. 显式指定名字：builtin('my-func', my_func) 以指定 Scheme 名注册
    
    这个函数是 primitives.py 中 @_b 装饰器的底层实现。
    所有内置过程（约 300 个）都通过此路径注册。
    
    注意：如果 fn 的 __name__ 与 Scheme 中的名字不匹配（如 Python 的 map 对应 Scheme 的 map），
    需要使用第二种形式显式命名。
    """
    if fn is None and callable(name):
        fn = name
        name = fn.__name__
    be.define(name, fn); return fn

# ═══════════════════════════════════════════════════════════════
# 3. 辅助格式化与打印引擎
# ═══════════════════════════════════════════════════════════════

def _rep(p, seen=None):
    """
    Cons Cell 的打印序列化器。
    引入 seen 集合，能通过检测历史节点对象 ID 的方式防范并中断循环列表（Circular Lists）的打印，
    避免底层栈溢出，并打印出 '...' 提示。

    参数 p：任意 Scheme 值。但本函数仅在 p 为 Cell 时由 __repr__ 调用。
    参数 seen：可选的 set，记录已访问的 Cell 对象 ID。
    返回值：字符串，表示 Cell 的 car/cdr 序列。

    打印规则：
    - 若 p 不是 Cell，直接调用 _pr(p) 作为原子值打印
    - 空列表（即 Cell 但 _rep 返回空 + __repr__ 返回 '()'）特例：由 Cell.__repr__ 处理
    - 标准列表（以 NIL 结尾的 Cell 链）：递归打印 car，迭代打印每个后续 car
    - 非标准列表（点对，cdr 不是 NIL 也不是 Cell）：在末尾打印 ". " + cdr
    
    循环检测：使用 seen 集合记录已访问的 Cell id。
    如果再次遇到相同 id，打印 '...' 并中断。
    这保障了对循环列表的打印不会无限递归导致 Python 栈溢出。
    
    注意：seen 只在第一次调用时创建 set()。递归调用传递 seen 引用，
    因此同一次打印过程中的所有嵌套 _rep 调用共享同一个 seen 集合。
    """
    if not isinstance(p, Cell): return _pr(p)
    if seen is None: seen=set()
    if id(p) in seen: return '...'
    seen.add(id(p))
    r=_pr(p.car); q=p.cdr
    while isinstance(q, Cell):
        if id(q) in seen: r+=' ...'; q=NIL; break
        seen.add(id(q))
        r+=' '+_pr(q.car); q=q.cdr
    if q is not NIL: r+=' . '+_pr(q)
    return r

def _pr(x):
    """将 Scheme 运行时的对象完全转化为标准的符合 Scheme 规范的外部展示字符串
    这是核心打印函数，被 REPL、display、write、error 消息等广泛使用。
    
    参数 x：任意 Scheme 值。
    返回值：字符串（Python str），其格式与 Scheme 的 write 过程一致。
    
    类型分派顺序（按检查频率和重要性排列）：
    1. TRUE/FALSE 单例 —— 最频繁，放在最前
    2. NIL 单例
    3. Sym —— 返回 .name
    4. Python str —— 转义并加引号（Scheme 字符串字面量）
    5. int —— str(x)
    6. Fraction —— "numerator/denominator"，若分母为 1 则只显示分子
    7. float —— 处理 +inf.0、-inf.0、+nan.0 和普通浮点数
    8. complex —— 复杂格式化逻辑（见下面详细说明）
    9. 特殊 tuple ('char', c) —— 兼容 reader 遗留格式
    10. _Void —— "#<void>"
    11. Python list —— 打印为向量字面量（兼容代码内部使用）
    12. Cell —— 委托 _rep
    
    complex 格式化细节：
    - 实部为 0 时不显示实部（纯虚数）
    - 虚部为 0 时显示为实数（不做复数）
    - 虚部为 1 或 -1 时只显示 "i" 或 "-i"
    - 整数浮点数（如 3.0）显示为整数 "3"
    
    注意：SchemeString 类型在此没有显式分支 —— 它通过 __repr__ 方法处理。
    SyntaxObject、ErrorObject、Promise 等其他类型在最后 fallback 到 repr(x)。
    """
    if x is TRUE: return '#t'
    if x is FALSE: return '#f'
    if x is NIL: return '()'
    if isinstance(x, Sym): return x.name
    if isinstance(x, str): return '"'+x.replace('\\','\\\\').replace('"','\\"')+'"'
    if isinstance(x, int): return str(x)
    if isinstance(x, Fraction):
        if x.denominator==1: return str(x.numerator)
        return f'{x.numerator}/{x.denominator}'
    if isinstance(x, float):
        if x==float('inf'): return '+inf.0'
        if x==float('-inf'): return '-inf.0'
        if x!=x: return '+nan.0'
        return str(x)
    if isinstance(x, complex):
        r,i=x.real,x.imag
        if i==0:
            if r==int(r): return str(int(r))
            return str(r)
        sr='' if r==0 else (str(int(r)) if isinstance(r,float) and r==int(r) else str(r))
        sgn='+' if i>0 and r!=0 else '-' if i<0 else '' if r==0 else ''
        ai=abs(i)
        si='i' if ai==1 else (str(int(ai)) if isinstance(ai,float) and ai==int(ai) else str(ai))+'i'
        if r==0 and i<0: si='-'+si
        return sr+sgn+si
    if isinstance(x,tuple) and len(x) == 2 and x[0]=='char': return '#\\'+('space' if x[1]==' ' else x[1])
    if isinstance(x, _Void): return '#<void>'
    if isinstance(x, list): return '#('+' '.join(_pr(v) for v in x)+')'
    if isinstance(x, Cell): return '('+_rep(x)+')'
    return repr(x)

def _cells(p):
    """生成器：将 Scheme 双子链表展平为 Python 迭代器
    参数 p：Scheme 列表（Cell 链，以 NIL 结尾）。
    Yields：列表中的每个 car 值。
    用途：当 Python 需要按顺序迭代 Scheme 列表的元素时使用。
    
    例如：_cells(Cell(1, Cell(2, Cell(3, NIL)))) 依次 yield 1, 2, 3
    非标准列表的末尾点对不会被 yield（迭代在遇到非 Cell 时停止）。
    
    注意：如果 p 不是 Cell（即 NIL），生成器立即结束，不 yield 任何值。
    """
    while isinstance(p, Cell): yield p.car; p=p.cdr

def _cell_len(p):
    """计算列表物理长度
    参数 p：Scheme 列表（Cell 链）。
    返回值：从 p 开始到第一个非 Cell（通常为 NIL）经过的 Cell 数量。
    
    注意：这是 O(N) 操作。与 Cell.__len__ 功能相同但作为独立函数存在，
    用于不需要创建 __len__ 调用开销的上下文。
    非标准列表（点对）的长度计算不包括末尾的非 NIL cdr。
    """
    n=0; cur=p
    while isinstance(cur, Cell): n+=1; cur=cur.cdr
    return n

def _so(x):
    """解开语法对象（SyntaxObject），还原其内部 S-Expression
    参数 x：任意 Scheme 值。
    返回值：若 x 是 SyntaxObject 则返回 x.expr，否则原样返回 x。
    
    在宏展开和 eval 的多个位置使用，确保 SyntaxObject 包裹的值被透明地解包处理。
    这是实现卫生宏的关键 —— 宏生成的代码可能包裹在 SyntaxObject 中，
    但 eval 需要看到内部的裸 S-表达式。
    """
    return x.expr if isinstance(x, SyntaxObject) else x

def _sn(x):
    """提取符号或字符串的名字
    参数 x：任意 Scheme 值。
    返回值：若 x 是 Sym 则返回 x.name（Python str），否则原样返回 x。
    
    用于统一处理 Sym 和 str 类型的参数。因为 Env 的字典键是 str，
    在调用 define/lookup/set_val 前需要将 Sym 转换为其 name。
    
    注意：如果 x 是 SyntaxObject 包裹的 Sym，_sn 不会解包 ——
    调用者需要先使用 _so(x) 解包，如果有必要再使用 _sn。
    某些代码路径同时使用 _so 和 _sn 来处理嵌套 SyntaxObject(Sym) 的情况。
    """
    return x.name if isinstance(x, Sym) else x

def _plist(p):
    """解开链表，返回一个平面列表，包含最后一个 cdr（哪怕是点号非规范列表）
    参数 p：Scheme 列表（Cell 链）。
    返回值：Python list，包含所有 car 值和末尾的点对值（如果有）。
    
    与 _cells 的区别：
    - _cells 忽略末尾的点对值（非 NIL cdr）
    - _plist 保留末尾点对值作为列表的最后一个元素
    - _cells 是生成器，_plist 是完整列表
    
    示例：
    - _plist(Cell(1, Cell(2, NIL)))  → [1, 2]
    - _plist(Cell(1, Cell(2, 3)))    → [1, 2, 3]（保留末尾点对 3）
    
    用途：宏系统中的模式匹配需要处理非标准列表（点对）形式的参数列表。
    """
    r=[]
    while isinstance(p, Cell): r.append(p.car); p=p.cdr
    if p is not NIL: r.append(p)
    return r

def _lst(items):
    if not isinstance(items, (list, tuple)):
        items = list(items)
    r=NIL
    for x in reversed(items): r=Cell(x,r)
    return r

def _bind_params(params, evaled, nenv):
    """
    将求值后的参数列表绑定到 lambda 的形式参数。
    
    参数 params：Python list of str，形式参数名列表（来自 lambda 表达式）。
              支持 rest: 前缀表示剩余参数（如 rest:args 表示将所有剩余参数绑定到 args）。
    参数 evaled：Python list，已求值的实际参数值。
    参数 nenv：Env 实例，参数绑定发生的新环境帧。
    返回值：无（原地修改 nenv.data）。
    
    处理逻辑：
    1. 遍历 params 中的每个形式参数
    2. 如果不以 'rest:' 开头：将 evaled[pi] 绑定到 nenv.data[p]
    3. 如果以 'rest:' 开头：将所有剩余参数（evaled[pi:]）打包为 Scheme 列表，
       绑定到去掉 'rest:' 前缀后的名字（p[5:]），然后 break 停止处理
    
    边界情况：
    - 如果 rest: 参数不在 params 末尾：break 导致后续 params 被忽略绑定
      （这是当前实现的限制 —— 实际上 Scheme 要求 rest 参数只能是最后一个）
    - 如果 evaled 长度少于非 rest 参数：evaled[pi] 访问会抛出 IndexError
      （由上层调用者保证参数个数匹配）
    - 如果 evaled 长度多于非 rest 参数且没有 rest: 前缀：
      Python 不会报错，但多余参数不会被绑定 —— 在 Scheme 中这是错误情况
      （由调用者负责检查参数个数）
    
    已知缺陷：
    - params 元素应是字符串（从 lambda 参数列表提取的符号名经 _sn 转换后），
      但如果意外传入 Sym 或 SchemeString，p[5:] 或字符串操作可能行为异常
    - rest: 前缀要求 p[5:] 切片 —— 如果名字长度不足 5（如 rest: 但空名字），
      p[5:] 返回空字符串，导致绑定到一个空名的变量
    """
    pi = 0
    for p in params:
        if p.startswith('rest:'):
            nenv.data[p[5:]] = _lst(evaled[pi:])
            break
        if pi < len(evaled):
            nenv.data[p] = evaled[pi]
        pi += 1

p_lst = _lst
p_cells = _cells
