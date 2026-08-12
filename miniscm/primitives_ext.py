# primitives_ext.py — R7RS-large 扩展内置过程
# 通过 Python builtin 实现高性能原语，在 miniscm.py 引导时使用 initenv_ext() 注册

import math, sys, json as _json
from fractions import Fraction
from mtypes import (
    SchemeException, Sym, Cell, SchemeString, SchemeChar, SchemeVector, SchemeBytevector,
    Promise, SyntaxObject, ErrorObject, NIL, VOID, EOF, TRUE, FALSE, Env, TailCall,
    _pr, _so, _sn, _plist, _lst, builtin, be
)
from primitives import port_out, scheme_truthy, cell_iter, cells, list_span, str_mutate, stream_filter_fn, cs_char, char_val 
from primitives_first import call, port_out

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

def string_trim(s):
    return SchemeString(str(s).strip())

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
                yield fn(v)
                v = g()
        except: pass
    it = gen_map()
    return lambda: next(it, EOF)

def generator_filter(pred, g):
    def gen_filter():
        try:
            v = g()
            while v is not EOF:
                if pred(v) is TRUE:
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
            fn(v)
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
import random as _random
_str_builtin = str
_RNG = _random.Random()

def random_integer(n):
    return _RNG.randrange(int(n))

def random_real():
    return _RNG.random()

def random_seed(seed):
    _RNG.seed(int(seed))

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
    return SchemeString(''.join(ch for ch in s if pred(SchemeChar(ch)) is not TRUE))

def string_filter_fn(pred, s):
    s = str(s)
    return SchemeString(''.join(ch for ch in s if pred(SchemeChar(ch)) is TRUE))

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
    return EOF

# initenv_ext() is now in initenv_ext.py
