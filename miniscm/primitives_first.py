# primitives_first.py — 宏系统自举核心 builtin 的辅助函数独立副本
# 自包含：仅依赖 mtypes.py；对 miniscm 求值器的访问使用函数体内惰性导入
import math, sys
from fractions import Fraction

from mtypes import (
    Sym, Cell, SchemeString, SchemeChar, SchemeVector, SchemeBytevector,SyntaxObject,SYM_QUOTE,
    ErrorObject, NIL, VOID, TRUE, FALSE, Env,
    _pr, _sn, _plist, _lst, _cells, _cell_len, _UNBOUND,be
)
from mtypes import SchemeException, TailCall

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

# ── 类型辅助（eqv?/equal? 依赖）──
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
    if (isinstance(a, dict) or isinstance(a, _EqHashTable)) and (isinstance(b, dict) or isinstance(b, _EqHashTable)):
        if len(a) != len(b): return FALSE
        for k, v in a.items():
            if k not in b: return FALSE
            if equal(v, b[k], seen) is FALSE: return FALSE
        return TRUE
    return FALSE

# ── 对与列表 ──
def cons(a,d): return Cell(a,d)

def car(p):
    if isinstance(p,Cell): return p.car
    raise TypeError("car: not a pair")

def cdr(p):
    if isinstance(p,Cell): return p.cdr
    raise TypeError("cdr: not a pair")

def caar(x): return x.car.car
def cadr(x): return x.cdr.car
def cdar(x): return x.car.cdr
def cddr(x): return x.cdr.cdr

def lst(*a):
    r=NIL
    for x in reversed(a): r=Cell(x,r)
    return r

# ── 列表检测 ──
def is_list(x):
    seen=set()
    while isinstance(x,Cell):
        if id(x) in seen: return FALSE
        seen.add(id(x))
        x=x.cdr
    return TRUE if x is NIL else FALSE

def list_ref(lst,k):
    for _ in range(k):
        if not isinstance(lst,Cell): raise IndexError("list-ref")
        lst=lst.cdr
    if not isinstance(lst, Cell): raise IndexError("list-ref")
    return lst.car

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

def memq(k,lst):
    while isinstance(lst,Cell):
        if lst.car is k: return lst
        lst=lst.cdr
    return FALSE

def assq(k,al):
    while isinstance(al,Cell):
        p=al.car
        if isinstance(p,Cell) and p.car is k: return p
        al=al.cdr
    return FALSE

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

def filter_(pred, lst):
    return _lst([x for x in _cells(lst) if pred(x) is not FALSE])

# ── 数值运算 ──
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

def sub(*a):
    if not a: raise SchemeException("-: wrong number of arguments")
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

def eq_num(*a):
    return FALSE if any(
        (type(a[i]) is bool) != (type(a[i+1]) is bool)
        or (not isinstance(a[i], (int,float,complex,Fraction))
            and not isinstance(a[i+1], (int,float,complex,Fraction))
            and type(a[i]) is not type(a[i+1]))
        or a[i] != a[i+1]
        for i in range(len(a)-1)) else TRUE

def lt(*a):
    return FALSE if any(isinstance(x,complex) for x in a) or any(a[i]>=a[i+1] for i in range(len(a)-1)) else TRUE
def gt(*a):
    return FALSE if any(isinstance(x,complex) for x in a) or any(a[i]<=a[i+1] for i in range(len(a)-1)) else TRUE
def le(*a):
    return FALSE if any(isinstance(x,complex) for x in a) or any(a[i]>a[i+1] for i in range(len(a)-1)) else TRUE
def ge(*a):
    return FALSE if any(isinstance(x,complex) for x in a) or any(a[i]<a[i+1] for i in range(len(a)-1)) else TRUE

# ── 通用过程调用（TailCall 解析经惰性导入 miniscm）──
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

# ── 副作用 ──
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

def error(*a):
    msg=str(a[0]) if a else ""
    irr=_lst(a[1:]) if len(a)>1 else NIL
    raise SchemeException(ErrorObject(msg, irr))

def port_out(port, s):
    if isinstance(port, tuple):
        if port[0] == 'str-port' and isinstance(port[1], list):
            port[1][0] += s; return True
        if port[0] == 'file-port' and len(port) > 3:
            port[3].write(s); port[3].flush(); return True
    return False

def dsp(x, port=None):
    s=str(x) if isinstance(x,(str,SchemeString)) else _pr(x)
    if not port_out(port, s): sys.stdout.write(s); return VOID
    return VOID

# ── 宏系统桥接 ──
_CURRENT_MACRO_DEF_ENV = None
_CURRENT_EXPAND_ENV = None

def _eval_bridge(expr, env=None):
    from miniscm import _eval as _eval_fn
    env = env if isinstance(env, Env) else be
    return _eval_fn(expr, env)

def _sx_defined(name, env=None):
    env = env if isinstance(env, Env) else be
    nm = name.name if hasattr(name, 'name') else str(name)
    return TRUE if env.lookup_silent(nm, _UNBOUND) is not _UNBOUND else FALSE

def _sx_defmacro(name, pattern, body, env=None):
    env = env if isinstance(env, Env) else be
    nm = name.name if hasattr(name, 'name') else str(name)
    be.data[nm] = ('macro', pattern, body, env, True)
    return name

