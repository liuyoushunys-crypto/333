# primitives.py
import math, sys, cmath
from fractions import Fraction

from mtypes import (
    Sym, Cell, SchemeString, SchemeChar, SchemeVector, SchemeBytevector,
    Promise, SyntaxObject, SchemeException, ErrorObject, TailCall, NIL, VOID, EOF, TRUE, FALSE, Env, SYM_QUOTE,_UNBOUND,
    _pr, _sn, _plist, _lst, _ContinuationEscape, _cont_id, _gensym_ctr, builtin, be
)
from reader import read

# ── 从 primitives_first 导入自举核心函数 ──
from primitives_first import (
    car, cdr, cons, eqv, equal, call, port_out,
)

# char_val: extract Python str from SchemeChar
_has_sc = False
def cs_char(c):
    if isinstance(c, SchemeChar): return c.char
    if isinstance(c, tuple) and len(c) == 2 and c[0] == 'char': return c[1]
    if isinstance(c, str) and len(c) == 1: return c
    return str(c)[0] if len(str(c)) > 0 else ' '

# ── SECTION A: 位运算辅助函数（匿名 lambda 用于 bitwise 操作）──
AND = lambda a,b: a&b
IOR = lambda a,b: a|b
NOT = lambda a: ~a
XOR = lambda a,b: a^b

# ── 原生函数求值辅助器 ──

# scheme_truthy: check if a value is truthy in Scheme (only #f is false)
def scheme_truthy(v):
    return v is not FALSE and v is not False and v is not NIL


# char_val: 从字符表示中提取纯 Python str
#   支持 SchemeChar 对象和 ('char', str) 元组两种格式
# make_char: 统一创建 SchemeChar（接受元组或现有 SchemeChar）
def make_char(s):
    if isinstance(s,SchemeChar): return s
    if isinstance(s,tuple) and s[0]=='char': return SchemeChar(s[1])
    return SchemeChar(str(s))

# str_set_char: SchemeString 的原地字符设置（通过 .data 列表）
def char_val(c):
    if isinstance(c, SchemeChar): return c.char
    if isinstance(c, tuple) and len(c) == 2 and c[0] == 'char': return c[1]
    if isinstance(c, str) and len(c) == 1: return c
    if isinstance(c, int): return chr(c)
    raise SchemeException("char-val: invalid argument")

def str_set_char(v, i, c):
    v.data[i] = char_val(c)

# str_mutate: 强制 SchemeString 获得 .data 属性（惰性初始化）
#   坑：__class__.__name__ 检查而非 isinstance，因为未设置 __class__
def str_mutate(v):
    if not hasattr(v, 'data'):
        setattr(v, 'data', list(str(v)))
    return v

# vec_set_elem: 向量元素设置（支持 SchemeVector.data 和 Python list）
def vec_set_elem(v, i, x):
    if hasattr(v, 'data'):
        v.data[i] = x
    elif isinstance(v, list):
        v[i] = x
    return VOID

# bv_set_u8: 字节向量单字节设置
def bv_set_u8(v, i, x):
    v.data[i] = x
    return VOID

# is_list: 循环检测 true list（含环形链表检测 via seen set）
#   返回值是 TRUE/FALSE（Scheme 布尔值），不是 Python bool

# str_cons: 从多个字符参数构造字符串
def str_cons(*chars):
    return ''.join(c[1] if isinstance(c,tuple) else (c.char if hasattr(c,'char') else str(c)) for c in chars)


# assoc: 通用 assoc 查找，支持自定义比较函数 eq
def assoc(k,al,eq):
    while isinstance(al,Cell):
        p=al.car
        if isinstance(p,Cell) and eq(p.car,k) is TRUE: return p
        al=al.cdr
    return FALSE

# bit_op: 对参数列表执行二元位操作（折叠）
def bit_op(args,op):
    r=args[0]
    for x in args[1:]: r=op(r,x)
    return r

