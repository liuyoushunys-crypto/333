# primitives.py
import math, sys, cmath
from fractions import Fraction

from mtypes import (
    Sym, Cell, SchemeString, SchemeChar, SchemeVector, SchemeBytevector,
    Promise, SyntaxObject, SchemeException, ErrorObject, TailCall, NIL, VOID, EOF, TRUE, FALSE, Env, SYM_QUOTE,_UNBOUND,
    _pr, _sn, _plist, _lst, _ContinuationEscape, _cont_id, _gensym_ctr, builtin, be
)
from reader import read

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

# isnum: 判断是否为 Scheme 数值类型（int/float/complex/Fraction）
def isnum(x): return isinstance(x,(int,float,complex,Fraction)) and type(x) is not bool

# num: 将数值统一转为 Fraction（Scheme 精确数的内部表示）
#   注意：float 和 complex 原样返回，不做强制转换
def num(x):
    if type(x) is bool: raise TypeError(f"not a number: {x}")
    if isinstance(x,Fraction): return x
    if isinstance(x,int): return Fraction(x,1)
    if isinstance(x,float): return x
    if isinstance(x,complex): return x
    raise TypeError(f"not a number: {x}")

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
def is_list(x):
    seen=set()
    while isinstance(x,Cell):
        if id(x) in seen: return FALSE
        seen.add(id(x))
        x=x.cdr
    return TRUE if x is NIL else FALSE

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
def is_scheme_char(x):
    return isinstance(x, SchemeChar) or (isinstance(x, tuple) and len(x) == 2 and x[0] == 'char')
def get_scheme_char(x):
    return x.char if isinstance(x, SchemeChar) else x[1]
def is_scheme_str(x):
    return isinstance(x, (str, SchemeString))
def get_scheme_str(x):
    return ''.join(x.data) if isinstance(x, SchemeString) else x
def is_scheme_vec(x):
    return isinstance(x, (list, SchemeVector))
def get_scheme_vec_data(x):
    return x.data if isinstance(x, SchemeVector) else x

class _EqHashTable:
    __slots__ = ('data',)
    def __init__(self): self.data = {}

# ── eqv? 与 equal? ──
# eqv? 的数值相等判定：需要类型一致（exact vs inexact），0 的符号检测
#   注意：NaN 比较 (x != x) 和 signed zero 的特殊处理
def eqv(a, b):
    if a is b: return TRUE
    is_num_a = isinstance(a, (int, float, complex, Fraction)) and type(a) is not bool
    is_num_b = isinstance(b, (int, float, complex, Fraction)) and type(b) is not bool
    if is_num_a and is_num_b:
        exact_a = isinstance(a, (int, Fraction))
        exact_b = isinstance(b, (int, Fraction))
        if exact_a != exact_b: return FALSE
        if isinstance(a, float) and isinstance(b, float):
            if a != a and b != b: return TRUE
        if a == 0 and b == 0:
            ar = a.real if isinstance(a, complex) else a
            br = b.real if isinstance(b, complex) else b
            if isinstance(ar, float) and isinstance(br, float):
                if math.copysign(1.0, ar) != math.copysign(1.0, br): return FALSE
        try: return TRUE if a == b else FALSE
        except: return FALSE
    if is_scheme_char(a) and is_scheme_char(b): return TRUE if get_scheme_char(a) == get_scheme_char(b) else FALSE
    return FALSE