def _sx_expand_call(expr, env=None):
    env = env if isinstance(env, Env) else be
    if isinstance(expr, Cell) and isinstance(expr.car, Sym):
        proc = env.lookup_silent(expr.car.name, _UNBOUND)
        if proc is not _UNBOUND:
            expanded = expand_macro(proc, expr.cdr, env)
            if expanded is not None:
                return expanded
    return FALSE

# mbody 编译缓存: {id(macro_tuple): compiled}
# 第一优先级: 原生 syntax-rules 编译器 (native_syntax.py) — 展开时零解释器。
# 第二优先级: mbody 编译成带 args 参数的 LambdaProc。
# 失败都回退解释器。调用前设置 _CURRENT_MACRO_DEF_ENV/_CURRENT_EXPAND_ENV 等价。
_MBODY_COMPILE_CACHE = {}

def _extract_syntax_rules(proc):
    """从宏元组提取 (lits, rules)。结构必须是 sx-make-macro-binding 生成的
    ((sx-macro-expand 'args '((sx-dispatch args 'lits 'rules))) args (sx-expand-env))。
    返回 (lits, rules) 或 None(非 syntax-rules 结构)。"""
    try:
        mbody = proc[2]
        form = mbody.car
        if not (isinstance(form, Cell) and isinstance(form.car, Sym)
                and form.car.name == 'sx-macro-expand'):
            return None
        body_list = form.cdr.cdr.car.cdr.car
        dispatch = body_list.car
        if not (isinstance(dispatch, Cell) and isinstance(dispatch.car, Sym)
                and dispatch.car.name == 'sx-dispatch'):
            return None
        lits = dispatch.cdr.cdr.car.cdr.car
        rules = dispatch.cdr.cdr.cdr.car.cdr.car
        return (lits, rules)
    except Exception:
        return None

def _compile_mbody(proc):
    """编译宏体。优先原生 syntax-rules 编译器, 其次 LambdaProc 编译。
    返回编译对象(原生 callable 或 LambdaProc)或 None → 解释器回退。
    原生 callable 带 __native_syntax__ 标记。
    """
    if not (isinstance(proc, tuple) and len(proc) >= 5 and proc[0] == 'macro'):
        return None
    defEnv = proc[3]
    if not isinstance(defEnv, Env):
        return None
    # 1) 原生 syntax-rules 编译器
    try:
        from native_syntax import compile_syntax_rules
        sr = _extract_syntax_rules(proc)
        if sr is not None:
            lits, rules = sr
            native = compile_syntax_rules(lits, rules, defEnv)
            if native is not None:
                native.__native_syntax__ = True
                return native
    except Exception:
        pass
    # 2) mbody LambdaProc 编译
    from compiler import LambdaProc, compile_lambda_proc
    mbody = proc[2]
    lp = LambdaProc(['args'], mbody, defEnv, True, name='__macro_mbody__')
    try:
        cv = compile_lambda_proc(lp)
    except Exception:
        cv = None
    if cv is None:
        return None
    lp.compiled_version = cv
    return lp

def _expand_macro_compiled(compiled_lp, args, env, defEnv):
    """用编译版宏体展开。调用前设置宏全局状态(与解释器路径等价)。
    返回展开结果或 None(需回退解释器)。原生 callable 直接调用(无 TailCall)。"""
    global _CURRENT_MACRO_DEF_ENV, _CURRENT_EXPAND_ENV
    savedDefEnv = _CURRENT_MACRO_DEF_ENV
    _CURRENT_MACRO_DEF_ENV = defEnv
    _CURRENT_EXPAND_ENV = env
    try:
        if getattr(compiled_lp, '__native_syntax__', False):
            return compiled_lp(args if args is not None else NIL)
        from compiler import __mscm_eval_tail_call__
        from mtypes import TailCall, NIL as _NIL
        args_val = args if args is not None else _NIL
        r = compiled_lp(args_val)
        while isinstance(r, TailCall):
            r = __mscm_eval_tail_call__(r)
        if isinstance(r, SyntaxObject):
            r = r.expr
        return r
    except Exception:
        return None
    finally:
        _CURRENT_MACRO_DEF_ENV = savedDefEnv

def expand_macro(proc, args, env):
    global _CURRENT_MACRO_DEF_ENV, _CURRENT_EXPAND_ENV
    from miniscm import eval_seq, _eval
    if not (isinstance(proc, tuple) and len(proc) >= 5 and proc[0] == 'macro'):
        return None
    if not isinstance(proc[3], Env):
        return None
    defEnv = proc[3]
    mbody = proc[2]

    # 编译缓存路径: 宏体已编译则直接调用, 失败自动回退解释器
    key = id(proc)
    try:
        if key not in _MBODY_COMPILE_CACHE:
            _MBODY_COMPILE_CACHE[key] = _compile_mbody(proc)
        compiled_lp = _MBODY_COMPILE_CACHE[key]
        if compiled_lp is not None:
            result = _expand_macro_compiled(compiled_lp, args, env, defEnv)
            if result is not None:
                return resolve_hygiene_markers(result, defEnv)
    except Exception:
        pass

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
        _CURRENT_MACRO_DEF_ENV = savedDefEnv

    result = r.expr if isinstance(r, SyntaxObject) else r
    return resolve_hygiene_markers(result, defEnv)

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

def _sx_def_env():
    global _CURRENT_MACRO_DEF_ENV
    from mtypes import be
    return _CURRENT_MACRO_DEF_ENV or be

def _sx_expand_env():
    global _CURRENT_EXPAND_ENV
    from mtypes import be
    return _CURRENT_EXPAND_ENV or be