# format: ~a/~s/~d/~%/~~ 格式化引擎（Scheme format 子集）
def format(fmt,args):
    parts=[]; i=0; ai=0
    while i<len(fmt):
        if fmt[i]=='~' and i+1<len(fmt):
            c=fmt[i+1]
            if c=='a': parts.append(str(args[ai]) if isinstance(args[ai], (str, SchemeString)) else _pr(args[ai])); ai+=1; i+=2
            elif c=='s': parts.append(_pr(args[ai])); ai+=1; i+=2
            elif c=='d':
                if ai >= len(args): raise SchemeException("format: not enough arguments")
                val = args[ai]
                if isinstance(val, Fraction): val = int(val)
                parts.append(str(int(val))); ai+=1; i+=2
            elif c=='%': parts.append('\n'); i+=2
            elif c=='~': parts.append('~'); i+=2
            else: parts.append(fmt[i]); i+=2
        else: parts.append(fmt[i]); i+=1
    return ''.join(parts)

# compose: 函数组合（从右到左执行）
def compose(fns):
    def comp(x):
        r=x
        for fn in reversed(fns):
            r=call(fn,[r]) if not callable(fn) else fn(r)
        return r
    return comp

# ── 辅助函数（模块级，避免 equal?/eqv? 每次调用重复创建）──


# ── eqv? 与 equal? ──
# eqv? 的数值相等判定：需要类型一致（exact vs inexact），0 的符号检测
#   注意：NaN 比较 (x != x) 和 signed zero 的特殊处理

# equal? 递归比较：支持链表、向量、字节向量、hash-table、字符串、字符
#   使用 seen set 检测循环引用（环形链表不导致无限递归）

# member_py: 通用列表成员查找（使用自定义相等判定 _e）
def member_py(k, lst, _e):
    while isinstance(lst, Cell):
        if _e(lst.car, k) is TRUE: return lst
        lst = lst.cdr
    return FALSE

# next_gensym: 生成唯一的 gensym 符号（自增计数器）
def next_gensym():
    _gensym_ctr[0] += 1
    return Sym(f"g{_gensym_ctr[0]}")

# ── 原生基础库绑定 ──


# +：加法，多参，支持 int/Fraction/float/complex 混合运算
#   如果任一参数是 complex，所有参数转 complex 计算
#   如果任一参数是 Fraction，int 参数先转 Fraction
# -：减法，单参取负，多参连续减
#   complex/Fraction 混合处理同 +
# *：乘法，多参
def mul(*a):
    if not a: return 1
    all_int = True
    for x in a:
        if not isinstance(x, int):
            all_int = False
            break
    if all_int:
        r = 1
        for x in a: r *= x
        return r
    if any(isinstance(x,complex) for x in a):
        r=1
        for x in a: r*=x
        return r
    if any(isinstance(x,Fraction) for x in a):
        r=Fraction(1,1)
        for x in a: r*=Fraction(x,1) if isinstance(x,int) else x
        return r
    r=1
    for x in a: r*=x
    return r
# /：除法——为何 int/int 返回 Fraction？
#   R7RS 要求 exact 除法返回精确结果。1/2 在 Scheme 中应为 1/2 而非 0.5
#   两个 int 不能整除时 (a%x!=0) 自动转为 Fraction
#   float 参数出现后保持 float 路径
def div(a,*b):
    if not b:
        if isinstance(a,complex): return 1/a
        if isinstance(a,Fraction): return Fraction(1,1)/a
        if isinstance(a,int): return Fraction(1,a)
        return 1/a
    has_float = isinstance(a,float)
    for x in b:
        if isinstance(a,complex) or isinstance(x,complex): a/=x; has_float = isinstance(a,float)
        elif isinstance(a,Fraction) or isinstance(x,Fraction):
            a=Fraction(a,1) if isinstance(a,int) else a
            x=Fraction(x,1) if isinstance(x,int) else x
            a/=x
        elif isinstance(a,int) and isinstance(x,int):
            if x == 0: raise SchemeException("division by zero")
            if a%x==0: a//=x
            else: a=Fraction(a,x)
        else: a/=x; has_float = has_float or isinstance(a,float)
    if isinstance(a,Fraction): return a
    return int(a) if isinstance(a,float) and a==int(a) and not has_float else a
# gcd2: gcd(a/b, c/d) = gcd(a,c) / lcm(b,d)
def gcd2(a,b):
    a,b=abs(a),abs(b)
    _gcd = math.gcd
    _lcm = lambda x,y: x * y // _gcd(x, y) if x and y else 0
    if isinstance(a,Fraction) and isinstance(b,Fraction):
        g = lambda: 0
        return Fraction(_gcd(a.numerator,b.numerator), _lcm(a.denominator,b.denominator))
    if isinstance(a,Fraction) or isinstance(b,Fraction):
        na, da = a.numerator, a.denominator if isinstance(a,Fraction) else (int(a),1)
        nb, db = b.numerator, b.denominator if isinstance(b,Fraction) else (int(b),1)
        return Fraction(_gcd(na,nb), _lcm(da,db))
    a,b=int(a),int(b)
    while b: a,b=b,a%b
    return a
