# Unified Scheme primitive implementations.
import ast, base64, cmath, functools, io, json, math, os, pathlib, queue, random, re, sys, threading, time
_base64 = base64
_functools = functools
_json = json
_os = os
_random = random
_re = re
_time = time
from fractions import Fraction
from functools import cmp_to_key
from mtypes import (
    Sym, Cell, SchemeString, SchemeChar, SchemeVector, SchemeBytevector,
    Promise, SyntaxObject, SchemeException, ErrorObject, TailCall, Env,
    NIL, VOID, EOF, TRUE, FALSE, SYM_QUOTE, _UNBOUND, _pr, _sn, _plist,
    _lst, _cells, _cell_len, _so, _ContinuationEscape, _cont_id, _gensym_ctr,
    builtin, be
)
from reader import read, parse_number_scheme
from minref import (
    sx_macro_expand, qq_walk, sx_expand, sx_get_bindings, sx_gen_temps,
    sx_syntax_case, sx_with_syntax, sx_let_syntax, sx_make_macro_binding,
    qs_expand, sx_dispatch, sx_def_env, _sx_mutated_vars
)


# ---- primitives_first.py ----
# primitives_first.py — 宏系统自举核心 builtin 的辅助函数独立副本
# 自包含：仅依赖 mtypes.py；对 miniscm 求值器的访问使用函数体内惰性导入


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
    if not a: return 0
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

# mbody 编译缓存: {macro_tuple: compiled}; 保留宏对象，避免 id 重用命中旧宏。
# 第一优先级: 原生 syntax-rules 编译器 (native_syntax.py) — 展开时零解释器。
# 第二优先级: mbody 编译成带 args 参数的 LambdaProc。
# 失败都回退解释器。调用前设置 _CURRENT_MACRO_DEF_ENV/_CURRENT_EXPAND_ENV 等价。
_MBODY_COMPILE_CACHE = {}

def clear_macro_caches():
    """Drop compiled macro bodies after a Scheme library is reloaded."""
    _MBODY_COMPILE_CACHE.clear()

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
        from compiler import __mscm_invoke__
        from mtypes import NIL as _NIL
        args_val = args if args is not None else _NIL
        # 迭代 trampoline（C# JitRuntime.Invoke 等价）：宏编译体的 JIT 尾调用
        # 在循环内解包，避免递归 __mscm_eval_tail_call__ 逐层 +1 栈帧。
        r = __mscm_invoke__(compiled_lp, [args_val], env)
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
    key = proc
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
    def marker_name(value):
        if isinstance(value, Cell) and isinstance(value.car, Sym) and value.car.name == 'sx-hygiene':
            if isinstance(value.cdr, Cell) and value.cdr.cdr is NIL and isinstance(value.cdr.car, Sym):
                return value.cdr.car.name
        return value.name if isinstance(value, Sym) else None

    def walk(value, bound):
        while isinstance(value, SyntaxObject):
            value = value.expr
        if not isinstance(value, Cell):
            return value
        if isinstance(value.car, Sym) and value.car.name == 'sx-hygiene':
            name = marker_name(value)
            if name in bound:
                return Sym(name)
            v = defEnv.data.get(name) if name is not None else None
            if v is not None and not callable(v) and not (isinstance(v, tuple) and v and v[0] == 'macro'):
                return Cell(SYM_QUOTE, Cell(v, NIL))
            return Sym(name) if name is not None else value
        if (isinstance(value.car, Sym) and value.car.name in ('let', 'let*', 'letrec', 'letrec*')
                and isinstance(value.cdr, Cell) and isinstance(value.cdr.car, Cell)):
            binds, rest = value.cdr.car, value.cdr.cdr
            names = []
            out = NIL
            tail = None
            cur = binds
            while isinstance(cur, Cell):
                b = cur.car
                if isinstance(b, Cell):
                    name = marker_name(b.car)
                    name_value = Sym(name) if name is not None else walk(b.car, bound)
                    names.append(name or (name_value.name if isinstance(name_value, Sym) else ''))
                    item = Cell(name_value, walk(b.cdr, bound))
                else:
                    item = walk(b, bound)
                node = Cell(item, NIL)
                if tail is None: out = node
                else: tail.cdr = node
                tail = node
                cur = cur.cdr
            body = walk(rest, bound | {n for n in names if n})
            return Cell(value.car, Cell(out, body))
        return Cell(walk(value.car, bound), walk(value.cdr, bound))

    return walk(expr, set())

def _sx_def_env():
    global _CURRENT_MACRO_DEF_ENV
    from mtypes import be
    return _CURRENT_MACRO_DEF_ENV or be

def _sx_expand_env():
    global _CURRENT_EXPAND_ENV
    from mtypes import be
    return _CURRENT_EXPAND_ENV or be

# Implementations moved from initbuiltin.py.

def _number_to_string(x, radix=10):
    radix = int(radix)
    if radix != 10 and isinstance(x, int) and not isinstance(x, bool):
        if not 2 <= radix <= 36:
            raise ValueError("number->string: invalid radix")
        digits = "0123456789abcdefghijklmnopqrstuvwxyz"
        sign = "-" if x < 0 else ""
        n = abs(x)
        out = "0" if n == 0 else ""
        while n:
            out = digits[n % radix] + out
            n //= radix
        return SchemeString(sign + out)
    return SchemeString(str(x))

def _min_sx_expand(tmpl, bindings):
    return sx_expand(tmpl, bindings, _sx_mutated_vars, sx_def_env())

def _read_bytevector(n, port=None):
    p = port
    if p is None:
        data = sys.stdin.buffer.read(n)
        return SchemeBytevector(list(data))
    if isinstance(p, tuple) and p[0] == 'bin-str-port':
        data, pos = p[1]
        end = min(pos + n, len(data))
        p[1][1] = end
        return SchemeBytevector(list(data[pos:end]))
    if isinstance(p, tuple) and p[0] == 'bin-file-port' and len(p) > 3:
        return SchemeBytevector(list(p[3].read(n)))
    return SchemeBytevector([])

def _read_bytevector_into(target, port):
    data = _read_bytevector(len(target.data), port)
    target.data[:len(data.data)] = data.data
    return len(data.data)

def _write_bytevector(value, port):
    data = bytes(value.data)
    if isinstance(port, tuple) and port[0] == 'byte-port':
        port[1].extend(data)
    elif isinstance(port, tuple) and port[0] == 'bin-file-port' and len(port) > 3:
        port[3].write(data)
    return VOID


def _last_pair(lst):
    cur = lst
    while isinstance(cur, Cell) and cur.cdr is not NIL:
        cur = cur.cdr
    return cur

def make_coroutine_generator(proc):
    import queue, threading
    from mtypes import EOF as _EOF
    vals = queue.Queue()
    done = [False]
    resume = threading.Event()

    def _yield(v):
        vals.put(v)
        resume.clear()
        resume.wait()

    def _run():
        try:
            proc(_yield)
        finally:
            vals.put(_EOF)
            done[0] = True

    t = threading.Thread(target=_run, daemon=True)
    t.start()
    started = [False]

    def gen():
        if done[0] and vals.empty():
            return _EOF
        if not started[0]:
            started[0] = True
            resume.set()
        v = vals.get()
        resume.set()
        return v

    return gen


def _reduce_bit_or(args):
    r = 0
    for a in args: r |= int(a)
    return r

def _sys_exit(code):
    raise SystemExit(code)

def _redirect_in(stream):
    sys.stdin = stream

def _redirect_out(stream):
    sys.stdout = stream

def _with_file(path, thunk, mode, redirect):
    old = sys.stdin if mode == 'r' else sys.stdout
    with open(str(path), mode) as f:
        redirect(f)
        try:
            r = call(thunk, [])
        finally:
            if mode == 'r': sys.stdin = old
            else: sys.stdout = old
    return r

def _with_string_input(value, thunk):
    old = sys.stdin
    _redirect_in(io.StringIO(value))
    try:
        return call(thunk, [])
    finally:
        _redirect_in(old)

def _inexact_to_exact_fn(x):
    if isinstance(x, float):
        if x != x or x == float('inf') or x == float('-inf'):
            raise SchemeException("inexact->exact: not a finite number")
        return Fraction(x).limit_denominator(1000000)
    if isinstance(x, Fraction) and x.denominator == 1: return int(x)
    return x
def _string_to_number(s, radix=10):
    text = str(s)
    radix = int(radix)
    if radix != 10:
        try:
            return int(text, radix)
        except ValueError:
            return FALSE
    return parse_number_scheme(text)

def port_pos(p):
    if isinstance(p, tuple) and p[0] == 'str-port' and isinstance(p[1], list) and len(p[1]) > 1:
        if not hasattr(set_port_pos, '_saved_str'):
            set_port_pos._saved_str = {}
        original = set_port_pos._saved_str.setdefault(id(p), p[1][0])
        return len(original) - len(p[1][0])
    if isinstance(p, tuple) and p[0] == 'file-port' and len(p) > 3:
        return p[3].tell()
    if isinstance(p, tuple) and p[0] == 'bin-str-port' and isinstance(p[1], list) and len(p[1]) > 1:
        return p[1][1]
    return 0

def hash_table_set(ht, *pairs):
    if len(pairs) % 2: raise SchemeException('hash-table-set!: expected key/value pairs')
    for i in range(0, len(pairs), 2): ht[pairs[i]] = pairs[i + 1]
    return VOID
def hash_table_merge_slash(dst, src):
    dst.update(src)
    return dst

def _call(proc, *args):
    return proc(*args)


class Hook:
    def __init__(self): self.procedures = []


class RandomSource:
    def __init__(self, state=None): self.state = int(time.time()) if state is None else int(state)

    def step(self):
        self.state = (1103515245 * self.state + 12345) % 2147483648
        return self.state