# equal? 递归比较：支持链表、向量、字节向量、hash-table、字符串、字符
#   使用 seen set 检测循环引用（环形链表不导致无限递归）
def equal(a, b, seen=None):
    if eqv(a, b) is TRUE: return TRUE
    if is_scheme_char(a) and is_scheme_char(b): return TRUE if get_scheme_char(a) == get_scheme_char(b) else FALSE
    if is_scheme_str(a) and is_scheme_str(b): return TRUE if get_scheme_str(a) == get_scheme_str(b) else FALSE
    if seen is None: seen = set()
    pair_id = (id(a), id(b))
    if pair_id in seen: return TRUE
    seen.add(pair_id)
    if isinstance(a, Cell) and isinstance(b, Cell):
        if equal(a.car, b.car, seen) is TRUE: return equal(a.cdr, b.cdr, seen)
        return FALSE
    if is_scheme_vec(a) and is_scheme_vec(b):
        da = get_scheme_vec_data(a); db = get_scheme_vec_data(b)
        if len(da) != len(db): return FALSE
        for x, y in zip(da, db):
            if equal(x, y, seen) is FALSE: return FALSE
        return TRUE
    if isinstance(a, SchemeBytevector) and isinstance(b, SchemeBytevector):
         return TRUE if a.data == b.data else FALSE
    if isinstance(a, _EqHashTable) and isinstance(b, _EqHashTable):
        if len(a.data) != len(b.data): return FALSE
        for k, v in a.data.items():
            if k not in b.data: return FALSE
            if equal(v, b.data[k], seen) is FALSE: return FALSE
        return TRUE
    # Handle plain Python dicts (from make-strong-hash-table etc.)
    if (isinstance(a, dict) or isinstance(a, _EqHashTable)) and (isinstance(b, dict) or isinstance(b, _EqHashTable)):
        if len(a) != len(b): return FALSE
        for k, v in a.items():
            if k not in b: return FALSE
            if equal(v, b[k], seen) is FALSE: return FALSE
        return TRUE
    return FALSE

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
def cons(a,d): return Cell(a,d)

def car(p):
    if isinstance(p,Cell): return p.car
    raise TypeError("car: not a pair")

def cdr(p):
    if isinstance(p,Cell): return p.cdr
    raise TypeError("cdr: not a pair")
def lst(*a):
    r=NIL
    for x in reversed(a): r=Cell(x,r)
    return r
# +：加法，多参，支持 int/Fraction/float/complex 混合运算
#   如果任一参数是 complex，所有参数转 complex 计算
#   如果任一参数是 Fraction，int 参数先转 Fraction
def add(*a):
    if not a: return 0
    all_int = True
    for x in a:
        if not isinstance(x, int):
            all_int = False
            break
    if all_int:
        r = 0
        for x in a: r += x
        return r
    if any(isinstance(x,complex) for x in a):
        return sum(complex(x) if not isinstance(x,complex) else x for x in a)
    if any(isinstance(x,Fraction) for x in a):
        return sum((Fraction(x,1) if isinstance(x,int) else x) for x in a)
    return sum(a)
# -：减法，单参取负，多参连续减
#   complex/Fraction 混合处理同 +
def sub(*a):
    if not a: raise SchemeException("-\: wrong number of arguments")
    if len(a)==1: return -a[0] if isnum(a[0]) else -num(a[0])
    all_int = True
    for x in a:
        if not isinstance(x, int):
            all_int = False
            break
    if all_int:
        r = a[0]
        for x in a[1:]: r -= x
        return r
    if any(isinstance(x,complex) for x in a):
        ca=a[0] if isinstance(a[0],complex) else complex(float(a[0].real if isinstance(a[0],Fraction) else a[0]),0)
        for x in a[1:]: ca-=x if isinstance(x,complex) else complex(float(x.real if isinstance(x,Fraction) else x),0)
        return ca
    if any(isinstance(x,Fraction) for x in a):
        r=Fraction(a[0],1) if isinstance(a[0],int) else a[0]
        for x in a[1:]: r-=Fraction(x,1) if isinstance(x,int) else x
        return r
    return a[0]-sum(a[1:])
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
    if isinstance(a,Fraction) and isinstance(b,Fraction):
        g = lambda: 0
        _gcd = math.gcd
        _lcm = lambda x,y: x * y // _gcd(x, y) if x and y else 0
        return Fraction(_gcd(a.numerator,b.numerator), _lcm(a.denominator,b.denominator))
    if isinstance(a,Fraction) or isinstance(b,Fraction):
        _gcd = math.gcd
        _lcm = lambda x,y: x * y // _gcd(x, y) if x and y else 0
        na, da = a.numerator, a.denominator if isinstance(a,Fraction) else (int(a),1)
        nb, db = b.numerator, b.denominator if isinstance(b,Fraction) else (int(b),1)
        return Fraction(_igcd(na,nb), _ilcm(da,db))
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
        return Fraction(_ilcm(a.numerator,b.numerator), _igcd(a.denominator,b.denominator))
    return abs(int(a)*int(b))//gcd2(a,b)