def gcd(*a):
    if not a: return 0
    r=0
    for x in a:
        if r==0: r=abs(x)
        else: r=gcd2(r,x)
    return r
# lcm2: lcm(a/b, c/d) = lcm(a,c) / gcd(b,d)
def lcm2(a,b):
    if a==0 or b==0: return 0
    if isinstance(a,Fraction) or isinstance(b,Fraction):
        _gcd = math.gcd
        _lcm = lambda x,y: x * y // _gcd(x, y) if x and y else 0
        a=Fraction(a,1) if isinstance(a,int) else a
        b=Fraction(b,1) if isinstance(b,int) else b
        return Fraction(_lcm(a.numerator,b.numerator), _gcd(a.denominator,b.denominator))
    return abs(int(a)*int(b))//gcd2(a,b)
def lcm(*a):
    if not a: return 1
    r=a[0]
    for x in a[1:]: r=lcm2(r,x)
    return r
def load(path):
    from miniscm import load_file
    return load_file(path)
# map_：标准 map，支持多列表
#   TailCall 陷阱：f_real() 调用可能返回 TailCall（当 f 是编译后的跨函数尾调用时）
#   必须用 _eval_fn 解析 TailCall 后才 cons 到结果 Cell 中
#   递归调用 map_ 处理 cdr（非尾递归，深度受限）
def list_tail(lst,k):
    for _ in range(k):
        if not isinstance(lst,Cell): raise IndexError("list-tail")
        lst=lst.cdr
    return lst
# append: append，逆转+平坦化后重建
def member(k,lst):
    while isinstance(lst,Cell):
        if equal(lst.car,k) is TRUE: return lst
        lst=lst.cdr
    return FALSE

def memv(k,lst):
    while isinstance(lst,Cell):
        if eqv(lst.car, k) is TRUE: return lst
        lst=lst.cdr
    return FALSE


# assoc: 通用关联列表查找，支持自定义比较（第三个参数）
#   默认比较器是 equal；注意 eq 返回 TRUE 或 True 都算匹配
def assoc(k,al,*cmp):
    eq = cmp[0] if cmp else equal
    while isinstance(al,Cell):
        p=al.car
        if isinstance(p,Cell):
            res = eq(p.car,k) if not callable(eq) else eq(p.car, k)
            if res is TRUE or res is True: return p
        al=al.cdr
    return FALSE

def assv(k,al):
    while isinstance(al,Cell):
        p=al.car
        if isinstance(p,Cell) and eqv(p.car,k) is TRUE: return p
        al=al.cdr
    return FALSE

# call/cc: call-with-current-continuation
#   通过 _ContinuationEscape 异常实现控制流逃逸
#   _cont_id 用于匹配逃逸来源，防止外部逃逸被内部捕获
def call_cc(f):
    global _cont_id
    _cont_id+=1; my_id=_cont_id
    captured=[None]
    def_esc = lambda v: captured.__setitem__(0, v) or (_ for _ in ()).throw(_ContinuationEscape(my_id))
    try:
        return f(def_esc) if callable(f) else call(f,[def_esc])
    except _ContinuationEscape as e:
        if e.args[0]!=my_id: raise
        return captured[0]

# cvw: call-with-values
#   生产者 f 返回值检测：
#     - 如果返回 tuple（values 多值），拆解后传给 g
#     - 如果返回 Cell 点对（非 Cell 的 cdr 且非 NIL），作为两个值
#     - 如果返回 Cell 正规二元素列表且至少一个元素是列表，也作为两个值
#       （break/span/partition/split-at 等返回双列表，但单元素如 (list 1 2) 保持单值）
#     - 否则作为单值传给 g
def cvw(f,g):
    r=call(f,[])
    if isinstance(r,tuple):
        if len(r)==0: return call(g,[])
        if len(r)==1: return call(g,[r[0]])
        return call(g,list(r))
    if isinstance(r, Cell) and not isinstance(r.cdr, Cell) and r.cdr is not NIL:
        return call(g,[r.car,r.cdr])
    if isinstance(r, Cell) and isinstance(r.cdr, Cell) and r.cdr.cdr is NIL:
        if isinstance(r.car, Cell) or r.car is NIL or isinstance(r.cdr.car, Cell) or r.cdr.car is NIL:
            return call(g,[r.car, r.cdr.car])
    return call(g,[r])