class BinaryHeap:
    def __init__(self, cmp=lambda a, b: a < b, initial=NIL):
        self.cmp, self.items = cmp, list(cell_iter(initial))
        for i in range(len(self.items) // 2 - 1, -1, -1): self._down(i)

    def _down(self, i):
        n = len(self.items)
        while True:
            left, right, best = 2 * i + 1, 2 * i + 2, i
            if left < n and scheme_truthy(self.cmp(self.items[left], self.items[best])): best = left
            if right < n and scheme_truthy(self.cmp(self.items[right], self.items[best])): best = right
            if best == i: return
            self.items[i], self.items[best], i = self.items[best], self.items[i], best

    def insert(self, value):
        self.items.append(value); i = len(self.items) - 1
        while i:
            p = (i - 1) // 2
            if not scheme_truthy(self.cmp(self.items[i], self.items[p])): break
            self.items[i], self.items[p], i = self.items[p], self.items[i], p


class Bimap:
    def __init__(self, init):
        self.forward, self.reverse = {}, {}
        for pair in cell_iter(init): self.set(pair.car, pair.cdr)
    def set(self, key, value):
        self.forward[key], self.reverse[value] = value, key

    def forward_ref(self, key, default=FALSE):
        return self.forward.get(key, default)

    def reverse_ref(self, value, default=FALSE):
        return self.reverse.get(value, default)


class Deque:
    def __init__(self, items=()): self.items = list(items)


class Array:
    def __init__(self, dimensions, value=0):
        self.dimensions = list(dimensions)
        def build(ds):
            if len(ds) == 1: return SchemeVector([value] * ds[0])
            return SchemeVector([build(ds[1:]) for _ in range(ds[0])])
        self.value = build(self.dimensions)


def _pair_items(m):
    return [(p.car, p.cdr) for p in cell_iter(m)]


def _mapping(*pairs):
    vals = cells(pairs[0]) if len(pairs) == 1 and isinstance(pairs[0], Cell) else list(pairs)
    return _lst([Cell(vals[i], vals[i + 1]) for i in range(0, len(vals) - 1, 2)])


def _array_dims(x):
    if not isinstance(x, SchemeVector): return []
    return [len(x.data)] + _array_dims(x.data[0]) if x.data else [0]




def builtin_remove_heap(h):
    value = h.items.pop(0)
    if h.items: h._down(0)
    return value


def _array_ref(a, indices):
    for i in indices: a = a.data[int(i)]
    return a


def _array_set(a, value, indices):
    for i in indices[:-1]: a = a.data[int(i)]
    a.data[int(indices[-1])] = value
    return VOID


def _char_set_integer(cs):
    values = cs.data if hasattr(cs, 'data') else cs
    result = 0
    for i, value in enumerate(values[:256]):
        if scheme_truthy(value): result = result * 33 + i
    return result

def rint(s, n): return int(round(s.step() / 2147483648.0 * int(n))) % int(n)

def qremove(q, end=False):
    if not q['items']: raise ValueError('empty list queue')
    return q['items'].pop(-1 if end else 0)

def _gen_fold(f, acc, g):
    while True:
        x = g()
        if x is EOF: return acc
        acc = f(x, acc)


def _bit_fold(fn, values):
    if not values: return -1 if fn(1, 1) == 1 else 0
    result = int(values[0])
    for value in values[1:]: result = fn(result, int(value))
    return result


def _loop_n(n):
    return _loop_n(n - 1) if n else Sym('done')


def _json_value(x):
    if x is TRUE: return True
    if x is FALSE: return False
    if x is NIL: return None
    if isinstance(x, SchemeString): return str(x)
    if isinstance(x, Cell): return [_json_value(v) for v in cell_iter(x)]
    if isinstance(x, SchemeVector): return [_json_value(v) for v in x.data]
    if isinstance(x, Sym): return x.name
    return x


def _map_value(f, value): return EOF if value is EOF else f(value)
def _filter_value(p, g):
    while True:
        value = g()
        if value is EOF or scheme_truthy(p(value)): return value


def _vector_cumulate(f, init, v):
    result, acc = [], init
    for value in v.data:
        acc = f(acc, value); result.append(acc)
    return SchemeVector(result)


def _vector_index_right(p, v, *start):
    data = v.data; i = int(start[0]) if start else len(data)-1
    while i >= 0:
        if scheme_truthy(p(data[i])): return i
        i -= 1
    return FALSE


def _vector_skip_right(p, v, *start):
    data = v.data; i = int(start[0]) if start else len(data)-1
    while i >= 0:
        if not scheme_truthy(p(data[i])): return i
        i -= 1
    return FALSE


def _vector_append_subvectors(*args):
    result = []
    for i in range(0, len(args), 3): result.extend(args[i].data[int(args[i+1]):int(args[i+2])])
    return SchemeVector(result)


def _reverse_bang(lst):
    previous = NIL
    current = lst
    while isinstance(current, Cell):
        following = current.cdr
        current.cdr = previous
        previous, current = current, following
    return previous


def _gen_take(n, g):
    left = [n]
    def out():
        if left[0] <= 0: return EOF
        left[0] -= 1; return g()
    return out


def _vec_fold(f, acc, v):
    for i,x in enumerate(v.data if isinstance(v, SchemeVector) else v): acc = f(i,x,acc)
    return acc


def _vec_fold_right(f, acc, v):
    data = v.data if isinstance(v, SchemeVector) else v
    for i in range(len(data)-1,-1,-1): acc = f(i,data[i],acc)
    return acc


def _vec_map_bang(f,v):
    for i,x in enumerate(v.data): v.data[i] = f(x)
    return VOID

def drop_gen(n, g):
    for _ in range(n):
        if g() is EOF: break
    return g

def rcons(acc, value):
    items = list(cell_iter(acc)) if isinstance(acc, Cell) else []
    return _lst(items + [value])
def tmap(f):
    return lambda reducer: lambda acc, value: _scheme_call(reducer, [acc, _scheme_call(f, [value])])
def tfilter(pred):
    return lambda reducer: lambda acc, value: _scheme_call(reducer, [acc, value]) if scheme_truthy(_scheme_call(pred, [value])) else acc
def list_transduce(xform, reducer, init, values):
    step = _scheme_call(xform, [reducer])
    acc = init
    for value in cell_iter(values):
        acc = _scheme_call(step, [acc, value])
    return acc


class ISet:
    __slots__ = ('items',)
    def __init__(self, items=None):
        self.items = set()
        if items is not None:
            for it in items:
                self.items.add(int(it))
    def __repr__(self):
        return '#<iset %s>' % sorted(self.items)

def _unsupported(name):
    def fail(*args):
        raise SchemeException(f'{name}: unsupported by this implementation')
    return fail

def iset_fn(*xs):
    return ISet(xs)
def iset_p(x):
    return TRUE if isinstance(x, ISet) else FALSE
def iset_contains_p(s, v):
    return TRUE if int(v) in s.items else FALSE
def iset_adjoin(s, *xs):
    n = ISet(); n.items = set(s.items)
    for x in xs: n.items.add(int(x))
    return n
def iset_delete(s, *xs):
    n = ISet(); n.items = set(s.items)
    for x in xs: n.items.discard(int(x))
    return n
def iset_empty():
    return ISet()
def iset_size(s):
    return len(s.items)
def iset_empty_p(s):
    return TRUE if not s.items else FALSE
def iset_union(a, b):
    n = ISet(); n.items = a.items | b.items; return n
def iset_intersection(a, b):
    n = ISet(); n.items = a.items & b.items; return n
def iset_difference(a, b):
    n = ISet(); n.items = a.items - b.items; return n
def iset_to_list(s):
    return _lst(sorted(s.items))
def list_to_iset(xs):
    return ISet(cell_iter(xs))
def update_fn(lst, i, proc):
    xs = list(cell_iter(lst))
    idx = int(i)
    xs[idx] = proc(xs[idx])
    return _lst(xs)

def _append_bang(*xs):
    return append(*xs)

def _append_reverse_bang(x, y):
    return append(reverse(x), y)

def _char_set_unfold(stop, mapper, successor, seed, *bases):
    result = [False] * 256
    state = seed
    while not stop(state):
        ch = mapper(state)
        cp = ord(cs_char(ch))
        if cp < 256: result[cp] = True
        state = successor(state)
    for base in bases:
        result = char_set_binop([result, base], lambda a, b: a or b)
    return result

def _integer_char_set(value):
    n = int(value)
    return [bool(n & (1 << i)) for i in range(256)]

def _drop_right_bang(xs, n):
    items = list(cell_iter(xs))
    keep = len(items) - int(n)
    if keep < 0: raise SchemeException('drop-right!: count exceeds list length')
    cur = xs
    if keep == 0: return NIL
    for _ in range(1, keep): cur = cur.cdr
    cur.cdr = NIL
    return xs

def _find_tail(pred, xs):
    cur = xs
    while isinstance(cur, Cell):
        if pred(cur.car) is not FALSE: return cur
        cur = cur.cdr
    return FALSE

def _fold_right_1(proc, xs):
    values = list(cell_iter(xs))
    if not values: raise SchemeException('fold-right-1: empty list')
    acc = values[-1]
    for value in reversed(values[:-1]): acc = proc(value, acc)
    return acc

def _include_ci(path):
    import pathlib
    requested = str(path)
    p = pathlib.Path(requested)
    if not p.exists():
        matches = [x for x in p.parent.iterdir() if x.name.lower() == p.name.lower()]
        if matches: p = matches[0]
    if not p.exists(): raise SchemeException(f'include-ci: file not found: {requested}')
    from miniscm import load_file
    return load_file(str(p))

def _lset_adjoin(eq, xs, *values):
    result = list(cell_iter(xs))
    for value in values:
        if not any(eq(value, old) is TRUE for old in result): result.append(value)
    return _lst(result)

def _lset_subset(eq, *lists):
    for left, right in zip(lists, lists[1:]):
        if any(not any(eq(x, y) is TRUE for y in cell_iter(right)) for x in cell_iter(left)): return FALSE
    return TRUE

def _random_integers(source, bound):
    rng = source if isinstance(source, _random.Random) else _random.Random()
    return lambda n: rng.randrange(int(n))

def _random_reals(source):
    rng = source if isinstance(source, _random.Random) else _random.Random()
    return lambda: rng.random()

def _test_equal(actual, expected):
    return TRUE if actual == expected else FALSE

def sorted_by_fn(pred, lst):
    xs = list(cell_iter(lst))
    def _cmp(a, b):
        if scheme_truthy(pred(a, b)): return -1
        if scheme_truthy(pred(b, a)): return 1
        return 0
    return _lst(sorted(xs, key=_functools.cmp_to_key(_cmp)))

def _group_by(pred, values):
    groups = {}
    order = []
    for value in cell_iter(values):
        key = call(pred, [value])
        key = key.name if isinstance(key, Sym) else key
        if key not in groups:
            groups[key] = []
            order.append(key)
        groups[key].append(value)
    return _lst([_lst(groups[key]) for key in order])

def _option(spec, required, handler):
    return ('option', spec, required, handler)

def hash_table_merge_bang(dst, src):
    dst.update(src)
    return dst

def file_exists_fn(p):
    from miniscm import _resolve_load_path
    r = _resolve_load_path(p)
    return TRUE if (r is not None and _os.path.exists(r)) else FALSE

# ---- primitives.py ----
# primitives.py


# ── 从 primitives_first 导入自举核心函数 ──

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
    fmt = str(fmt)
    parts=[]; i=0; ai=0
    while i<len(fmt):
        if fmt[i]=='~' and i+1<len(fmt):
            c=fmt[i+1]
            if c=='a':
                value = args[ai]
                parts.append(value.char if isinstance(value, SchemeChar) else (str(value) if isinstance(value, (str, SchemeString)) else _pr(value))); ai+=1; i+=2
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
    return ''.join(str(p) for p in parts)

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
    return load_file(str(path))
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
    if len(a) >= 2 and a[0] is FALSE:
        return format(a[1], list(a[2:]))
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
    if isinstance(p, tuple) and p[0] == 'bin-str-port' and isinstance(p[1], list):
        p[1][1] = max(0, min(pos, len(p[1][0])))
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



# The extension implementations use this explicit alias to avoid importing the
# core module back into the consolidated primitive module.
_scheme_call = call

# ---- primitives_ext.py ----
# primitives_ext.py — R7RS-large 扩展内置过程
# 通过 Python builtin 实现高性能原语，在 miniscm.py 引导时使用 initenv_ext() 注册


# char_ci_eq: 字符大小写不敏感比较（从 primitives.py 迁入）
def char_ci_eq(a,b):
    ca=a[1].lower() if isinstance(a,tuple) else (a.char.lower() if hasattr(a,'char') else str(a).lower())
    cb=b[1].lower() if isinstance(b,tuple) else (b.char.lower() if hasattr(b,'char') else str(b).lower())
    return TRUE if ca==cb else FALSE

# rvrs: 列表反转（迭代式，非递归；从 primitives.py 迁入）
def rvrs(lst):
    r=NIL
    cur = lst
    while isinstance(cur, Cell):
        r=Cell(cur.car,r); cur=cur.cdr
    if cur is not NIL: raise SchemeException("reverse: dotted list")
    return r

# vec_fill_range: 向量范围填充（可选 start/end；从 primitives.py 迁入）
def vec_fill_range(v, x, *a):
    if hasattr(v, 'data'):
        start = a[0] if a else 0
        end = a[1] if len(a) > 1 else len(v.data)
        for i in range(start, end): v.data[i] = x
    elif isinstance(v, list):
        start = a[0] if a else 0
        end = a[1] if len(a) > 1 else len(v)
        for i in range(start, end): v[i] = x

# ─── 辅助 ───
def is_exact_int(x):
    return isinstance(x, (int, Fraction)) and (isinstance(x, int) or x.denominator == 1)

def is_real(x):
    return isinstance(x, (int, float, Fraction)) or (isinstance(x, complex) and x.imag == 0)

def is_flonum(x):
    return isinstance(x, float)

def is_fixnum(x):
    return isinstance(x, int) and -((1<<63)-1) <= x <= (1<<63)-1

def nth(lst, n):
    if not isinstance(lst, Cell): return FALSE
    cur = lst
    for _ in range(n):
        if not isinstance(cur.cdr, Cell): return FALSE
        cur = cur.cdr
    return cur.car

def to_int(x):
    if isinstance(x, Sym):
        return int(x.name)
    return int(x)

def check_fx(x):
    if not isinstance(x, int):
        raise TypeError(f"not a fixnum: {x}")
    if x < -((1<<62)-1) or x > (1<<62)-1:
        raise TypeError(f"fixnum overflow: {x}")
    return x

# ═══════════════════════════════════════════════════════════════════
# SRFI-111: Boxes
# ═══════════════════════════════════════════════════════════════════

# ═══════════════════════════════════════════════════════════════════
# SRFI-128: Comparators
# 比较器封装：(type, compare-proc, hash-proc, name)
# ═══════════════════════════════════════════════════════════════════
COMPARATOR_TAG = 'comparator'

def make_comparator(eq, lt, hash_fn, name='custom'):
    return [COMPARATOR_TAG, eq, lt, hash_fn, name]

def is_comparator(x):
    if isinstance(x, list) or isinstance(x, Cell):
        from mtypes import _cell_len
        length = _cell_len(x) if isinstance(x, Cell) else len(x)
        return length >= 3

def comparator_eq_fn(c):
    return c[1] if is_comparator(c) else (lambda a, b: a is b or a == b)

def comparator_lt_fn(c):
    return c[2] if is_comparator(c) else (lambda a, b: (a is not b and a is FALSE) if False else (a < b))

def comparator_hash_fn(c):
    return c[3] if is_comparator(c) else (lambda x: hash(str(x)))

# 默认比较器：eqv 语义
def default_comparator():
    return make_comparator(
        lambda a, b: TRUE if (a is b or (hasattr(a, '__eq__') and a == b)) else FALSE,
        lambda a, b: TRUE if (isinstance(a, (int, float, Fraction, complex)) and isinstance(b, (int, float, Fraction, complex)) and a < b) else FALSE if (isinstance(a, str) and isinstance(b, str) and a < b) else FALSE,
        lambda x: hash(repr(x)),
        'default'
    )

def is_comparator_order(c):
    return is_comparator(c)

def is_comparator_hashable(c):
    return is_comparator(c)

# ═══════════════════════════════════════════════════════════════════
# SRFI-141: Division (handles int, Fraction, float; rejects complex)
# ═══════════════════════════════════════════════════════════════════

def as_numeric_pair(n, d):
    if isinstance(n, complex) or isinstance(d, complex):
        raise TypeError("complex numbers not supported")
    if not isinstance(n, (int, Fraction, float)):
        n = float(n) if hasattr(n, '__float__') else int(n)
    if not isinstance(d, (int, Fraction, float)):
        d = float(d) if hasattr(d, '__float__') else int(d)
    return n, d

def floor_div(n, d):
    if d == 0: raise ValueError("division by zero")
    n, d = as_numeric_pair(n, d)
    return n // d  # // does floor division for int, Fraction, float

def floor_rem(n, d):
    if d == 0: raise ValueError("division by zero")
    n, d = as_numeric_pair(n, d)
    return n % d  # % gives floor remainder

def floor_mod(n, d):
    if d == 0: raise ValueError("division by zero")
    n, d = as_numeric_pair(n, d)
    return n % d

def truncate_div(n, d):
    if d == 0: raise ValueError("division by zero")
    n, d = as_numeric_pair(n, d)
    q = n / d
    if isinstance(q, Fraction):
        return q.numerator // q.denominator if q.numerator >= 0 else -(-q.numerator // q.denominator)
    return int(q) if q >= 0 else -int(-q)

def truncate_rem(n, d):
    if d == 0: raise ValueError("division by zero")
    n, d = as_numeric_pair(n, d)
    return n - truncate_div(n, d) * d

def ceiling_div(n, d):
    if d == 0: raise ValueError("division by zero")
    n, d = as_numeric_pair(n, d)
    return -(-n // d)

def ceiling_rem(n, d):
    if d == 0: raise ValueError("division by zero")
    n, d = as_numeric_pair(n, d)
    return n - ceiling_div(n, d) * d

def round_div(n, d):
    if d == 0: raise ValueError("division by zero")
    n, d = as_numeric_pair(n, d)
    q = n / d
    if isinstance(q, Fraction):
        return int(round(q))
    return round(q)

def euclidean_div(n, d):
    if d == 0: raise ValueError("division by zero")
    n, d = as_numeric_pair(n, d)
    r = n % abs(d)
    return (n - r) // d

def euclidean_rem(n, d):
    if d == 0: raise ValueError("division by zero")
    n, d = as_numeric_pair(n, d)
    if d < 0:
        d = -d
    r = n % d
    if isinstance(r, Fraction):
        if r < 0: r += d
    elif r < 0:
        r += float(d)
    return r

# ═══════════════════════════════════════════════════════════════════
# SRFI-143: Fixnums (64-bit)
# ═══════════════════════════════════════════════════════════════════
FX_WIDTH = 64
FX_GREATEST = (1 << 63) - 1
FX_LEAST = -(1 << 63)

def fxcheck(x):
    if not isinstance(x, int):
        raise TypeError(f"not a fixnum: {x}")
    return x

def fxwrap(x):
    if x >= 0: return x & FX_GREATEST
    if x == -1: return -1
    return -((-x) & FX_GREATEST)

def fx_add(*args):
    r = 0
    for a in args: r = fxcheck(r) + fxcheck(a)
    if r > FX_GREATEST or r < FX_LEAST:
        raise TypeError("fixnum overflow")
    return r

def fx_sub(x, *args):
    r = fxcheck(x)
    for a in args: r -= fxcheck(a)
    if r > FX_GREATEST or r < FX_LEAST:
        raise TypeError("fixnum overflow")
    return r

def fx_mul(*args):
    r = 1
    for a in args: r = fxcheck(r) * fxcheck(a)
    if r > FX_GREATEST or r < FX_LEAST:
        raise TypeError("fixnum overflow")
    return r

def fx_div(x, y):
    x, y = fxcheck(x), fxcheck(y)
    if y == 0: raise SchemeException("fxdiv: division by zero")
    return truncate_div(x, y)

def fx_mod(x, y):
    x, y = fxcheck(x), fxcheck(y)
    if y == 0: raise SchemeException("fxmod: division by zero")
    q = truncate_div(x, y)
    return fxwrap(x - q * y)

def fx_and(*args):
    r = FX_GREATEST
    for a in args: r &= fxcheck(a)
    return r

def fx_ior(*args):
    r = 0
    for a in args: r |= fxcheck(a)
    return r

def fx_xor(*args):
    r = 0
    for a in args: r ^= fxcheck(a)
    return r

def fx_not(x):
    return fxcheck(x) ^ FX_GREATEST

def fx_lsh(x, n):
    x, n = fxcheck(x), int(n)
    return fxwrap(x << n)

def fx_rshl(x, n):
    x, n = fxcheck(x), int(n)
    return x >> n

def fx_rsha(x, n):
    x, n = fxcheck(x), int(n)
    return x >> n

def fx_cmp(op, *args):
    for i in range(len(args) - 1):
        if not op(fxcheck(args[i]), fxcheck(args[i+1])):
            return FALSE
    return TRUE

# ═══════════════════════════════════════════════════════════════════
# SRFI-144: Flonums
# ═══════════════════════════════════════════════════════════════════
def fl_check(x):
    if not isinstance(x, float):
        if isinstance(x, (int, Fraction)): raise SchemeException("expected flonum")
        return float(x)
    return x

def fl_add(*args):
    r = 0.0
    for a in args: r += fl_check(a)
    return r

def fl_sub(x, *args):
    if not args: return fl_check(-x)
    r = fl_check(x)
    for a in args: r -= fl_check(a)
    return r

def fl_mul(*args):
    r = 1.0
    for a in args: r *= fl_check(a)
    return r

def fl_div(x, *args):
    if not args: return fl_check(1.0 / fl_check(x))
    r = fl_check(x)
    for a in args: r /= fl_check(a)
    return r

def fl_cmp(op, *args):
    for i in range(len(args) - 1):
        if not op(args[i], args[i+1]):
            return FALSE
    return TRUE

def fl_min(*args):
    for a in args: fl_check(a)
    return min(args)

def fl_max(*args):
    for a in args: fl_check(a)
    return max(args)

# ═══════════════════════════════════════════════════════════════════
# SRFI-151: Bitwise Operations (enhanced)
# ═══════════════════════════════════════════════════════════════════
def bitwise_not(n):
    return ~int(n)

def bitwise_and(*args):
    r = -1
    for a in args: r &= int(a)
    return r

def bitwise_ior(*args):
    r = 0
    for a in args: r |= int(a)
    return r

def bitwise_xor(*args):
    r = 0
    for a in args: r ^= int(a)
    return r

def bitwise_if(m, t, e):
    m, t, e = int(m), int(t), int(e)
    return (m & t) | (~m & e)

def bitwise_length(n):
    n = int(n)
    if n >= 0: return n.bit_length()
    if n == -1: return 0
    return (~n).bit_length() - 1

def bitwise_count(n):
    n = int(n)
    if n >= 0: return n.bit_count()
    return (~n).bit_count() + 1 if n != -1 else 0 if hasattr(n, 'bit_count') else bin(n).count('1')

def bitwise_reverse_bitfield(n, start, end):
    n = int(n); start = int(start); end = int(end)
    width = end - start
    if width <= 0: return n
    mask = (1 << width) - 1
    field = (n >> start) & mask
    rev = 0
    for _ in range(width):
        rev = (rev << 1) | (field & 1)
        field >>= 1
    return (n & ~(mask << start)) | (rev << start)

def integer_length(n):
    return bitwise_length(n)

def integer_to_bits(n, k):
    n, k = int(n), int(k)
    return _lst([Sym('0') if (n >> i) & 1 == 0 else Sym('1') for i in range(k)])

def first_set_bit(n):
    n = int(n)
    if n == 0: return -1
    return (n & -n).bit_length() - 1

def bitwise_shift(n, cnt):
    n = int(n)
    if cnt >= 0:
        return n << cnt
    return n >> (-cnt)

# ═══════════════════════════════════════════════════════════════════
# SRFI-152: String Utilities
# ═══════════════════════════════════════════════════════════════════
def make_scheme_str(s):
    if isinstance(s, SchemeString): return s
    return SchemeString(s)

def string_take(s, n):
    s = str(s); return SchemeString(s[:n])

def string_drop(s, n):
    s = str(s); return SchemeString(s[n:])

def string_take_right(s, n):
    s = str(s)
    if n == 0: return SchemeString('')
    return SchemeString(s[-n:])

def string_drop_right(s, n):
    s = str(s)
    if n == 0: return SchemeString(s)
    return SchemeString(s[:-n])

def string_pad(s, n, ch=' '):
    s = str(s)
    ch = cs_char(ch) if not isinstance(ch, str) else (ch[0] if ch else ' ')
    if len(s) >= n: return SchemeString(s[:n])
    return SchemeString(ch * (n - len(s)) + s)

def string_pad_right(s, n, ch=' '):
    s = str(s)
    ch = cs_char(ch) if not isinstance(ch, str) else (ch[0] if ch else ' ')
    if len(s) >= n: return SchemeString(s[:n])
    return SchemeString(s + ch * (n - len(s)))

def string_trim(s, char_set=None):
    text = str(s)
    if char_set is None: return SchemeString(text.strip())
    def matches(ch):
        value = call(char_set, [SchemeChar(ch)]) if not callable(char_set) else char_set(SchemeChar(ch))
        return scheme_truthy(value)
    left = 0
    right = len(text)
    while left < right and matches(text[left]): left += 1
    while right > left and matches(text[right - 1]): right -= 1
    return SchemeString(text[left:right])

def string_trim_right(s):
    return SchemeString(str(s).rstrip())

def string_trim_both(s):
    return SchemeString(str(s).strip())

def string_replace(s, rep, start, end):
    return SchemeString(str(s)[:int(start)] + str(rep) + str(s)[int(end):])

def string_split(s, sep=None):
    s = str(s)
    if sep is None:
        parts = s.split()
    else:
        sep = cs_char(sep) if isinstance(sep, SchemeChar) else str(sep)
        parts = s.split(sep)
    return _lst([SchemeString(p) for p in parts])

def string_join(parts, delim=' '):
    ls = []
    cur = parts
    while isinstance(cur, Cell):
        ls.append(str(cur.car))
        cur = cur.cdr
    return SchemeString(str(delim).join(ls))

def string_contains(s, needle):
    i = str(s).find(str(needle))  # str(SchemeString) returns content
    return i if i >= 0 else FALSE

def str_prefix_q(s1, s2):
    return TRUE if str(s2).startswith(str(s1)) else FALSE

def str_suffix_q(s1, s2):
    return TRUE if str(s2).endswith(str(s1)) else FALSE

def string_count(s, pred_or_char):
    s = str(s)
    if callable(pred_or_char):
        return sum(1 for ch in s if pred_or_char(SchemeChar(ch)) is TRUE)
    return s.count(str(pred_or_char))

def string_map(fn, s):
    s = str(s)
    chars = []
    for ch in s:
        r = fn(SchemeChar(ch))
        if isinstance(r, SyntaxObject):
            chars.append(_so(r))
        elif isinstance(r, SchemeChar):
            chars.append(r.char)
        elif isinstance(r, tuple) and len(r) == 2 and r[0] == 'char':
            chars.append(r[1])
        elif isinstance(r, str):
            chars.append(r)
        else:
            chars.append(str(r))
    return SchemeString(''.join(chars))

def string_for_each(fn, s):
    s = str(s)
    for ch in s: fn(SchemeChar(ch))
    return VOID

def string_fold(fn, init, s):
    s = str(s)
    acc = init
    for ch in s:
        acc = fn(SchemeChar(ch), acc)
    return acc

# ═══════════════════════════════════════════════════════════════════
# SRFI-133: Vector Extensions
# ═══════════════════════════════════════════════════════════════════
def vec(v):
    if isinstance(v, SchemeVector): return v.data
    if isinstance(v, list): return v
    return list(v)

def vector_map(fn, *vecs):
    if not vecs: return NIL
    vdata = [vec(v) for v in vecs]
    result = [fn(*args) for args in zip(*vdata)]
    return SchemeVector(result)

def vector_for_each(fn, *vecs):
    if not vecs: return VOID
    vdata = [vec(v) for v in vecs]
    for args in zip(*vdata): fn(*args)
    return VOID

def vector_append(*vecs):
    result = []
    for v in vecs:
        result.extend(vec(v))
    return SchemeVector(result)

def vector_count(pred, v):
    return sum(1 for x in vec(v) if pred(x) is TRUE)

def vector_any(pred, v):
    for x in vec(v):
        if scheme_truthy(pred(x)): return TRUE
    return FALSE

def vector_every(pred, v):
    for x in vec(v):
        if pred(x) is FALSE: return FALSE
    return TRUE

def vector_fold(fn, init, v):
    acc = init
    for i, x in enumerate(vec(v)):
        acc = fn(i, acc, x)
    return acc

def vector_fold_right(fn, init, v):
    acc = init
    data = vec(v)
    for i in range(len(data) - 1, -1, -1):
        acc = fn(i, acc, data[i])
    return acc

def do_vector_swap(v, i, j):
    v = vec(v) if isinstance(v, SchemeVector) else v
    v[i], v[j] = v[j], v[i]
    return VOID

def do_vector_reverse(v):
    v = vec(v) if isinstance(v, SchemeVector) else v
    v.reverse()
    return VOID

def do_vector_map(fn, v):
    if isinstance(v, SchemeVector):
        vd = vec(v)
        for i in range(len(vd)):
            vd[i] = call(fn, [vd[i]]) if not callable(fn) else fn(vd[i])
        return v
    vd = list(v)
    for i in range(len(vd)):
        vd[i] = call(fn, [vd[i]]) if not callable(fn) else fn(vd[i])
    return VOID

def vec_empty_q(v):
    return TRUE if len(vec(v)) == 0 else FALSE

def vector_unfold(fn, n, seed):
    result = []
    s = seed
    for i in range(n):
        r = fn(i, s)
        if isinstance(r, Cell):
            result.append(r.car)
            s = r.cdr
        elif isinstance(r, tuple) and len(r) >= 2:
            result.append(r[0])
            s = r[1] if len(r) == 2 else _lst(r[1:])
        else:
            result.append(r)
            s = r
    return SchemeVector(result)

def vector_index(pred, v):
    for i, x in enumerate(vec(v)):
        if scheme_truthy(pred(x)): return i
    return FALSE

def vector_skip(pred, v):
    vd = vec(v)
    for i, x in enumerate(vd):
        if pred(x) is FALSE: return i
    return len(vd)

# ═══════════════════════════════════════════════════════════════════
# SRFI-158: Generators (enhanced)
# ═══════════════════════════════════════════════════════════════════


def make_generator(gen_fn):
    return gen_fn

def list_generator(lst):
    items = []
    cur = lst
    while isinstance(cur, Cell):
        items.append(cur.car)
        cur = cur.cdr
    it = iter(items)
    return lambda: next(it, EOF)

def vector_generator(v):
    it = iter(vec(v))
    return lambda: next(it, EOF)

def string_generator(s):
    it = iter(str(s))
    return lambda: next(it, EOF)

def generator(*vals):
    it = iter(vals)
    return lambda: next(it, EOF)

def generator_map(fn, g):
    def gen_map():
        try:
            v = g()
            while v is not EOF:
                yield _scheme_call(fn, [v])
                v = g()
        except: pass
    it = gen_map()
    return lambda: next(it, EOF)

def generator_filter(pred, g):
    def gen_filter():
        try:
            v = g()
            while v is not EOF:
                if scheme_truthy(_scheme_call(pred, [v])):
                    yield v
                v = g()
        except: pass
    it = gen_filter()
    return lambda: next(it, EOF)

def generator_take(g, n):
    cnt = [0]
    def gen_take():
        try:
            v = g()
            while v is not EOF and cnt[0] < n:
                cnt[0] += 1
                yield v
                v = g()
        except: pass
    it = gen_take()
    return lambda: next(it, EOF)

def generator_drop(g, n):
    cnt = [0]
    def gen_drop():
        try:
            while cnt[0] < n:
                v = g()
                if v is EOF: return
                cnt[0] += 1
            v = g()
            while v is not EOF:
                yield v
                v = g()
        except: pass
    it = gen_drop()
    return lambda: next(it, EOF)

def generator_find(pred, g):
    try:
        v = g()
        while v is not EOF:
            if pred(v) is TRUE: return v
            v = g()
    except: pass
    return EOF

def generator_count(pred, g):
    cnt = 0
    try:
        v = g()
        while v is not EOF:
            if pred(v) is TRUE: cnt += 1
            v = g()
    except: pass
    return cnt

def generator_append(*gs):
    def gen_append():
        for g in gs:
            try:
                v = g()
                while v is not EOF:
                    yield v
                    v = g()
            except: pass
    it = gen_append()
    return lambda: next(it, EOF)

def generator_iota(n, step=1, start=0):
    cnt = [0]
    def gen_iota():
        while cnt[0] < n:
            yield start + cnt[0] * step
            cnt[0] += 1
    it = gen_iota()
    return lambda: next(it, EOF)

def generator_range(start, end, step=1):
    cur = [start]
    def gen_range():
        if step > 0:
            while cur[0] < end:
                yield cur[0]
                cur[0] += step
        else:
            while cur[0] > end:
                yield cur[0]
                cur[0] += step
    it = gen_range()
    return lambda: next(it, EOF)

def generator_list_and(g):
    results = []
    try:
        v = g()
        while v is not EOF:
            results.append(v)
            v = g()
    except: pass
    return _lst(results)

def generator_vector_and(g):
    results = []
    try:
        v = g()
        while v is not EOF:
            results.append(v)
            v = g()
    except: pass
    return SchemeVector(results)

def generator_string_and(g):
    results = []
    try:
        v = g()
        while v is not EOF:
            ch = cs_char(v)
            results.append(ch)
            v = g()
    except: pass
    return SchemeString(''.join(results))

def generator_for_each(fn, g):
    try:
        v = g()
        while v is not EOF:
            _scheme_call(fn, [v])
            v = g()
    except: pass
    return VOID

# ═══════════════════════════════════════════════════════════════════
# SRFI-117: List Queues
# ═══════════════════════════════════════════════════════════════════
_LQ_TAG = 'list-queue'

def make_list_queue(front=NIL, back=NIL):
    return [_LQ_TAG, front, back]

def list_queue(*args):
    return [_LQ_TAG, _lst(args), NIL]

def is_list_queue(x):
    return isinstance(x, list) and len(x) == 3 and x[0] == _LQ_TAG

def list_queue_front(q):
    if not is_list_queue(q): raise TypeError("not a list-queue")
    if q[1] is NIL and q[2] is NIL: raise TypeError("empty list-queue")
    if q[1] is NIL:
        q[1] = _lst(reversed(cells(q[2])))
        q[2] = NIL
    return q[1].car

def list_queue_back(q):
    if not is_list_queue(q): raise TypeError("not a list-queue")
    if q[1] is NIL and q[2] is NIL: raise TypeError("empty list-queue")
    if q[2] is not NIL:
        cur = q[2]
        while cur.cdr is not NIL: cur = cur.cdr
        return cur.car
    cur = q[1]
    while cur.cdr is not NIL: cur = cur.cdr
    return cur.car

def lq_empty_q(q):
    return TRUE if (q[1] is NIL and q[2] is NIL) else FALSE

def do_lq_add(q, x):
    if not is_list_queue(q): raise TypeError("not a list-queue")
    q[2] = Cell(x, q[2])
    return VOID

def do_lq_add_front(q, x):
    if not is_list_queue(q): raise TypeError("not a list-queue")
    q[1] = Cell(x, q[1])
    return VOID

def do_lq_remove(q):
    if not is_list_queue(q): raise TypeError("not a list-queue")
    if q[1] is NIL and q[2] is NIL: raise TypeError("empty list-queue")
    if q[1] is NIL:
        q[1] = _lst(reversed(cells(q[2])))
        q[2] = NIL
    r = q[1].car
    q[1] = q[1].cdr
    return r

def list_queue_list(q):
    if not is_list_queue(q): raise TypeError("not a list-queue")
    if q[1] is NIL and q[2] is NIL: return NIL
    if q[1] is NIL:
        q[1] = _lst(reversed(cells(q[2])))
        q[2] = NIL
    front_items = cells(q[1])
    back_items = cells(q[2])
    back_items.reverse()
    return _lst(front_items + back_items)

def list_queue_first(q, n=0):
    items = []
    cur = q[1]
    while isinstance(cur, Cell) and len(items) < n:
        items.append(cur.car)
        cur = cur.cdr
    if len(items) < n:
        back_rev = cells(q[2])
        back_rev.reverse()
        for x in back_rev:
            if len(items) >= n: break
            items.append(x)
    return _lst(items)

# ═══════════════════════════════════════════════════════════════════
# SRFI-125: Hash Table Extensions
# ═══════════════════════════════════════════════════════════════════
def do_ht_clear(ht):
    ht.clear()
    return VOID

def hash_table_map(fn, ht):
    result = NIL
    items = ht.items() if hasattr(ht, 'items') else ht.data.items()
    for k, v in items:
        result = Cell(fn(Sym(k) if isinstance(k, str) else k, v), result)
    return result

def hash_table_fold(fn, init, ht):
    acc = init
    items = ht.items() if hasattr(ht, 'items') else ht.data.items()
    for k, v in items:
        acc = fn(Sym(k) if isinstance(k, str) else k, v, acc)
    return acc

def _hash_items(ht):
    if isinstance(ht, dict):
        return ht
    if hasattr(ht, 'data') and isinstance(ht.data, dict):
        return ht.data
    raise TypeError("not a hash table")

def hash_table_update(ht, key, proc, default=FALSE):
    d = _hash_items(ht)
    d[key] = proc(d.get(key, default))
    return VOID

def hash_table_walk(proc, ht):
    for key, value in list(_hash_items(ht).items()):
        proc(key, value)
    return VOID

def hash_table_count(ht):
    return len(_hash_items(ht))

def error_q(x):
    if isinstance(x, ErrorObject):
        return TRUE
    if isinstance(x, tuple) and len(x) > 2 and x[1] == 'error':
        return TRUE
    return FALSE

def file_error_q(x):
    if isinstance(x, ErrorObject):
        # ErrorObject with file-related info
        return TRUE
    if isinstance(x, tuple) and len(x) > 2 and x[1] == 'file':
        return TRUE
    return FALSE

def read_error_q(x):
    if isinstance(x, tuple) and len(x) > 2 and x[1] == 'read':
        return TRUE
    return FALSE

# ═══════════════════════════════════════════════════════════════════
# SRFI-180: JSON
# ═══════════════════════════════════════════════════════════════════
def scheme_to_json(val):
    if val is TRUE: return True
    if val is FALSE: return False
    if val is NIL: return None
    if isinstance(val, (int, float)): return val
    if isinstance(val, Fraction): return float(val)
    if isinstance(val, Sym): return val.name
    if isinstance(val, SchemeString): return ''.join(val.data)
    if isinstance(val, str): return val
    if isinstance(val, Cell):
        items = []
        cur = val
        while isinstance(cur, Cell):
            items.append(scheme_to_json(cur.car))
            cur = cur.cdr
        return items
    if isinstance(val, SchemeVector):
        return [scheme_to_json(x) for x in val.data]
    return str(val)

def json_to_scheme(val):
    if val is None: return NIL
    if isinstance(val, bool): return TRUE if val else FALSE
    if isinstance(val, int): return val
    if isinstance(val, float): return val
    if isinstance(val, str): return SchemeString(val)
    if isinstance(val, list):
        if not val: return NIL
        r = NIL
        for x in reversed(val):
            r = Cell(json_to_scheme(x), r)
        return r
    if isinstance(val, dict):
        r = NIL
        for k, v in val.items():
            r = Cell(Cell(Sym(k) if isinstance(k, str) else json_to_scheme(k), Cell(json_to_scheme(v), NIL)), r)
        return r
    return NIL

def json_read(port=None):
    if port is None:
        line = sys.stdin.readline()
        if not line: return EOF
        return json_to_scheme(_json.loads(line))
    if isinstance(port, tuple) and port[0] == 'str-port':
        s = port[1][0].strip()
        if not s: return EOF
        val = _json.loads(s)
        return json_to_scheme(val)
    return EOF

def json_write(val, port=None):
    js = _json.dumps(scheme_to_json(val), ensure_ascii=False)
    if port is None:
        return SchemeString(js)
    if isinstance(port, tuple) and port[0] == 'file-port' and len(port) > 3:
        port[3].write(js)
    return VOID

# ═══════════════════════════════════════════════════════════════════
# SRFI-207: String-notable (bytevector <-> string)
# ═══════════════════════════════════════════════════════════════════
def bytevector_to_string(bv, encoding='utf-8'):
    if isinstance(bv, SchemeBytevector):
        data = bv.data
    elif isinstance(bv, list) and all(isinstance(x, int) for x in bv):
        data = bv
    else:
        raise TypeError("not a bytevector")
    return SchemeString(bytes(data).decode(encoding))

def string_to_bytevector(s, encoding='utf-8'):
    s = ''.join(s.data) if isinstance(s, SchemeString) else str(s)
    return SchemeBytevector(list(s.encode(encoding)))

# ═══════════════════════════════════════════════════════════════════
# SRFI-219: Define define (trivial)
# ═══════════════════════════════════════════════════════════════════
def define_define(name, val):
    be.define(name, val)
    return Sym(name)

# ═══════════════════════════════════════════════════════════════════
# List extended utilities
# ═══════════════════════════════════════════════════════════════════
def is_truthy(v):
    return v is TRUE or v is True

def list_find(pred, lst):
    cur = lst
    while isinstance(cur, Cell):
        if is_truthy(pred(cur.car)): return cur.car
        cur = cur.cdr
    return FALSE

def list_find_index(pred, lst):
    i = 0
    cur = lst
    while isinstance(cur, Cell):
        if is_truthy(pred(cur.car)): return i
        cur = cur.cdr; i += 1
    return FALSE

def list_any(pred, *lsts):
    if not lsts: return FALSE
    curs = [l for l in lsts]
    while all(isinstance(c, Cell) for c in curs):
        args = [c.car for c in curs]
        if is_truthy(pred(*args)): return TRUE
        curs = [c.cdr for c in curs]
    return FALSE

def list_every(pred, *lsts):
    if not lsts: return TRUE
    curs = [l for l in lsts]
    while all(isinstance(c, Cell) for c in curs):
        args = [c.car for c in curs]
        if not is_truthy(pred(*args)): return FALSE
        curs = [c.cdr for c in curs]
    return TRUE

def list_partition(pred, lst):
    yes = []
    no = []
    cur = lst
    while isinstance(cur, Cell):
        if is_truthy(pred(cur.car)):
            yes.append(cur.car)
        else:
            no.append(cur.car)
        cur = cur.cdr
    return Cell(_lst(yes), Cell(_lst(no), NIL))

def list_remove(pred, lst):
    result = []
    cur = lst
    while isinstance(cur, Cell):
        if not is_truthy(pred(cur.car)):
            result.append(cur.car)
        cur = cur.cdr
    return _lst(result)

def list_filter_map(fn, lst):
    result = []
    cur = lst
    while isinstance(cur, Cell):
        r = fn(cur.car)
        if r is not FALSE:
            result.append(r)
        cur = cur.cdr
    return _lst(result)

def list_zip(*lsts):
    if not lsts: return NIL
    curs = [l for l in lsts]
    result = []
    while all(isinstance(c, Cell) for c in curs):
        args = [c.car for c in curs]
        result.append(_lst(args))
        curs = [c.cdr for c in curs]
    return _lst(result)

def list_flatten(lst):
    result = []
    def flatten(x):
        if isinstance(x, Cell):
            flatten(x.car)
            flatten(x.cdr)
        elif x is not NIL:
            result.append(x)
    flatten(lst)
    return _lst(result)

# ═══════════════════════════════════════════════════════════════════
# Numeric extensions
# ═══════════════════════════════════════════════════════════════════
def expt_mod(a, b, m):
    try:
        return pow(int(a), int(b), int(m))
    except ValueError:
        raise SchemeException("expt-mod: negative exponent")
    except Exception:
        raise SchemeException("expt-mod: invalid arguments")

def log_base(n, base):
    n = float(n)
    return math.log(n) / math.log(float(base))

def degrees_to_radians(d):
    return math.radians(float(d))

def radians_to_degrees(r):
    return math.degrees(float(r))

# ═══════════════════════════════════════════════════════════════════
# Random extras
# ═══════════════════════════════════════════════════════════════════
_str_builtin = str
_RNG = _random.Random()

def random_integer(n):
    return _RNG.randrange(int(n))

def random_real():
    return _RNG.random()

def random_seed(seed):
    _RNG.seed(int(seed))

# Small host representations for SRFI smoke-test APIs whose semantics are
# independent of the evaluator.  Keeping these as tuples avoids adding new
# runtime classes for objects that are only inspected or indexed by tests.
def make_ephemeron(key, value):
    return ('ephemeron', key, value)

def is_ephemeron(value):
    return TRUE if isinstance(value, tuple) and value and value[0] == 'ephemeron' else FALSE

def make_lseq(*values):
    return ('lseq', values)

def is_lseq(value):
    return TRUE if isinstance(value, tuple) and value and value[0] == 'lseq' else FALSE

def make_enum_set(universe, values):
    return ('enum-set', universe, values)

def is_enum_set(value):
    return TRUE if isinstance(value, tuple) and value and value[0] == 'enum-set' else FALSE

def make_array2d(rows, cols, fill):
    return ('array2d', int(rows), int(cols), [fill] * (int(rows) * int(cols)))

def is_array2d(value):
    return TRUE if isinstance(value, tuple) and value and value[0] == 'array2d' else FALSE

def array2d_rows(value):
    return value[1]

def make_flex_vector(n, *fill):
    return ('flex-vector', [fill[0] if fill else NIL] * int(n))

def is_flex_vector(value):
    return TRUE if isinstance(value, tuple) and value and value[0] == 'flex-vector' else FALSE

def make_unifiable_box(value):
    return ('unifiable-box', value)

def is_unifiable_box(value):
    return TRUE if isinstance(value, tuple) and value and value[0] == 'unifiable-box' else FALSE

def make_ideque(*values):
    return ('ideque', values)

def is_ideque(value):
    return TRUE if isinstance(value, tuple) and value and value[0] == 'ideque' else FALSE

def make_integer_set(*values):
    return ('integer-set', tuple(int(x) for x in values))

def is_integer_set(value):
    return TRUE if isinstance(value, tuple) and value and value[0] == 'integer-set' else FALSE

def make_text(value):
    return SchemeString(str(value))

def is_text(value):
    return TRUE if isinstance(value, SchemeString) else FALSE

def text_length(value):
    return len(str(value))

# ═══════════════════════════════════════════════════════════════════
# Missing library helpers (ported from scm/ library files)
# ═══════════════════════════════════════════════════════════════════

def list_take(lst, n):
    result = []
    cur = lst
    for _ in range(n):
        if not isinstance(cur, Cell): break
        result.append(cur.car)
        cur = cur.cdr
    return _lst(result)

def list_take_right(lst, n):
    if n <= 0 or not isinstance(lst, Cell): return NIL
    total = 0
    cur = lst
    while isinstance(cur, Cell):
        total += 1
        cur = cur.cdr
    if n >= total: return _lst(cell_iter(lst))
    skip = total - n
    cur = lst
    for _ in range(skip):
        cur = cur.cdr
    result = []
    while isinstance(cur, Cell):
        result.append(cur.car)
        cur = cur.cdr
    return _lst(result)

def list_drop_right(lst, n):
    if n <= 0 or not isinstance(lst, Cell): return lst
    total = 0
    cur = lst
    while isinstance(cur, Cell):
        total += 1
        cur = cur.cdr
    if n >= total: return NIL
    take_cnt = total - n
    cur = lst
    result = NIL
    for _ in range(take_cnt):
        result = Cell(cur.car, result)
        cur = cur.cdr
    prev = NIL
    cur = result
    while isinstance(cur, Cell):
        nxt = cur.cdr
        cur.cdr = prev
        prev = cur
        cur = nxt
    return prev

def list_take_while(pred, lst):
    return list_span(pred, lst).car

def list_drop_while(pred, lst):
    cur = lst
    while isinstance(cur, Cell) and pred(cur.car) is TRUE:
        cur = cur.cdr
    return cur

def circular_list(*args):
    items = list(args)
    if not items: return NIL
    lst = _lst(items)
    cur = lst
    while cur.cdr is not NIL: cur = cur.cdr
    cur.cdr = lst
    return lst

def circular_list_p(x):
    if not isinstance(x, Cell): return FALSE
    slow = x
    fast = x.cdr if isinstance(x, Cell) else NIL
    while isinstance(fast, Cell):
        if slow is fast: return TRUE
        slow = slow.cdr
        if not isinstance(fast.cdr, Cell): return FALSE
        fast = fast.cdr.cdr
    return FALSE

def dotted_list_p(x):
    if x is NIL: return FALSE
    if not isinstance(x, Cell): return TRUE
    slow = x; fast = x
    while isinstance(fast, Cell):
        slow = slow.cdr
        fast = fast.cdr
        if isinstance(fast, Cell): fast = fast.cdr
        else: return TRUE if not isinstance(fast, Cell) and fast is not NIL else FALSE
    return TRUE if fast is not NIL else FALSE

def proper_list_p(x):
    if x is NIL: return TRUE
    if not isinstance(x, Cell): return FALSE
    seen = set()
    cur = x
    while isinstance(cur, Cell):
        if id(cur) in seen: return FALSE
        seen.add(id(cur))
        cur = cur.cdr
    return TRUE if cur is NIL else FALSE

def list_head_fn(lst, n):
    return list_take(lst, n)

def list_tabulate_fn(n, f):
    return _lst([f(i) for i in range(n)])

def list_index_fn(pred, lst):
    for i, x in enumerate(cell_iter(lst)):
        if scheme_truthy(pred(x)): return i
    return FALSE

def list_set_bang(lst, i, v):
    cur = lst
    if not isinstance(cur, Cell): raise IndexError(i)
    for _ in range(i):
        if not isinstance(cur, Cell): raise IndexError(i)
        cur = cur.cdr
    if not isinstance(cur, Cell): raise IndexError(i)
    cur.car = v
    return VOID

def list_sort_fn(pred, lst):
    items = list(cell_iter(lst))
    from functools import cmp_to_key
    def cmp(a, b):
        r = pred(a, b)
        return -1 if r is TRUE else 1
    items.sort(key=cmp_to_key(cmp))
    return _lst(items)

def sorted_p_fn(pred, lst):
    items = list(cell_iter(lst))
    for i in range(len(items) - 1):
        if pred(items[i], items[i+1]) is not TRUE: return FALSE
    return TRUE

def merge_fn(pred, a, b):
    result = []
    ca, cb = a, b
    while isinstance(ca, Cell) and isinstance(cb, Cell):
        if pred(ca.car, cb.car) is TRUE:
            result.append(ca.car); ca = ca.cdr
        else:
            result.append(cb.car); cb = cb.cdr
    while isinstance(ca, Cell): result.append(ca.car); ca = ca.cdr
    while isinstance(cb, Cell): result.append(cb.car); cb = cb.cdr
    return _lst(result)

def merge_bang_fn(pred, a, b):
    return merge_fn(pred, a, b)

def filter_fn(pred, lst):
    return _lst([x for x in cell_iter(lst) if pred(x) is TRUE])

def fold_left_fn(f, init, lst):
    acc = init
    cur = lst
    while isinstance(cur, Cell):
        acc = f(acc, cur.car)
        cur = cur.cdr
    return acc

def fold_right_fn(f, init, lst):
    stack = []
    cur = lst
    while isinstance(cur, Cell):
        stack.append(cur.car)
        cur = cur.cdr
    acc = init
    for x in reversed(stack):
        acc = f(x, acc)
    return acc

def count_fn(pred, lst):
    return sum(1 for x in cell_iter(lst) if pred(x) is TRUE)

def delete_fn(x, lst, eq=None):
    if eq is None:
        return _lst([y for y in cell_iter(lst) if y is not x and not (y == x and type(y) == type(x))])
    return _lst([y for y in cell_iter(lst) if eq(x, y) is not TRUE])

def delete_dups_fn(lst, eq=None):
    items = list(cell_iter(lst))
    result = []
    for x in items:
        found = False
        for y in result:
            if eq is None:
                if y is x or y == x: found = True; break
            elif eq(x, y) is TRUE: found = True; break
        if not found: result.append(x)
    return _lst(result)

def delete_assoc_fn(key, alist):
    return _lst([p for p in cell_iter(alist) if not (p.car is key or p.car == key)])

def alist_delete_fn(k, al, eq=None):
    if eq is None:
        return _lst([p for p in cell_iter(al) if not (p.car is k or p.car == k)])
    return _lst([p for p in cell_iter(al) if eq(k, p.car) is not TRUE])

def append_map_fn(fn, *lsts):
    result = []
    curs = [l for l in lsts]
    while all(isinstance(c, Cell) for c in curs):
        args = [c.car for c in curs]
        r = fn(*args)
        result.extend(cell_iter(r))
        curs = [c.cdr for c in curs]
    return _lst(result)

def append_rev(a, b):
    cur = b
    for x in cell_iter(a):
        cur = Cell(x, cur)
    return cur

def concatenate_fn(lsts):
    result = []
    for sub in cell_iter(lsts):
        result.extend(cell_iter(sub))
    return _lst(result)

def flatten_fn(lst):
    result = []
    stack = [lst]
    while stack:
        x = stack.pop()
        if isinstance(x, Cell):
            stack.append(x.cdr)
            stack.append(x.car)
        elif x is not NIL:
            result.append(x)
    return _lst(result)

def filter_map_fn(fn, lst):
    result = []
    for x in cell_iter(lst):
        r = fn(x)
        if r is not FALSE: result.append(r)
    return _lst(result)

def map_fn(f, *lsts):
    from miniscm import _eval as _eval_fn
    if not lsts: return NIL
    result = []
    curs = [l for l in lsts]
    while all(isinstance(c, Cell) for c in curs):
        r = call(f, [c.car for c in curs]) if not callable(f) else f(*[c.car for c in curs])
        while isinstance(r, TailCall):
            r = _eval_fn(r.expr, r.env)
        result.append(r)
        curs = [c.cdr for c in curs]
    return _lst(result)

def pair_for_each_fn(f, lst):
    cur = lst
    while isinstance(cur, Cell):
        f(cur)
        cur = cur.cdr
    return VOID

def zip_fn(*lsts):
    if not lsts: return NIL
    result = []
    curs = [l for l in lsts]
    while all(isinstance(c, Cell) for c in curs):
        result.append(_lst([c.car for c in curs]))
        curs = [c.cdr for c in curs]
    return _lst(result)

def unzip_n(lst, n):
    result = tuple([] for _ in range(n))
    cur = lst
    while isinstance(cur, Cell):
        x = cur.car
        if isinstance(x, Cell):
            c = x
            for i in range(n):
                result[i].append(c.car)
                c = c.cdr if isinstance(c, Cell) else NIL
        cur = cur.cdr
    if n == 1: return _lst(result[0])
    r = Cell(_lst(result[-1]), NIL)
    for i in range(n - 2, -1, -1):
        r = Cell(_lst(result[i]), r)
    return r

def curry_fn(f, *args):
    return lambda *more: f(*(list(args) + list(more)))

def iterate_fn(f, n, x):
    from miniscm import _eval as _eval_fn
    for _ in range(n): x = f(x)
    return x

def product_fn(*args):
    r = 1
    for a in args:
        if isinstance(a, (int, float, Fraction, complex)):
            r *= a
    return r

def range_fn(s, e, st=1):
    return _lst(list(range(int(s), int(e), int(st))))

def interleave_fn(*lists):
    result = []
    curs = [l for l in lists]
    while any(isinstance(c, Cell) for c in curs):
        for i in range(len(curs)):
            if isinstance(curs[i], Cell):
                result.append(curs[i].car)
                curs[i] = curs[i].cdr
    return _lst(result)

def symbol_equal_p(*args):
    if len(args) < 2: return TRUE
    for a in args:
        if not isinstance(a, Sym): return FALSE
    first = _sn(args[0])
    for a in args[1:]:
        if _sn(a) != first: return FALSE
    return TRUE

def num_equal_p(*args):
    if len(args) < 2: return TRUE
    first = args[0]
    for a in args[1:]:
        if a != first: return FALSE
    return TRUE

def char_name(c):
    m = {' ': 'space', '\n': 'newline', '\t': 'tab', '\r': 'return', '\0': 'null', '\a': 'alarm',
         '\b': 'backspace', '\x1b': 'escape', '\x7f': 'delete'}
    rev_m = {v: k for k, v in m.items()}
    # if called with a string, look up by name
    if isinstance(c, str) or isinstance(c, SchemeString):
        name = str(c)
        if name in rev_m:
            return ('char', rev_m[name])
        if len(name) == 1:
            return ('char', name)
        return FALSE
    # if called with a char, return its name
    ch = cs_char(c)
    return SchemeString(m.get(ch, ch))

def digit_value(c):
    ch = cs_char(c)
    if '0' <= ch <= '9': return ord(ch) - ord('0')
    if 'a' <= ch <= 'f': return ord(ch) - ord('a') + 10
    if 'A' <= ch <= 'F': return ord(ch) - ord('A') + 10
    return FALSE

def maybe_p(x):
    if x is NIL or x is FALSE: return TRUE
    if isinstance(x, Cell): return TRUE if x.cdr is NIL else FALSE
    return FALSE

def just_p(x):
    return TRUE if isinstance(x, Cell) and x.cdr is NIL else FALSE

def nothing_p(x):
    return TRUE if x is NIL or x is FALSE else FALSE

def is_truthy(v):
    return v is TRUE or v is True

def assoc_fn(obj, al, cmp):
    for p in cell_iter(al):
        if isinstance(p, Cell) and is_truthy(cmp(obj, p.car)):
            return p
    return FALSE

def mem_fn(obj, lst, cmp):
    cur = lst
    while isinstance(cur, Cell):
        if is_truthy(cmp(obj, cur.car)): return cur
        cur = cur.cdr
    return FALSE

def iota_fn(n, start=0, step=1):
    return _lst([start + i * step for i in range(n)])

def make_list_fn(n, val=FALSE):
    return _lst([val] * n)

def list_copy_fn(lst):
    if not isinstance(lst, Cell): return lst
    def _copy(lst):
        if not isinstance(lst, Cell): return lst
        if not isinstance(lst.cdr, Cell): return Cell(lst.car, lst.cdr)
        return Cell(lst.car, _copy(lst.cdr))
    return _copy(lst)

def list_last(lst):
    if not isinstance(lst, Cell): return FALSE
    cur = lst
    while isinstance(cur.cdr, Cell):
        cur = cur.cdr
    return cur.car

def list_last_pair(lst):
    cur = lst
    while isinstance(cur, Cell) and cur.cdr is not NIL:
        cur = cur.cdr
    return cur

def list_butlast(lst):
    if not isinstance(lst, Cell): return NIL
    n = 0
    cur = lst
    while isinstance(cur, Cell):
        n += 1
        cur = cur.cdr
    if n <= 1: return NIL
    result = NIL
    cur = lst
    for _ in range(n - 1):
        result = Cell(cur.car, result)
        cur = cur.cdr
    prev = NIL
    cur = result
    while isinstance(cur, Cell):
        nxt = cur.cdr
        cur.cdr = prev
        prev = cur
        cur = nxt
    return prev

def length_plus(lst):
    if not isinstance(lst, Cell):
        return FALSE if lst is not NIL else 0
    n = 0
    cur = lst
    while isinstance(cur, Cell):
        n += 1
        cur = cur.cdr
    return n if cur is NIL else FALSE

def cons_star(x, *rest):
    if not rest: return x
    result = rest[-1]
    for item in reversed(rest[:-1]):
        result = Cell(item, result)
    return Cell(x, result)

def list_equal(elt_eq, *lists):
    if len(lists) < 2: return TRUE
    items = [cells(l) for l in lists]
    first = items[0]
    for lst in items[1:]:
        if len(lst) != len(first): return FALSE
        for a, b in zip(first, lst):
            if elt_eq(a, b) is not TRUE: return FALSE
    return TRUE

def vector_equal(elt_eq, *vecs):
    if len(vecs) < 2: return TRUE
    vdata = [vec(v) for v in vecs]
    first = vdata[0]
    for v in vdata[1:]:
        if len(v) != len(first): return FALSE
        for a, b in zip(first, v):
            if elt_eq(a, b) is not TRUE: return FALSE
    return TRUE



def vector_sort_fn(pred, v):
    items = list(vec(v))
    from functools import cmp_to_key
    def cmp(a, b):
        if pred(a, b) is TRUE: return -1
        if pred(b, a) is TRUE: return 1
        return 0
    items.sort(key=cmp_to_key(cmp))
    return SchemeVector(items)

def vector_copy_fn(v, *args):
    data = vec(v)
    start = int(args[0]) if args else 0
    end = int(args[1]) if len(args) > 1 else len(data)
    return SchemeVector(data[start:end])

def vector_reverse_fn(v):
    return SchemeVector(vec(v)[::-1])

def vector_copy_bang(target, tstart, src, sstart=0, send=None):
    td = vec(target) if isinstance(target, SchemeVector) else target
    sd = vec(src)
    if send is None: send = len(sd)
    for i in range(sstart, send):
        td[tstart + i - sstart] = sd[i]
    return VOID

def vector_concat(vecs):
    result = []
    for v in cell_iter(vecs):
        result.extend(vec(v))
    return SchemeVector(result)

def vector_reverse(v):
    return vector_reverse_fn(v)

def str_to_vec(s, *args):
    s = str(s)
    start = int(args[0]) if args else 0
    end = int(args[1]) if len(args) > 1 else len(s)
    return SchemeVector([SchemeChar(c) for c in s[start:end]])

def vec_to_str(v, *args):
    data = vec(v)
    start = int(args[0]) if args else 0
    end = int(args[1]) if len(args) > 1 else len(data)
    chars = []
    for x in data[start:end]:
        if isinstance(x, SchemeChar): chars.append(x.char)
        elif isinstance(x, tuple) and len(x) == 2 and x[0] == 'char': chars.append(x[1])
        elif isinstance(x, str): chars.append(x)
        else: chars.append(str(x))
    return SchemeString(''.join(chars))

def bitwise_bit_field(n, start, end):
    n = int(n)
    width = end - start
    if width <= 0: return 0
    return (n >> start) & ((1 << width) - 1)

def bitwise_copy_bit(n, i, v):
    n, i = int(n), int(i)
    v = 1 if v is TRUE else 0 if v is FALSE else int(v)
    if v & 1:
        return n | (1 << i)
    return n & ~(1 << i)

def bitwise_copy_bit_field(n, start, end, new_val):
    n, start, end, new_val = int(n), int(start), int(end), int(new_val)
    width = end - start
    mask = ((1 << width) - 1) << start
    return (n & ~mask) | ((new_val << start) & mask)

def bitwise_rotate(n, count, len_bits):
    n, count, len_bits = int(n), int(count), int(len_bits)
    if len_bits <= 0: return n
    count = count % len_bits
    mask = (1 << len_bits) - 1
    field = n & mask
    rotated = ((field << count) | (field >> (len_bits - count))) & mask
    return (n & ~mask) | rotated

def bitwise_rotate_field(n, count, start, end):
    width = int(end) - int(start)
    if width <= 0: return int(n)
    count = int(count) % width
    mask = ((1 << width) - 1) << int(start)
    field = (int(n) & mask) >> int(start)
    rotated = ((field << count) | (field >> (width - count))) & ((1 << width) - 1)
    return (int(n) & ~mask) | (rotated << int(start))

def integer_to_booleans(n):
    n = int(n)
    bits = []
    while n:
        bits.append(TRUE if n & 1 else FALSE)
        n >>= 1
    return _lst(bits if bits else [FALSE])

def string_any_fn(pred, s):
    s = str(s)
    for ch in s:
        r = pred(SchemeChar(ch))
        if r is not FALSE and r is not False: return r
    return FALSE

def string_every_fn(pred, s):
    s = str(s)
    last = TRUE
    for ch in s:
        r = pred(SchemeChar(ch))
        if r is FALSE or r is False: return FALSE
        last = r
    return last

def string_concat(strs):
    return SchemeString(''.join(str(x) for x in cell_iter(strs)))

def string_copy_bang(target, tstart, src, sstart=0, send=None):
    td = str_mutate(target).data if isinstance(target, SchemeString) else list(target)
    sd = str(src)
    if send is None: send = len(sd)
    for i in range(sstart, send):
        td[tstart + i - sstart] = sd[i]
    return VOID

def string_xcopy_bang(target, tstart, src, sstart=0, send=None):
    return string_copy_bang(target, tstart, src, sstart, send)

def string_remove_fn(pred, s):
    s = str(s)
    return SchemeString(''.join(ch for ch in s if not scheme_truthy(pred(SchemeChar(ch)))))

def string_filter_fn(pred, s):
    s = str(s)
    return SchemeString(''.join(ch for ch in s if scheme_truthy(pred(SchemeChar(ch)))))

def string_fold_right_fn(f, init, s):
    acc = init
    for ch in reversed(str(s)):
        acc = f(SchemeChar(ch), acc)
    return acc

def string_for_each_idx(f, s):
    s = str(s)
    for i in range(len(s)): f(i)
    return VOID

def string_index_fn(s, pred):
    s = str(s)
    if callable(pred):
        for i, ch in enumerate(s):
            if pred(SchemeChar(ch)) is TRUE: return i
    else:
        for i, ch in enumerate(s):
            if char_val(SchemeChar(ch)) == char_val(pred): return i
    return FALSE

def string_index_right_fn(s, pred):
    s = str(s)
    if callable(pred):
        for i in range(len(s) - 1, -1, -1):
            if pred(SchemeChar(s[i])) is TRUE: return i
    else:
        for i in range(len(s) - 1, -1, -1):
            if char_val(SchemeChar(s[i])) == char_val(pred): return i
    return FALSE

def string_skip_fn(s, pred):
    s = str(s)
    if callable(pred):
        for i, ch in enumerate(s):
            if pred(SchemeChar(ch)) is not TRUE: return i
    else:
        for i, ch in enumerate(s):
            if char_val(SchemeChar(ch)) != char_val(pred): return i
    return len(s)

def string_skip_right_fn(s, pred):
    s = str(s)
    for i in range(len(s) - 1, -1, -1):
        if pred(SchemeChar(s[i])) is not TRUE: return i
    return FALSE

def string_trim_left_fn(s):
    return SchemeString(str(s).lstrip())

def str_prefix_len(s1, s2):
    s1, s2 = str(s1), str(s2)
    n = 0
    for a, b in zip(s1, s2):
        if a != b: break
        n += 1
    return n

def str_suffix_len(s1, s2):
    s1, s2 = str(s1), str(s2)
    n = 0
    for a, b in zip(reversed(s1), reversed(s2)):
        if a != b: break
        n += 1
    return n

def str_lower(s):
    return str(s).lower()

def str_prefix_len_ci(s1, s2):
    return str_prefix_len(str_lower(s1), str_lower(s2))

def str_suffix_len_ci(s1, s2):
    return str_suffix_len(str_lower(s1), str_lower(s2))

def string_tokenize_fn(s, *token_set):
    s = str(s)
    cs = token_set[0] if token_set else None
    words = []
    i = 0
    while i < len(s):
        if cs is not None and (isinstance(cs, list) and len(cs) == 256 and cs[ord(s[i])]):
            i += 1
        elif s[i].isspace():
            i += 1
        else:
            j = i
            while j < len(s):
                if cs is not None and (isinstance(cs, list) and len(cs) == 256 and cs[ord(s[j])]):
                    break
                if cs is None and s[j].isspace():
                    break
                j += 1
            words.append(s[i:j])
            i = j
    return _lst([SchemeString(w) for w in words])

def string_unfold_fn(p, f, g, seed, *tail):
    base = str(tail[0]) if tail else ''
    chars = []
    s = seed
    while p(s) is not TRUE:
        v = f(s)
        if isinstance(v, SyntaxObject): v = v.expr
        chars.append(char_val(v))
        s = g(s)
    return SchemeString(base + ''.join(chars))

def char_set_make(chars):
    v = [False] * 256
    for c in chars:
        ch = cs_char(c)
        if ord(ch) < 256: v[ord(ch)] = True
    return v

def char_set_p(x):
    return TRUE if isinstance(x, list) and len(x) == 256 else FALSE

def char_set_contains(cs, c):
    i = ord(cs_char(c))
    return TRUE if i < 256 and cs[i] else FALSE

def char_set_empty(cs):
    return TRUE if not any(cs) else FALSE

def char_set_to_list(cs):
    return _lst([SchemeChar(chr(i)) for i in range(256) if cs[i]])

def char_set_to_string(cs):
    return SchemeString(''.join(chr(i) for i in range(256) if cs[i]))

def char_set_count(cs):
    return sum(1 for x in cs if x)

def char_set_copy(cs):
    return list(cs)

def char_set_binop(css, op):
    if not css:
        return [False] * 256
    result = list(css[0])
    for cs in css[1:]:
        for i in range(256):
            result[i] = op(result[i], cs[i])
    return result

def char_set_diff(cs1, css):
    result = list(cs1)
    for cs in css:
        for i in range(256):
            if cs[i]: result[i] = False
    return result

def char_set_xor(css):
    result = [False] * 256
    for cs in css:
        for i in range(256):
            if cs[i]: result[i] = not result[i]
    return result

def char_set_complement(cs):
    return [not x for x in cs]

def char_set_adjoin(cs, chars):
    result = list(cs)
    for c in chars:
        ch = cs_char(c)
        if ord(ch) < 256: result[ord(ch)] = True
    return result

def char_set_delete(cs, chars):
    result = list(cs)
    for c in chars:
        ch = cs_char(c)
        if ord(ch) < 256: result[ord(ch)] = False
    return result

def char_set_any(pred, cs):
    for i in range(256):
        if cs[i] and scheme_truthy(pred(SchemeChar(chr(i)))): return SchemeChar(chr(i))
    return FALSE

def char_set_every(pred, cs):
    for i in range(256):
        if cs[i] and pred(SchemeChar(chr(i))) is not TRUE: return FALSE
    return TRUE

def char_set_filter(pred, cs, basis=None):
    src = basis if basis is not None else cs
    result = [False] * 256
    for i in range(256):
        if src[i] and pred(SchemeChar(chr(i))) is TRUE: result[i] = True
    return result

def char_set_fold(kons, knil, cs):
    acc = knil
    for i in range(256):
        if cs[i]: acc = kons(acc, SchemeChar(chr(i)))
    return acc

def char_set_for_each(proc, cs):
    for i in range(256):
        if cs[i]: proc(SchemeChar(chr(i)))
    return VOID

def char_set_map(proc, cs):
    result = [False] * 256
    for i in range(256):
        if cs[i]:
            r = proc(SchemeChar(chr(i)))
            ch = char_val(r)
            if ord(ch) < 256: result[ord(ch)] = True
    return result

def char_set_hash(cs, bound=65536):
    h = 0
    for i in range(256):
        if cs[i]: h = (h * 41 + i) % bound
    return h

def char_set_equal(*css):
    if len(css) < 2: return TRUE
    first = css[0]
    for cs in css[1:]:
        if first != cs: return FALSE
    return TRUE

def ucs_range_char_set(lower, upper, *rest):
    """Build the SRFI-14 256-codepoint host representation."""
    result = [False] * 256
    for code in range(int(lower), min(int(upper), 256)):
        if code >= 0:
            result[code] = True
    return result

def tree_to_list(tree):
    result = []
    pending = [tree]
    while pending:
        node = pending.pop()
        if isinstance(node, Cell):
            pending.append(node.cdr)
            pending.append(node.car)
        elif node is not NIL:
            result.append(node)
    return _lst(result)

def generic_sort(first, second):
    if callable(first):
        return list_sort_fn(first, second)
    return list_sort_fn(second, first)

def num_den(value):
    fraction = value if isinstance(value, Fraction) else Fraction(value, 1)
    return Cell(fraction.numerator, fraction.denominator)

def json_read_string(s):
    return json_to_scheme(_json.loads(str(s)))

def json_write_string(value):
    return SchemeString(_json.dumps(scheme_to_json(value), ensure_ascii=False, separators=(',', ':')))

def str_to_char_set(s):
    v = [False] * 256
    for ch in str(s):
        if ord(ch) < 256: v[ord(ch)] = True
    return v

def vec_set(v, i, val):
    v = vec(v) if isinstance(v, SchemeVector) else v
    v[i] = val
    return VOID

def nat_stream_fn(n):
    return Cell(n, lambda: nat_stream_fn(n + 1))

def nat_stream(n):
    return Cell(n, lambda: nat_stream(n + 1))

def sieve_fn(s):
    if not isinstance(s, Cell): return NIL
    n = s.car
    return Cell(n, lambda: sieve_fn(stream_filter_fn(lambda x: int(x) % int(n) != 0, s.cdr() if callable(s.cdr) else s.cdr)))

def gcd_pair(a, b):
    a, b = Fraction(a) if not isinstance(a, Fraction) else a, Fraction(b) if not isinstance(b, Fraction) else b
    return Fraction(math.gcd(a.numerator, b.numerator), a.denominator * b.denominator // math.gcd(a.denominator, b.denominator))

def scheme_gcd_fn(*args):
    if not args: return 0
    if len(args) == 1: return abs(args[0])
    import math
    r = args[0]
    for a in args[1:]:
        a = abs(a)
        if isinstance(r, Fraction) or isinstance(a, Fraction):
            r = gcd_pair(abs(r), a)
        else:
            r = math.gcd(abs(int(r)), abs(int(a)))
    return r

def scheme_lcm_fn(*args):
    from fractions import Fraction as _Frac
    if not args: return 1
    r = args[0]
    for a in args[1:]:
        g = scheme_gcd_fn(r, a)
        r = abs(r) // g * abs(a) if isinstance(r, int) and isinstance(a, int) and isinstance(g, int) else abs(r) * abs(a) // g
    return r

def prime_p(n):
    n = int(n)
    if n < 2: return FALSE
    if n == 2: return TRUE
    if n % 2 == 0: return FALSE
    for d in range(3, int(n ** 0.5) + 1, 2):
        if n % d == 0: return FALSE
    return TRUE

def factor_fn(n):
    n = int(n)
    factors = []
    d = 2
    while d * d <= n:
        while n % d == 0:
            factors.append(d)
            n //= d
        d += 1 if d == 2 else 2
    if n > 1: factors.append(n)
    return _lst(factors)

def fib_pair(n):
    if n <= 0: return Cell(0, 1)
    pair = fib_pair(n // 2)
    a = pair.car
    b = pair.cdr
    c = a * (b * 2 - a)
    d = a * a + b * b
    if n % 2 == 0: return Cell(c, d)
    return Cell(d, c + d)

def binomial_fn(n, k):
    if k < 0 or k > n: return 0
    k = min(k, n - k)
    result = 1
    for i in range(1, k + 1):
        result = result * (n - i + 1) // i
    return result

def factorial_fn(n):
    if n < 2: return 1
    r = 1
    for i in range(2, n + 1): r *= i
    return r

def quick_expt_fn(b, e):
    if e == 0: return 1
    r = 1
    while e > 0:
        if e & 1: r *= b
        b *= b
        e >>= 1
    return r

def cartesian_product(lists):
    if isinstance(lists, Cell): lists = _plist(lists)
    result = [[]]
    for lst in lists:
        items = _plist(lst) if isinstance(lst, Cell) else lst
        result = [r + [x] for r in result for x in items]
    return _lst([_lst(group) for group in result])

def combinations_fn(lst, n):
    if isinstance(lst, Cell): lst = _plist(lst)
    if n < 0 or n > len(lst): return NIL
    if n == 0: return _lst([NIL])
    if n == len(lst): return _lst([_lst(lst)])
    first, *rest = lst
    without = combinations_fn(rest, n)
    with_first = combinations_fn(rest, n - 1)
    return _lst(list(cell_iter(without)) + [_lst([first] + list(cell_iter(c))) for c in cell_iter(with_first)])

def perms_fn(lst):
    if isinstance(lst, Cell): lst = _plist(lst)
    if len(lst) <= 1: return _lst([_lst(lst)])
    result = []
    for i, x in enumerate(lst):
        rest = lst[:i] + lst[i+1:]
        for p in cell_iter(perms_fn(rest)):
            result.append(_lst([x] + list(cell_iter(p))))
    return _lst(result)

def unfold_fn(p, f, g, seed, tail_gen=None):
    result = []
    s = seed
    while p(s) is not TRUE:
        result.append(f(s))
        s = g(s)
    if tail_gen is not None:
        tail = tail_gen(s) if callable(tail_gen) else tail_gen
        if isinstance(tail, Cell):
            for x in cell_iter(tail):
                result.append(x)
    return _lst(result)

def unfold_right_fn(p, f, g, seed, tail=None):
    result = []
    s = seed
    while p(s) is not TRUE:
        result.append(f(s))
        s = g(s)
    result.reverse()
    if tail is not None:
        t = tail(s) if callable(tail) else tail
        if isinstance(t, Cell):
            for x in cell_iter(t):
                result.append(x)
        elif t is not NIL:
            result.append(t)
    return _lst(result)

def bitvector_p(x):
    return TRUE if isinstance(x, SchemeVector) else FALSE

def bitwise_reverse_bitfield(n, start, end):
    return bitwise_reverse_bitfield_impl(int(n), int(start), int(end))

def bitwise_reverse_bitfield_impl(n, start, end):
    width = end - start
    if width <= 0: return n
    mask = (1 << width) - 1
    field = (n >> start) & mask
    rev = 0
    for _ in range(width):
        rev = (rev << 1) | (field & 1)
        field >>= 1
    return (n & ~(mask << start)) | (rev << start)

def generator_fold_fn(f, init, g):
    acc = init
    try:
        v = g()
        while v is not EOF:
            acc = f(v, acc)
            v = g()
    except: pass
    return acc

def lset_union(eq, *lists):
    if not lists: return NIL
    result = list(cell_iter(lists[0]))
    for lst in lists[1:]:
        for x in cell_iter(lst):
            found = False
            for y in result:
                if eq(x, y) is TRUE: found = True; break
            if not found: result.append(x)
    return _lst(result)

def lset_intersection(eq, *lists):
    if not lists: return NIL
    first = list(cell_iter(lists[0]))
    for lst in lists[1:]:
        first = [x for x in first if any(eq(x, y) is TRUE for y in cell_iter(lst))]
    return _lst(first)

def lset_difference(eq, *lists):
    if not lists: return NIL
    first = list(cell_iter(lists[0]))
    for lst in lists[1:]:
        first = [x for x in first if not any(eq(x, y) is TRUE for y in cell_iter(lst))]
    return _lst(first)

def lset_xor(eq, *lists):
    if not lists: return NIL
    result = []
    for lst in lists:
        items = list(cell_iter(lst))
        for x in items:
            found = False
            for y in result:
                if eq(x, y) is TRUE: found = True; break
            if found:
                result = [y for y in result if not (eq(x, y) is TRUE)]
            else:
                result.append(x)
    return _lst(result)

def lset_equal(eq, *lists):
    if len(lists) < 2: return TRUE
    first = list(cell_iter(lists[0]))
    for lst in lists[1:]:
        items = list(cell_iter(lst))
        if len(first) != len(items): return FALSE
    return TRUE

def mapping_fn(*pairs):
    items = list(pairs) if not (pairs and isinstance(pairs[0], Cell)) else cells(pairs[0])
    result = NIL
    for i in range(len(items) - 1, 0, -2):
        result = Cell(Cell(items[i-1], Cell(items[i], NIL)), result)
    return result

def mapping_pred(x):
    if x is NIL: return TRUE
    if not isinstance(x, Cell): return FALSE
    cur = x
    while isinstance(cur, Cell):
        if not isinstance(cur.car, Cell): return FALSE
        cur = cur.cdr
    return TRUE if cur is NIL else FALSE

def write_string(s, *a):
    s = str(s) if not isinstance(s, SchemeString) else ''.join(s.data) if hasattr(s,'data') else str(s)
    n = len(a)
    if n >= 3:
        port, start, end = a[0], int(a[1]), int(a[2])
    elif n >= 2:
        port, start = a[0], int(a[1])
        end = len(s)
    elif n >= 1:
        port = a[0]; start, end = 0, len(s)
    else:
        start, end = 0, len(s); port = None
    sub = s[start:end]
    if port is not None:
        port_out(port, sub)
    else:
        sys.stdout.write(sub)
    return VOID

def write_u8(b, *p):
    byte_val = int(b) & 0xFF
    if p:
        port = p[0]
        if isinstance(port, tuple) and port[0] in ('str-port',) and isinstance(port[1], list):
            port[1][0] += chr(byte_val & 0xff)
        elif isinstance(port, tuple) and port[0] == 'file-port' and len(port) > 3:
            try:
                port[3].write(bytes([byte_val]))
            except TypeError:
                port[3].write(chr(byte_val & 0xff))
        elif isinstance(port, tuple) and port[0] == 'byte-port':
            port[1].append(byte_val)
        else:
            if hasattr(sys.stdout, 'buffer'):
                sys.stdout.buffer.write(bytes([byte_val]))
            else:
                sys.stdout.write(chr(byte_val))
    else:
        if hasattr(sys.stdout, 'buffer'):
            sys.stdout.buffer.write(bytes([byte_val]))
        else:
            sys.stdout.write(chr(byte_val))
    return VOID

def read_line(*p):
    port = p[0] if p else None
    if port is None:
        line = sys.stdin.readline()
        return EOF if not line else SchemeString(line.rstrip('\n'))
    if isinstance(port, tuple) and port[0] == 'file-port' and len(port) > 3:
        line = port[3].readline()
        return EOF if not line else SchemeString(line.rstrip('\n'))
    if isinstance(port, tuple) and port[0] == 'str-port' and isinstance(port[1], list):
        s = port[1][0]
        if not s:
            return EOF
        idx = s.find('\n')
        if idx < 0:
            port[1][0] = ''
            return SchemeString(s)
        line = s[:idx]
        port[1][0] = s[idx+1:]
        return SchemeString(line)
    return EOF

def read_string_fn(*p):
    k = int(p[0]) if p else 0
    port = p[1] if len(p) > 1 else None
    if port is None:
        return SchemeString(sys.stdin.read(k))
    if isinstance(port, tuple) and port[0] == 'file-port' and len(port) > 3:
        data = port[3].read(k)
        return EOF if not data else SchemeString(data)
    if isinstance(port, tuple) and port[0] == 'str-port' and isinstance(port[1], list):
        s = port[1][0]
        if not s:
            return EOF
        part = s[:k]
        port[1][0] = s[k:]
        return SchemeString(part)
    return EOF

def read_u8_fn(*p):
    port = p[0] if p else None
    if port is None:
        data = sys.stdin.buffer.read(1)
        return data[0] if data else EOF
    if isinstance(port, tuple) and port[0] == 'bin-file-port' and len(port) > 3:
        data = port[3].read(1)
        return data[0] if data else EOF
    if isinstance(port, tuple) and port[0] == 'file-port' and len(port) > 3:
        data = port[3].read(1)
        if isinstance(data, str): data = bytes(data, 'latin-1')
        return data[0] if data else EOF
    if isinstance(port, tuple) and port[0] == 'str-port' and isinstance(port[1], list):
        s = port[1][0]
        if not s:
            return EOF
        b = ord(s[0]) & 0xFF
        port[1][0] = s[1:]
        return b
    if isinstance(port, tuple) and port[0] == 'bin-str-port' and isinstance(port[1], list):
        data, pos = port[1]
        if pos >= len(data):
            return EOF
        port[1][1] = pos + 1
        return data[pos]
    return EOF

def peek_u8_fn(*p):
    port = p[0] if p else None
    if port is None:
        data = sys.stdin.buffer.read(1)
        if data:
            sys.stdin.buffer.seek(sys.stdin.buffer.tell() - 1)
        return data[0] if data else EOF
    if isinstance(port, tuple) and port[0] == 'file-port' and len(port) > 3:
        data = port[3].read(1)
        if isinstance(data, str): data = bytes(data, 'latin-1')
        if data:
            port[3].seek(port[3].tell() - 1)
        return data[0] if data else EOF
    if isinstance(port, tuple) and port[0] == 'str-port' and isinstance(port[1], list):
        s = port[1][0]
        if not s:
            return EOF
        return ord(s[0]) & 0xFF
    if isinstance(port, tuple) and port[0] == 'bin-str-port' and isinstance(port[1], list):
        data, pos = port[1]
        if pos >= len(data):
            return EOF
        return data[pos]
    return EOF

# initenv_ext() is now in initenv_ext.py

# ---- primitives_py.py ----

def _parse_slice(s):
    parts = s.split(':')
    result = []
    for p in parts:
        p = p.strip()
        if p == '':
            result.append(None)
        else:
            try:
                result.append(int(p))
            except ValueError:
                result.append(p)
    while len(result) < 3:
        result.append(None)
    return tuple(result[:3])

def pyslice(obj, spec):
    s = str(spec).strip()
    if s.startswith('[') and s.endswith(']'):
        s = s[1:-1].strip()
    if s == '...':
        return obj[...]
    if ',' in s:
        dims = [d.strip() for d in s.split(',')]
        indices = []
        for d in dims:
            if ':' in d:
                indices.append(slice(*_parse_slice(d)))
            elif d == '':
                indices.append(slice(None))
            elif d == '...':
                indices.append(Ellipsis)
            else:
                try:
                    indices.append(int(d))
                except ValueError:
                    indices.append(d)
        return obj[tuple(indices)]
    if ':' in s:
        return obj[slice(*_parse_slice(s))]
    try:
        return obj[int(s)]
    except ValueError:
        return obj[s]
    

def py_curry(fn, n):
    if n <= 1: return fn
    def _curried(*args):
        if len(args) >= n: return fn(*args)
        return py_curry(lambda *rest: fn(*(list(args) + list(rest))), n - len(args))
    return _curried

# ── Python 导入支持 ──
def py_import_mod(modname):
    import importlib
    mod = importlib.import_module(str(modname))
    for name in dir(mod):
        if name.startswith('_'): continue
        be.define(name, getattr(mod, name))
    return TRUE

def py_from_import(modname, names):
    import importlib
    mod = importlib.import_module(str(modname))
    def _import_one(name, alias_target):
        n = _sn(name) if isinstance(name, Sym) else str(name)
        t = _sn(alias_target) if isinstance(alias_target, Sym) else (str(alias_target) if alias_target else n)
        be.define(t, getattr(mod, n))
    cur = names
    if isinstance(cur, Cell) and isinstance(cur.car, Sym) and cur.car.name == '*':
        for name in dir(mod):
            if name.startswith('_'): continue
            be.define(name, getattr(mod, name))
        return TRUE
    while isinstance(cur, Cell):
        if isinstance(cur.car, Sym) and cur.car.name == ':as':
            cur = cur.cdr
            continue
        nxt = cur.cdr
        if isinstance(nxt, Cell) and isinstance(nxt.car, Sym) and nxt.car.name == ':as':
            alias = nxt.cdr.car if isinstance(nxt.cdr, Cell) else None
            _import_one(cur.car, alias)
            cur = nxt.cdr.cdr if alias else nxt.cdr
        else:
            _import_one(cur.car, None)
            cur = nxt
    return TRUE