def lcm(*a):
    if not a: return 1
    r=1
    for x in a: r=lcm2(r,x)
    return r
def load(path):
    from miniscm import load_file
    return load_file(path)
# map_：标准 map，支持多列表
#   TailCall 陷阱：f_real() 调用可能返回 TailCall（当 f 是编译后的跨函数尾调用时）
#   必须用 _eval_fn 解析 TailCall 后才 cons 到结果 Cell 中
#   递归调用 map_ 处理 cdr（非尾递归，深度受限）
def map_(f,*lsts):
    from mtypes import Cell, NIL, TailCall
    from miniscm import _eval as _eval_fn
    f_real=f if callable(f) else lambda *a: call(f,list(a))
    result = NIL
    tail = None
    while True:
        heads=[l for l in lsts if isinstance(l,Cell)]
        if len(heads) < len(lsts) or not heads:
            if tail is None:
                # Reverse result
                prev = NIL
                cur = result
                while isinstance(cur, Cell):
                    nxt = cur.cdr
                    cur.cdr = prev
                    prev = cur
                    cur = nxt
                return prev
            return result
        r=f_real(*(l.car for l in heads))
        while isinstance(r, TailCall):
            r = _eval_fn(r.expr, r.env)
        result = Cell(r, result)
        lsts = tuple(h.cdr for h in heads)
def list_ref(lst,k):
    for _ in range(k):
        if not isinstance(lst,Cell): raise IndexError("list-ref")
        lst=lst.cdr
    if not isinstance(lst, Cell): raise IndexError("list-ref")
    return lst.car
def list_tail(lst,k):
    for _ in range(k):
        if not isinstance(lst,Cell): raise IndexError("list-tail")
        lst=lst.cdr
    return lst
# append: append，逆转+平坦化后重建
def append(*ls):
    if not ls: return NIL
    r = NIL
    last = ls[-1]
    if isinstance(last, Cell):
        for l in reversed(ls):
            if isinstance(l, Cell):
                rev = NIL; cur = l
                while isinstance(cur, Cell): rev = Cell(cur.car, rev); cur = cur.cdr
                cur = rev
                while isinstance(cur, Cell): r = Cell(cur.car, r); cur = cur.cdr
            elif l is not NIL: r = Cell(l, r)
    else:
        r = last
        for l in reversed(ls[:-1]):
            if isinstance(l, Cell):
                rev = NIL; cur = l
                while isinstance(cur, Cell): rev = Cell(cur.car, rev); cur = cur.cdr
                cur = rev
                while isinstance(cur, Cell): r = Cell(cur.car, r); cur = cur.cdr
            elif l is not NIL: r = Cell(l, r)
    return r
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

def memq(k,lst):
    while isinstance(lst,Cell):
        if lst.car is k: return lst
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

def assq(k,al):
    while isinstance(al,Cell):
        p=al.car
        if isinstance(p,Cell) and p.car is k: return p
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
def port_out(port, s):
    if isinstance(port, tuple):
        if port[0] == 'str-port' and isinstance(port[1], list):
            port[1][0] += s; return True
        if port[0] == 'file-port' and len(port) > 3:
            port[3].write(s); port[3].flush(); return True
    return False

def port_in(port):
    if isinstance(port, tuple):
        if port[0] == 'str-port' and isinstance(port[1], list):
            return port[1][0]
        if port[0] == 'file-port' and len(port) > 3:
            return port[3].read()
    return None

# dsp: display（字符串不引号包裹，其他值使用 _pr 打印）
def dsp(x, port=None):
    s=str(x) if isinstance(x,(str,SchemeString)) else _pr(x)
    if not port_out(port, s): sys.stdout.write(s); return VOID
    return VOID