# dynamic-wind: before/during/after 保证执行
def dynamic_wind(before,during,after):
    before() if callable(before) else call(before,[])
    try:
        r=during() if callable(during) else call(during,[])
        return r
    finally:
        after() if callable(after) else call(after,[])
# do_force: force promise（惰性求值缓存）
def do_force(p):
    if isinstance(p,Promise):
        if not p.forced:
            try:
                p.val=call(p.thunk,[])
            except Exception:
                p.forced=True
                raise
            p.forced=True
        return p.val
    return p
# port_out: 输出端口写入
#   str-port 使用 list 包裹字符串实现引用传递（'str-port', [缓冲串]）
#   file-port 直接写入 .write()

def port_in(port):
    if isinstance(port, tuple):
        if port[0] == 'str-port' and isinstance(port[1], list):
            return port[1][0]
        if port[0] == 'file-port' and len(port) > 3:
            return port[3].read()
    return None

# dsp: display（字符串不引号包裹，其他值使用 _pr 打印）
#   坑：str-port 从列表缓冲取第一个字符并截断，无字符返回 EOF
def rc(p):
    if p is None: p = ('str-port', [sys.stdin.read()])
    if isinstance(p,tuple) and p[0]=='file-port' and len(p)>3:
        c=p[3].read(1)
        return SchemeChar(c) if c else EOF
    if isinstance(p,tuple) and p[0]=='str-port' and isinstance(p[1],list):
        s=p[1][0]
        if not s: return EOF
        c=s[0]; p[1][0]=s[1:]; return SchemeChar(c)
    return EOF
# pkc: peek-char，窥视一个字符但不消耗
#   file-port 通过 seek(-1,1) 回退，str-port 只看不截断
def pkc(p):
    if isinstance(p,tuple) and p[0]=='file-port' and len(p)>3:
        c=p[3].read(1)
        if c:
            try: p[3].seek(p[3].tell()-1)
            except OSError: pass
        return SchemeChar(c) if c else EOF
    if isinstance(p,tuple) and p[0]=='str-port' and isinstance(p[1],list):
        s=p[1][0]
        if not s: return EOF
        return SchemeChar(s[0])
    return EOF
# wc: write-char，写一个字符到端口
def wc(c,p=None):
    ch=c[1] if isinstance(c,tuple) else (c.char if hasattr(c,'char') else str(c))
    if port_out(p, ch): return VOID
    sys.stdout.write(ch); return VOID
def write_proc(x, port=None):
    s=_pr(x)
    if port_out(port, s): return VOID
    sys.stdout.write(s); return VOID
# read_proc: read，从端口读一个 S 表达式
#   str-port 通过 _tokenize + _parse1 解析，保留未消耗部分
def read_proc(port=None):
    if port is None or port is TRUE:
        line=sys.stdin.readline()
        if not line: return EOF
        return read(line)
    if isinstance(port,tuple) and port[0]=='str-port' and isinstance(port[1],list):
        s = port[1][0]; s_stripped = s.lstrip()
        if not s_stripped: return EOF
        skip = len(s) - len(s_stripped)
        from reader import _tokenize, Reader, parse_reader
        toks = _tokenize(s_stripped)
        if not toks: return EOF
        r = Reader(toks)
        expr = parse_reader(r)
        consumed = r.pos
        pos = skip
        for t in toks[:consumed]:
            idx = s.find(t, pos)
            if idx < 0: break
            pos = idx + len(t)
        port[1][0] = s[pos:].lstrip()
        return expr
    if isinstance(port,tuple) and port[0]=='file-port' and len(port)>3:
        line=port[3].readline()
        if not line: return EOF
        return read(line)
    return EOF
# cwif: call-with-input-file（打开文件，调用 thunk，自动关闭）
def cwif(n,f):
    try:
        fp = open(str(n), 'r')
        p = ('file-port', str(n), 'r', fp)
        return f(p) if callable(f) else call(f,[p])
    except: return VOID
# cwof: call-with-output-file
def cwof(n,f):
    try:
        fp = open(str(n), 'w')
        p = ('file-port', str(n), 'w', fp)
        return f(p) if callable(f) else call(f,[p])
    except: return VOID
# call: 通用过程调用
#   TailCall 陷阱：可调用过程 proc 执行后可能返回 TailCall（来自 JIT 编译的跨函数尾调用）
#   必须用 _eval_fn 循环解析直到返回非 TailCall 值
#   lambda 元组（'lambda', params, body, penv, _）直接构造 env 并 eval_seq
#     因为 proc(*all_args) 返回 Python bool 时需要转成 Scheme TRUE/FALSE
def app(fn,*args):
    from miniscm import eval_seq
    all_args = []
    for x in args:
        if isinstance(x,Cell):
            while isinstance(x,Cell): all_args.append(x.car); x=x.cdr
        elif x is not NIL:
            all_args.append(x)
    if callable(fn):
        proc=fn
    elif isinstance(fn,tuple) and fn[0]=='lambda':
        proc=fn
    else:
        proc=be.lookup(fn)
    if isinstance(proc,tuple) and proc[0]=='lambda':
        _,params,body,penv, _ = proc; nenv=Env(penv); pi=0
        for p in params:
            ps=_sn(p)
            if ps.startswith('rest:'): nenv.define(ps[5:], _lst(all_args[pi:])); pi=len(all_args)
            else: nenv.define(ps, all_args[pi]); pi+=1
        r=eval_seq(body,nenv)
    else:
        r = proc(*all_args)
    if isinstance(r, TailCall):
        from miniscm import _eval as _eval_fn
        r = _eval_fn(r.expr, r.env)
    if r is True: return TRUE
    if r is False: return FALSE
    return r
def with_exception_handler(handler,thunk):
    try: return thunk() if callable(thunk) else call(thunk,[])
    except SchemeException as e:
        h=handler if callable(handler) else (lambda x: call(handler,[x]))
        return h(e.val)
    except Exception as e:
        h=handler if callable(handler) else (lambda x: call(handler,[x]))
        return h(SchemeString(str(e)))
def do_raise(x):
    raise SchemeException(x)


# make_coro_gen: 协程生成器（收集 yield 值后顺序返回）
def make_coro_gen(proc):
    vals = []
    call(proc, [lambda v: vals.append(v)])
    i = [0]
    def gen():
        if i[0] >= len(vals): return EOF
        v = vals[i[0]]; i[0] += 1
        return v
    return gen

# id_eq: bound-identifier=? / free-identifier=?
#   坑：只比较 SyntaxObject.expr 或原始对象的 str() 表示
#   如果 sa 和 sb 引用同一个 SyntaxObject（自引用），str(sa) == str(sb) 判定为相等
#   但 R7RS 要求比较 lexical context，此实现不追踪 context，仅按名称匹配
def id_eq(a,b):
    sa = a.expr if isinstance(a,SyntaxObject) else a
    sb = b.expr if isinstance(b,SyntaxObject) else b
    return TRUE if str(sa) == str(sb) else FALSE

# ── SECTION C: 运行时原语批处理绑定 ──
#   以下块通过 for 循环批量注册数学函数、CxR 组合器

import cmath

def math_or_cmath(f, cf):
    def _(x):
        if isinstance(x, complex): return cf(x)
        try: return f(float(x)) if isinstance(x,Fraction) else f(x)
        except ValueError: return cf(float(x))
    return _

# 数学科学计算

# float_result: 包装函数确保返回 float（用于 round/floor/ceil/truncate）
def float_result(f):
    def _(x):
        r = f(float(x)) if isinstance(x,Fraction) else f(x)
        return float(r)
    return _

# preserve_type: 类型保持包装——输入 int 返回 int，输入 float 返回 float
#   坑：Fraction 参数先转 float 计算，结果回落为 int 或 float
#   round/floor/ceil/truncate 用此包装以保持 Scheme 语义
def preserve_type(f):
    def _(x):
        r = f(x) if not isinstance(x,Fraction) else f(x)
        if isinstance(x, Fraction): return Fraction(r, 1) if int(r) == r else r
        if isinstance(x, float) and float('inf') in (x, -x, float('nan')): return x
        if isinstance(x, float): return float(r)
        return int(r)
    return _