# rc: read-char，从端口读一个字符
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
        from reader import _tokenize, _parse1
        toks = _tokenize(s_stripped)
        if not toks: return EOF
        expr, rem = _parse1(toks)
        pos = skip
        for t in toks[:len(toks)-len(rem)]:
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
def call(proc,args):
    from miniscm import eval_seq, _eval as _eval_fn
    from mtypes import TailCall
    if callable(proc):
        r = proc(*args)
        while isinstance(r, TailCall):
            r = _eval_fn(r.expr, r.env)
        return r
    if isinstance(proc,tuple) and proc[0]=='lambda':
        _,params,body,penv, _ = proc; nenv=Env(penv); pi=0
        for p in params:
            ps=_sn(p)
            if ps.startswith('rest:'):
                nenv.define(ps[5:], _lst(args[pi:]))
                pi=len(args)
            else:
                nenv.define(ps, args[pi]); pi+=1
        return eval_seq(body,nenv)
    raise TypeError(f"not callable: {proc}")
# app: apply
#   Cell 展开为平坦参数列表（list** 的 Scheme 版本）
#   先展开 args 中的 Cell 为 all_args 平坦列表
#   然后对 fn 分派：callable / lambda 元组 / be.lookup 符号
#   为何检查 r is True/r is False？
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

def error(*a):
    msg=str(a[0]) if a else ""
    irr=_lst(a[1:]) if len(a)>1 else NIL
    raise SchemeException(ErrorObject(msg, irr))

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
def for_each_fn(f, *lsts):
    if not lsts: return VOID
    xs = lsts[0]
    if isinstance(xs, (str, SchemeString)):
        iters = [str(x) for x in lsts]
        for items in zip(*iters):
            call(f, [SchemeChar(c) for c in items])
    else:
        iters = [_plist(x) for x in lsts]
        for items in zip(*iters):
            call(f, list(items))
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


def stream_map_fn(f, s):
    def gen():
        cur = s
        while isinstance(cur, Cell):
            yield f(cur.car)
            cur = _stream_next(cur)
    it = gen()
    try:
        first = next(it)
    except StopIteration:
        return NIL
    result = Cell(first, NIL)
    cur = result
    for val in it:
        cur.cdr = Cell(val, NIL)
        cur = cur.cdr
    return result


def stream_filter_fn(pred, s):
    def gen():
        cur = s
        while isinstance(cur, Cell):
            if pred(cur.car) is TRUE: yield cur.car
            cur = _stream_next(cur)
    it = gen()
    try:
        first = next(it)
    except StopIteration:
        return NIL
    result = Cell(first, NIL)
    cur = result
    for val in it:
        cur.cdr = Cell(val, NIL)
        cur = cur.cdr
    return result


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

# eval: (eval expr env) — 求值表达式于指定环境
def _eval_bridge(expr, env=None):
    from miniscm import _eval as _eval_fn
    env = env if isinstance(env, Env) else be
    return _eval_fn(expr, env)

# sx-defined?: 检查名称在环境中是否有绑定 (C#: env.LookupSilent(name) is not null)
def _sx_defined(name, env=None):
    env = env if isinstance(env, Env) else _be
    nm = name.name if hasattr(name, 'name') else str(name)
    return TRUE if env.lookup_silent(nm, _UNBOUND) is not _UNBOUND else FALSE

# sx-defmacro: 注册宏元组 ('macro', pattern, body, env, is_simple)
# pattern 是 rest 符号 (如 'args) — 展开时绑定全部实参 (与 C# 兼容)
# env 是 my-definemacro 传入的 (sx-expand-env) 宏定义点词法环境。
# 宏注册到全局, defEnv 字段记录词法定义点环境 (顶层→全局, let-syntax→局部)。
def _sx_defmacro(name, pattern, body, env=None):
    env = env if isinstance(env, Env) else _be
    nm = name.name if hasattr(name, 'name') else str(name)
    _be.data[nm] = ('macro', pattern, body, env, True)
    return name