# 批量注册：sin/cos/exp/sqrt/log/round/floor/ceiling/truncate/tan/asin/acos/atan/abs/expt
#   sqrt 特殊处理：整数完全平方返回 isqrt（精确），否则用 math_or_cmath
for fn,n in [(math_or_cmath(math.sin, cmath.sin),'sin'),
    (math_or_cmath(math.cos, cmath.cos),'cos'),
    (math_or_cmath(math.exp, cmath.exp),'exp'),
    (lambda x: int(math.isqrt(x)) if isinstance(x,int) and x>=0 and math.isqrt(x)**2==x else (math_or_cmath(math.sqrt, cmath.sqrt)(x)),'sqrt'),
    (math_or_cmath(math.log, cmath.log),'log'),
    (preserve_type(round),'round'),
    (preserve_type(math.floor),'floor'),
    (preserve_type(math.ceil),'ceiling'),
    (preserve_type(math.trunc),'truncate'),
    (math_or_cmath(math.tan, cmath.tan),'tan'),
    (math_or_cmath(math.asin, cmath.asin),'asin'),
    (math_or_cmath(math.acos, cmath.acos),'acos'),
    (math_or_cmath(math.atan, cmath.atan),'atan'),
    (abs,'abs'),(lambda a,b: a**b,'expt')]:
    builtin(n, fn)

# 动态 CxR 提取
#   c*r 组合器字典：通过 car/cdr 链实现 caaaar/cddddr 等 24 种组合
_cxr_map = {
    'caaar': (car,car,car), 'caadr': (car,car,cdr), 'cadar': (car,cdr,car), 'caddr': (car,cdr,cdr),
    'cdaar': (cdr,car,car), 'cdadr': (cdr,car,cdr), 'cddar': (cdr,cdr,car), 'cdddr': (cdr,cdr,cdr),
    'caaaar': (car,car,car,car), 'caaadr': (car,car,car,cdr), 'caadar': (car,car,cdr,car), 'caaddr': (car,car,cdr,cdr),
    'cadaar': (car,cdr,car,car), 'cadadr': (car,cdr,car,cdr), 'caddar': (car,cdr,cdr,car), 'cadddr': (car,cdr,cdr,cdr),
    'cdaaar': (cdr,car,car,car), 'cdaadr': (cdr,car,car,cdr), 'cdadar': (cdr,car,cdr,car), 'cdaddr': (cdr,car,cdr,cdr),
    'cddaar': (cdr,cdr,car,car), 'cddadr': (cdr,cdr,car,cdr), 'cdddar': (cdr,cdr,cdr,car), 'cddddr': (cdr,cdr,cdr,cdr),
}
# mk_cxr: 闭包工厂，按逆序应用 cdr/car 链
for _n,_chain in _cxr_map.items():
    def mk_cxr(ch):
        def cxr(x):
            for c in reversed(ch): x=c(x)
            return x
        return cxr
    builtin(_n, mk_cxr(_chain))

# format_dispatch: format 的分发入口
#   如果第一个参数是 str-port（输出字符串端口），结果写入端口而非返回
def format_dispatch(*a):
    if len(a) >= 2 and isinstance(a[0], tuple) and a[0][0] == 'str-port' and isinstance(a[0][1], list):
        result = format(a[1], list(a[2:]))
        a[0][1][0] = result
        return VOID
    return format(a[0], list(a[1:]))
# simplest_between: 求 (a,b) 区间内的最简分数（用于 rationalize）
#   递归的 Stern-Brocot 树搜索
def simplest_between(a, b):
    if a >= b: return simplest_between(b, a)
    fa = int(math.floor(a))
    fb = int(math.floor(b))
    if fa != fb:
        return Fraction(fb, 1)
    r = simplest_between(Fraction(1, 1) / (Fraction(b) - fa), Fraction(1, 1) / (Fraction(a) - fa))
    return Fraction(fa, 1) + Fraction(1, 1) / r

# string_fill_prim: string-fill! 的底层实现
def string_fill_prim(s, c, *args):
    str_mutate(s)
    start = args[0] if args else 0
    end = args[1] if len(args) > 1 else len(s.data)
    ch = c[1] if isinstance(c, tuple) else (c.char if hasattr(c, 'char') else str(c))
    for i in range(start, end): s.data[i] = ch
    return VOID

# symbol_eq_prim: symbol=? 多参比较（全等判定，is 比较）
def symbol_eq_prim(*args):
    for i in range(len(args) - 1):
        if args[i] is not args[i+1]: return FALSE
    return TRUE

# for-each: 对列表/字符串/字符每个元素执行过程（副作用）
    return VOID
def make_ht():
    return {}
def alist2ht(al):
    r = {}
    while isinstance(al, Cell):
        p = al.car
        if isinstance(p, Cell):
            r[p.car] = p.cdr.car if isinstance(p.cdr, Cell) else p.cdr
        al = al.cdr
    return r

def make_param(init, *rest):
    converter = rest[0] if rest else None
    box = [converter(init) if converter else init]
    def param(*args):
        if not args:
            return box[0]
        if len(args) == 1:
            box[0] = converter(args[0]) if converter else args[0]
            return VOID
        raise SchemeException("make-parameter: too many arguments")
    return param


# ── Imported from primitives_ext ──
def cell_iter(lst):
    while isinstance(lst, Cell):
        yield lst.car
        lst = lst.cdr


def cells(lst):
    return list(cell_iter(lst))


def _stream_next(cur):
    nxt = cur.cdr
    if callable(nxt):
        return nxt()
    if isinstance(nxt, Promise):
        return do_force(nxt)
    return nxt


def stream_ref_fn(s, n):
    cur = s
    for _ in range(n):
        if cur is NIL: return NIL
        if isinstance(cur, Cell):
            cur = _stream_next(cur)
        elif isinstance(cur, tuple) and len(cur) == 2:
            cur = cur[1]
        else:
            return NIL
    if cur is NIL: return NIL
    if isinstance(cur, Cell): return cur.car
    if isinstance(cur, tuple) and len(cur) == 2: return cur[0]
    return NIL


def _stream_advance(v):
    if isinstance(v, Promise):
        return do_force(v)
    return v


def stream_map_fn(f, s):
    cur = s
    def _step():
        nonlocal cur
        if cur is NIL or not isinstance(cur, Cell):
            return NIL
        mapped = f(cur.car)
        nxt = _stream_advance(cur.cdr)
        out = Cell(mapped, Promise(_step))
        cur = nxt
        return out
    return _step()


def stream_filter_fn(pred, s):
    cur = s
    def _step():
        nonlocal cur
        while True:
            if cur is NIL or not isinstance(cur, Cell):
                return NIL
            if pred(cur.car) is TRUE:
                nxt = _stream_advance(cur.cdr)
                out = Cell(cur.car, Promise(_step))
                cur = nxt
                return out
            cur = _stream_advance(cur.cdr)
    return _step()


def stream_take_fn(s, n):
    result = []
    cur = s
    for _ in range(n):
        if not isinstance(cur, Cell): break
        result.append(cur.car)
        cur = _stream_next(cur)
    return _lst(result)


def list_split_at(lst, n):
    first = []
    cur = lst
    for _ in range(n):
        if not isinstance(cur, Cell): break
        first.append(cur.car)
        cur = cur.cdr
    return Cell(_lst(first), Cell(cur, NIL))


def list_span(pred, lst):
    yes = []
    cur = lst
    while isinstance(cur, Cell):
        if pred(cur.car) is TRUE:
            yes.append(cur.car)
            cur = cur.cdr
        else:
            break
    return Cell(_lst(yes), Cell(cur, NIL))


def break_list_fn(pred, lst):
    yes = []
    cur = lst
    while isinstance(cur, Cell):
        if pred(cur.car) is TRUE:
            break
        yes.append(cur.car)
        cur = cur.cdr
    return Cell(_lst(yes), Cell(cur, NIL))


def partition_fn(pred, lst):
    yes, no = [], []
    for x in cell_iter(lst):
        if pred(x) is TRUE: yes.append(x)
        else: no.append(x)
    return Cell(_lst(yes), Cell(_lst(no), NIL))


def booleans_to_integer(*bools):
    r = 0
    bit = 0
    for b in bools:
        if b is TRUE or b is True:
            r |= (1 << bit)
        bit += 1
    return r


def bits_to_integer(lst):
    r = 0
    cur = lst
    while isinstance(cur, Cell):
        v = cur.car
        r = r * 2 + (1 if v is TRUE or v is True or (isinstance(v, Sym) and v.name == '1') or v == 1 else 0)
        cur = cur.cdr
    return r


def bits_to_integer_lsb(lst):
    r = 0
    bit = 0
    cur = lst
    while isinstance(cur, Cell):
        v = cur.car
        if v is TRUE or v is True or (isinstance(v, Sym) and v.name == '1') or v == 1:
            r |= (1 << bit)
        bit += 1
        cur = cur.cdr
    return r