# sx-expand-call: 单次宏展开。若 (car expr) 是宏元组则展开, 否则返回 FALSE。
def _sx_expand_call(expr, env=None):
    env = env if isinstance(env, Env) else _be
    if isinstance(expr, Cell) and isinstance(expr.car, Sym):
        proc = env.lookup_silent(expr.car.name, _UNBOUND)
        if proc is not _UNBOUND:
            expanded = expand_macro(proc, expr.cdr, env)
            if expanded is not None:
                return expanded
    return FALSE

# ── 宏展开动态环境 (与 C# Evaluator.CurrentMacroDefEnv/CurrentExpandEnv 对应) ──
# sx-def-env: 当前宏的定义环境 (free template identifiers 在此解析)
# sx-expand-env: 当前宏调用点环境 (模板求值时模式替换的局部符号正确解析)
_CURRENT_MACRO_DEF_ENV = None
_CURRENT_EXPAND_ENV = None

# ── ExpandMacro: 单次宏展开 (C# Evaluator.ExpandMacro 的 Python 等价) ──
# proc 为 ("macro", pattern, body, defEnv, true) 元组时:
#   1. 绑定 pattern rest 符号 → args 于新环境 (父为调用点 env)
#   2. 动态设置 CurrentMacroDefEnv / CurrentExpandEnv
#   3. 求值宏体 (EvalSeq), 解包 TailCall
#   4. ResolveHygieneMarkers 解析 (sx-hygiene name) 标记
# 非 "macro" 元组返回 None (未展开)。
def expand_macro(proc, args, env):
    global _CURRENT_MACRO_DEF_ENV, _CURRENT_EXPAND_ENV
    from miniscm import eval_seq,_eval
    if not (isinstance(proc, tuple) and len(proc) >= 5 and proc[0] == 'macro'):
        return None
    if not isinstance(proc[3], Env):
        return None
    defEnv = proc[3]
    mbody = proc[2]

    nenv = Env(env)
    if isinstance(proc[1], Sym):
        nenv.data[proc[1].name] = args if args is not None else NIL

    savedDefEnv = _CURRENT_MACRO_DEF_ENV
    _CURRENT_MACRO_DEF_ENV = defEnv
    _CURRENT_EXPAND_ENV = env
    try:
        r = eval_seq(mbody, nenv)
        while isinstance(r, TailCall):
            r = _eval(r.expr, r.env)
    finally:
        # _CURRENT_EXPAND_ENV 不在此恢复: 宏展开结果 (如 my-definemacro 调用)
        # 在展开后求值, 需通过 (sx-expand-env) 读到宏定义点词法环境。
        _CURRENT_MACRO_DEF_ENV = savedDefEnv

    result = r.expr if isinstance(r, SyntaxObject) else r
    return resolve_hygiene_markers(result, defEnv)

# ── ResolveHygieneMarkers: 解析 (sx-hygiene name) 标记 (C# 等价) ──
# 宏模板中自由标识符经 sx-expand-sym 标记为 (sx-hygiene name),
# 需在宏定义环境 defEnv 中解析。数据值内联为 quote 字面量;
# 过程/宏保留为名字。非标记子表达式原样返回。
def resolve_hygiene_markers(expr, defEnv):
    while isinstance(expr, SyntaxObject):
        expr = expr.expr
    if isinstance(expr, Cell):
        c = expr
        if isinstance(c.car, Sym) and c.car.name == 'sx-hygiene':
            name = None
            if isinstance(c.cdr, Cell) and c.cdr.cdr is NIL and isinstance(c.cdr.car, Sym):
                name = c.cdr.car.name
            if name is not None:
                v = defEnv.data.get(name)
                if v is not None:
                    if isinstance(v, tuple) and len(v) >= 2 and v[0] == 'macro':
                        return c.cdr.car
                    if callable(v):
                        return c.cdr.car
                    return Cell(SYM_QUOTE, Cell(v, NIL))
            return c.cdr.car
        new_car = resolve_hygiene_markers(c.car, defEnv)
        new_cdr = resolve_hygiene_markers(c.cdr, defEnv)
        if new_car is c.car and new_cdr is c.cdr:
            return c
        return Cell(new_car, new_cdr)
    return expr