def integer_to_bits_list(n, k=0):
    n = int(n)
    bits = []
    temp = abs(n)
    while temp:
        bits.append(1 if temp & 1 else 0)
        temp >>= 1
    if not bits: bits = [0]
    if k > len(bits):
        bits = bits + [0] * (k - len(bits))
    return _lst(bits)


def alist_copy_fn(al):
    result = NIL
    for p in cell_iter(al):
        if isinstance(p, Cell) and isinstance(p.cdr, Cell) and p.cdr.cdr is NIL:
            result = Cell(Cell(p.car, Cell(p.cdr.car, NIL)), result)
        elif isinstance(p, Cell):
            result = Cell(Cell(p.car, p.cdr), result)
        else:
            result = Cell(p, result)
    # reverse to preserve order
    prev = NIL
    cur = result
    while isinstance(cur, Cell):
        nxt = cur.cdr
        cur.cdr = prev
        prev = cur
        cur = nxt
    return prev


def box(x): return ['box', x]

def is_box(x): return isinstance(x, list) and len(x) == 2 and x[0] == 'box'

def unbox(b): return b[1] if is_box(b) else (b[1] if isinstance(b, tuple) else b)

def do_set_box(b, x):
    if is_box(b): b[1] = x; return VOID
    raise TypeError("not a box")


def set_port_pos(p, pos):
    pos = int(pos)
    if not hasattr(set_port_pos, '_saved_str'):
        set_port_pos._saved_str = {}
    if isinstance(p, tuple) and p[0] == 'str-port' and isinstance(p[1], list):
        s = p[1][0]
        key = id(p)
        if key not in set_port_pos._saved_str:
            set_port_pos._saved_str[key] = s
        p[1][0] = set_port_pos._saved_str[key][pos:] if 0 <= pos < len(set_port_pos._saved_str[key]) else ('' if pos >= len(set_port_pos._saved_str[key]) else set_port_pos._saved_str[key])
    if isinstance(p, tuple) and p[0] == 'file-port' and len(p) > 3:
        p[3].seek(pos)
    return VOID


def hash_table_ref_default(ht, key, default):
    if isinstance(ht, dict):
        return ht.get(key, default)
    if hasattr(ht, 'data'):
        return ht.data.get(key, default)
    cur = ht
    while isinstance(cur, Cell):
        if cur.car is key or (hasattr(cur.car, '__eq__') and cur.car == key):
            return cur.cdr.car if isinstance(cur.cdr, Cell) else cur.cdr
        cur = cur.cdr
    return default


def hash_table_keys(ht):
    d = ht if isinstance(ht, dict) else ht.data
    return _lst(list(d.keys()))


def hash_table_values(ht):
    d = ht if isinstance(ht, dict) else ht.data
    return _lst(list(d.values()))


def compose_fn(*fns):
    if not fns: return lambda x: x
    def comp(*args):
        r = fns[-1](*args)
        for f in reversed(fns[:-1]):
            r = f(r)
        return r
    return comp


def list_drop(lst, n):
    cur = lst
    for _ in range(n):
        if not isinstance(cur, Cell): break
        cur = cur.cdr
    return cur


# pair-fold/pair-fold-right
def pair_fold_fn(f, init, plist):
    acc = init
    while plist is not NIL:
        acc = f(plist, acc)
        plist = plist.cdr
    return acc
def pair_fold_right_fn(f, init, plist):
    pairs = []
    cur = plist
    while cur is not NIL:
        pairs.append(cur)
        cur = cur.cdr
    acc = init
    for p in reversed(pairs):
        acc = f(p, acc)
    return acc

def do_quotient(n, d):
    if not d: raise SchemeException("division by zero")
    return n//d if (n>=0)==(d>=0) else -((-n)//d)

def trunc_rem(n, d):
    if not d: raise SchemeException("division by zero")
    r = n % d
    if r != 0 and (n >= 0) != (d >= 0):
        r -= d
    return r

def do_modulo(n, d):
    if not d: raise SchemeException("division by zero")
    return n - (n//d)*d

def string_ref_prim(s, *a):
    if not a: raise SchemeException("string-ref: wrong number of arguments")
    if isinstance(s, SchemeString): return SchemeChar(str(s)[a[0]])
    return SchemeChar(str(s)[a[0]])


from mtypes import be as _be
