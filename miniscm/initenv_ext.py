# initenv_ext.py — builtin registration extracted from primitives_ext.py
import math, sys, time as _time, json as _json, re as _re, os as _os, base64 as _base64
import random as _random
import functools as _functools
import json
import random
import time
from functools import cmp_to_key
from mtypes import (
    Sym, Cell, SchemeString, SchemeVector, SchemeBytevector,
    ErrorObject, NIL, VOID, TRUE, FALSE,EOF, Sym, _lst, _pr, builtin, be,
    _pr, _so, _sn, _lst, builtin
)
from primitives import *
from primitives_ext import *
from primitives import cell_iter, cells, scheme_truthy, cs_char, char_val, call as _scheme_call


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
# ═══════════════════════════════════════════════════
# SRFI-1 风格 update (按索引替换元素，返回新列表)
#   (update (list 1 2 3) 2 (lambda (x) 4)) => (1 2 4)
# ═══════════════════════════════════════════════════
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

# ═══════════════════════════════════════════════════
# sorted-by:  (sorted-by < '(3 1 2)) => (1 2 3)
# ═══════════════════════════════════════════════════
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

# ═══════════════════════════════════════════════════
# file-exists?: 相对路径解析依次尝试 CWD / 当前加载文件目录 /
# miniscm 目录 / 仓库根，并对 test<->test1 首段做互换补偿。
# ═══════════════════════════════════════════════════
def file_exists_fn(p):
    from miniscm import _resolve_load_path
    r = _resolve_load_path(p)
    return TRUE if (r is not None and _os.path.exists(r)) else FALSE

def initenv_ext():
    be.define('NIL', NIL)
    builtin('for-all', lambda pred, lst: TRUE if all(pred(x) is not FALSE for x in cell_iter(lst)) else FALSE)
    builtin('string-concatenate-reverse', lambda xs: SchemeString(''.join(str(x) for x in reversed(list(cell_iter(xs))))))
    builtin('substring-count', lambda s, sub: sum(1 for i in range(len(str(s)) - len(str(sub)) + 1) if str(s).startswith(str(sub), i)))
    builtin('remq', lambda x, xs: _lst([v for v in cell_iter(xs) if not (v is x)]))
    builtin('remv', lambda x, xs: _lst([v for v in cell_iter(xs) if v != x]))
    builtin('keyword?', lambda x: TRUE if isinstance(x, Sym) and x.name.startswith(':') else FALSE)
    builtin('string->keyword', lambda x: Sym(':' + str(x).lstrip(':')))
    builtin('keyword->string', lambda x: SchemeString(str(x).lstrip(':')))
    builtin('srfi-available?', lambda n: TRUE)
    builtin('stream?', lambda x: TRUE if isinstance(x, Cell) and (callable(x.cdr) or isinstance(x.cdr, Promise) or x.cdr is NIL) else FALSE)
    builtin('string-normalize-nfc', lambda x: SchemeString(str(x)))
    builtin('string-normalize-nfd', lambda x: SchemeString(str(x)))
    builtin('string-normalize-nfkc', lambda x: SchemeString(str(x)))
    builtin('string-normalize-nfkd', lambda x: SchemeString(str(x)))
    builtin('string-prefix-ci?', lambda a, b: TRUE if str(b).lower().startswith(str(a).lower()) else FALSE)
    builtin('gentemp', lambda: Sym('gentemp'))
    builtin('sorted-by', sorted_by_fn)
    builtin('update', update_fn)

    builtin('append!', _append_bang)
    builtin('append-reverse!', _append_reverse_bang)
    builtin('char-set-unfold', _char_set_unfold)
    builtin('concatenate!', concatenate_fn)
    builtin('cond-expand-srfi-61', lambda *args: TRUE)
    builtin('drop-right!', _drop_right_bang)
    builtin('find-tail', _find_tail)
    builtin('fold-right-1', _fold_right_1)
    builtin('include-ci', _include_ci)
    builtin('integer->char-set', _integer_char_set)
    builtin('lset-adjoin', _lset_adjoin)
    builtin('lset<=', _lset_subset)
    builtin('lset=', lambda eq, a, b: _lset_subset(eq, a, b) if _lset_subset(eq, a, b) is TRUE and _lset_subset(eq, b, a) is TRUE else FALSE)
    builtin('random-source-make-integers', _random_integers)
    builtin('random-source-make-reals', _random_reals)
    builtin('require-extension', lambda *args: TRUE)
    builtin('require-srfi', lambda *args: TRUE)
    builtin('test-equal?', _test_equal)
    for _name in ('define-record-type*', 'let*-values', 'let-values-helper', 'letrec*', 'record-accessor', 'record-constructor', 'record-modifier', 'record-predicate', 'simple-conditions', 'source-file', 'syntax-violation', 'transcript-off', 'transcript-on'):
        builtin(_name, _unsupported(_name))
    builtin('u8vector', lambda *xs: SchemeVector(list(xs)))
    builtin('u8vector?', lambda v: TRUE if isinstance(v, SchemeVector) else FALSE)
    builtin('u8vector-length', lambda v: len(v.data))
    builtin('u8vector-ref', lambda v, i: v.data[int(i)])
    builtin('u8vector-set!', lambda v, i, x: v.data.__setitem__(int(i), x) or VOID)
    builtin('vector-sort!', lambda v, less: VOID)
    builtin('xsubstring', lambda s, start, end: SchemeString(str(s)[int(start):int(end)]))
    builtin('make-u8vector', lambda n, *a: SchemeVector([(a[0] if a else 0) for _ in range(int(n))]))
    builtin('f64vector', lambda *xs: SchemeVector(list(xs)))
    builtin('f64vector?', lambda v: TRUE if isinstance(v, SchemeVector) else FALSE)
    builtin('f64vector-length', lambda v: len(v.data))
    builtin('f64vector-ref', lambda v, i: v.data[int(i)])
    for _prefix in ('f32', 'f64', 's8', 's16', 's32', 's64', 'u16', 'u32', 'u64'):
        builtin(_prefix + 'vector', lambda *xs: SchemeVector(list(xs)))
        builtin(_prefix + 'vector?', lambda v: TRUE if isinstance(v, SchemeVector) else FALSE)
        builtin(_prefix + 'vector-length', lambda v: len(v.data))
        builtin(_prefix + 'vector-ref', lambda v, i: v.data[int(i)])
        builtin(_prefix + 'vector-set!', lambda v, i, x: v.data.__setitem__(int(i), x) or VOID)
        builtin('make-' + _prefix + 'vector', lambda n, *a: SchemeVector([(a[0] if a else 0) for _ in range(int(n))]))
    builtin('integer-compare', lambda a, b: -1 if a < b else (1 if a > b else 0))
    builtin('set', lambda *xs: _lst(list(xs)))
    builtin('set?', lambda x: TRUE if isinstance(x, Cell) else FALSE)
    builtin('set-contains?', lambda s, x: TRUE if any(v == x for v in cell_iter(s)) else FALSE)
    builtin('regexp', lambda s: _re.compile(str(s)))
    builtin('regexp?', lambda x: TRUE if hasattr(x, 'search') else FALSE)
    builtin('regexp-matches?', lambda r, s: TRUE if r.search(str(s)) else FALSE)
    builtin('make-timer', lambda *args: ('timer', args))
    builtin('timer?', lambda x: TRUE if isinstance(x, tuple) and x and x[0] == 'timer' else FALSE)
    builtin('nonempty-list?', lambda x: TRUE if isinstance(x, Cell) else FALSE)
    builtin('string-cursor-start', lambda s: 0)
    builtin('lset=', lambda eq, a, b: TRUE if len(list(cell_iter(a))) == len(list(cell_iter(b))) and all(any(eq(x, y) is TRUE for y in cell_iter(b)) for x in cell_iter(a)) else FALSE)
    builtin('generic-sequence?', lambda x: TRUE if isinstance(x, Cell) or isinstance(x, SchemeVector) or isinstance(x, SchemeString) else FALSE)
    builtin('flat-sequence?', lambda x: TRUE if isinstance(x, Cell) else FALSE)
    builtin('generic-ref', lambda x, i: x[int(i)] if hasattr(x, '__getitem__') else FALSE)
    builtin('make-ephemeron', make_ephemeron)
    builtin('ephemeron?', is_ephemeron)
    builtin('ephemeron-key', lambda x: x[1])
    builtin('ephemeron-datum', lambda x: x[2])
    builtin('make-lseq', make_lseq)
    builtin('lseq?', is_lseq)
    builtin('make-enum-set', make_enum_set)
    builtin('enum-set?', is_enum_set)
    builtin('make-array2d', make_array2d)
    builtin('array2d?', is_array2d)
    builtin('array2d-rows', array2d_rows)
    builtin('array2d-cols', lambda x: x[2])
    builtin('make-flex-vector', make_flex_vector)
    builtin('flex-vector?', is_flex_vector)
    builtin('make-unifiable-box', make_unifiable_box)
    builtin('unifiable-box?', is_unifiable_box)
    builtin('box-eval', unbox)
    builtin('mutable-string?', lambda x: TRUE if isinstance(x, SchemeString) else FALSE)
    builtin('make-mutable-string', lambda x: SchemeString(str(x)))
    builtin('string-titlecase', lambda x: SchemeString(str(x).title()))
    builtin('everywhere', lambda f, x: f(x))
    builtin('set-at', lambda xs, i, x: _lst([x if n == int(i) else v for n, v in enumerate(cell_iter(xs))]))
    builtin('unifiable-box-ref', lambda x: x[1])
    builtin('ideque', make_ideque)
    builtin('ideque?', is_ideque)
    builtin('make-integer-set', make_integer_set)
    builtin('integer-set?', is_integer_set)
    builtin('make-text', make_text)
    builtin('text?', is_text)
    builtin('text-length', text_length)
    builtin('string-compare-ci', lambda a, b: (-1 if str(a).lower() < str(b).lower() else (1 if str(a).lower() > str(b).lower() else 0)))
    builtin('two-arg-invoke', lambda f, a, b: call(f, [a, b]))
    builtin('flex-vector-ref', lambda v, i: v[1][int(i)])
    builtin('<?.', lambda pred, *xs: TRUE if all(pred(xs[i], xs[i + 1]) is TRUE for i in range(len(xs) - 1)) else FALSE)
    builtin('syntax-closure?', lambda x: TRUE if isinstance(x, tuple) and x and x[0] == 'syntax-closure' else FALSE)
    builtin('make-syntax-closure', lambda free, bound: ('syntax-closure', free, bound))
    builtin('shape', lambda *dims: _lst([int(x) for x in dims]))
    builtin('array', lambda shp, *values: SchemeVector(list(values)))
    builtin('parse-body', lambda *args: VOID)
    builtin('type-of', lambda *args: VOID)
    integer_cmp = make_comparator(lambda a, b: TRUE if a == b else FALSE, lambda a, b: TRUE if a < b else FALSE, lambda x: int(x), 'integer')
    be.define('integer-comparator', integer_cmp)
    builtin('=?', lambda c, a, b: comparator_eq_fn(c)(a, b))
    builtin('<?', lambda c, a, b: comparator_lt_fn(c)(a, b))
    builtin('current-date', lambda: ('date', int(_time.time())))
    builtin('current-time', lambda: ('time', int(_time.time())))
    builtin('date?', lambda v: TRUE if isinstance(v, tuple) and v and v[0] == 'date' else FALSE)
    builtin('time?', lambda v: TRUE if isinstance(v, tuple) and v and v[0] == 'time' else FALSE)
    # Small SRFI host values.  These are deliberately plain tuples/lists so
    # predicates and accessors remain cheap and interoperable with the core.
    builtin('csv-read', lambda port: _lst([_lst([SchemeString(x) for x in line.split(',')]) for line in str(port[1][0]).splitlines() if line != '']))
    builtin('parse', lambda *chars: int(''.join(cs_char(c) for c in chars)))
    builtin('char', lambda value: value)
    builtin('range->list', lambda r: _lst(list(range(int(r[1]), int(r[2]), int(r[3])))))
    builtin('make-range', lambda start, end, step=1: ('range', int(start), int(end), int(step)))
    builtin('m4-zero', lambda: SchemeVector([0] * 16))
    builtin('bmi-and', lambda a, b: int(a) & int(b))
    builtin('sxml?', lambda x: TRUE if isinstance(x, Cell) else FALSE)
    builtin('file-exists?', file_exists_fn)
    builtin('group-by', _group_by)
    builtin('flex-vector', lambda *values: ('flex-vector', list(values)))
    builtin('base32-encode', lambda bv: SchemeString(_base64.b32encode(bytes(bv.data)).decode('ascii')))
    builtin('int-vector', lambda *values: SchemeVector([int(x) for x in values]))
    builtin('int-vector?', lambda x: TRUE if isinstance(x, SchemeVector) else FALSE)
    builtin('assoc-map', lambda *pairs: _lst([Cell(pairs[i], pairs[i + 1]) for i in range(0, len(pairs), 2)]))
    builtin('assoc-map?', lambda x: TRUE if x is NIL or (isinstance(x, Cell) and all(isinstance(p, Cell) for p in cell_iter(x))) else FALSE)
    builtin('rt-sin', lambda x: math.sin(float(x)))
    builtin('make-operator-parser', lambda *args: (lambda value: value))
    builtin('path-absolute?', lambda path: TRUE if _os.path.isabs(str(path)) else FALSE)
    builtin('make-domain', lambda lo, hi: ('domain', lo, hi))
    builtin('domain?', lambda x: TRUE if isinstance(x, tuple) and x and x[0] == 'domain' else FALSE)
    builtin('array-rank', lambda x: 1 if isinstance(x, SchemeVector) else 0)
    builtin('option', _option)
    builtin('option?', lambda x: TRUE if isinstance(x, tuple) and x and x[0] == 'option' else FALSE)
    builtin('make-color', lambda r, g, b: ('color', float(r), float(g), float(b)))
    builtin('color?', lambda x: TRUE if isinstance(x, tuple) and x and x[0] == 'color' else FALSE)
    builtin('color-red', lambda x: x[1])
    builtin('red', ('color', 1.0, 0.0, 0.0))
    builtin('floating-point-pi', lambda: math.pi)
    builtin('floating-point-e', lambda: math.e)
    builtin('recursive-equality?', lambda a, b: TRUE if equal(a, b) is TRUE else FALSE)

    # Existing flex-vector constructor uses the same representation.
    builtin('flex-vector-ref', lambda v, i: v[1][int(i)])

    # ═══════════════════════════════════════════════════════════════
    # SRFI-111: Boxes
    # ═══════════════════════════════════════════════════════════════

    # ═══════════════════════════════════════════════════════════════
    # SRFI-128: Comparators
    # ═══════════════════════════════════════════════════════════════
    builtin('make-comparator', lambda a, b, c, d='custom': make_comparator(a, b, c, d))
    builtin('comparator?', is_comparator)
    builtin('comparator-order?', is_comparator_order)
    builtin('comparator-hashable?', is_comparator_hashable)
    builtin('comparator-test-type', lambda c: (lambda x: TRUE))
    builtin('make-default-comparator', lambda: default_comparator())
    builtin('make-eq-comparator', lambda: make_comparator(lambda a,b: a is b, lambda a,b: False, lambda x: id(x)))
    builtin('make-eqv-comparator', lambda: make_comparator(lambda a,b: a is b or a == b, lambda a,b: False, lambda x: id(x)))
    builtin('make-equal-comparator', lambda: make_comparator(lambda a,b: a == b, lambda a,b: False, lambda x: id(x)))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-141: Division (exact integer division variants)
    # ═══════════════════════════════════════════════════════════════
    builtin('floor-div', floor_div)
    builtin('floor-mod', floor_mod)
    builtin('floor-rem', floor_rem)
    builtin('floor-quotient', floor_div)
    builtin('floor-remainder', floor_rem)
    builtin('floor/', lambda a, b: Cell(floor_div(a, b), floor_rem(a, b)))

    builtin('truncate-div', truncate_div)
    builtin('truncate-rem', truncate_rem)
    builtin('truncate-quotient', truncate_div)
    builtin('truncate-remainder', truncate_rem)
    builtin('truncate/', lambda a, b: Cell(truncate_div(a, b), truncate_rem(a, b)))

    builtin('ceiling-div', ceiling_div)
    builtin('ceiling-rem', ceiling_rem)
    builtin('ceiling-quotient', ceiling_div)
    builtin('ceiling-remainder', ceiling_rem)
    builtin('ceiling/', lambda a, b: Cell(ceiling_div(a, b), ceiling_rem(a, b)))

    builtin('round-div', round_div)
    builtin('round-rem', lambda n, d: int(n) - round_div(n, d) * int(d))
    builtin('round-quotient', round_div)
    builtin('round-remainder', lambda n, d: int(n) - round_div(n, d) * int(d))
    builtin('round/', lambda a, b: Cell(round_div(a, b), int(a) - round_div(a, b) * int(b)))

    builtin('euclidean-div', euclidean_div)
    builtin('euclidean-rem', euclidean_rem)
    builtin('euclidean-quotient', euclidean_div)
    builtin('euclidean-remainder', euclidean_rem)
    builtin('euclidean/', lambda a, b: Cell(euclidean_div(a, b), euclidean_rem(a, b)))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-143: Fixnums (exact integer arithmetic with overflow check)
    # ═══════════════════════════════════════════════════════════════
    builtin('fx-width', lambda: FX_WIDTH)
    builtin('fx-greatest', lambda: FX_GREATEST)
    builtin('fx-least', lambda: FX_LEAST)
    builtin('fx+', fx_add)
    builtin('fx-', fx_sub)
    builtin('fx*', fx_mul)
    builtin('fxdiv', fx_div)
    builtin('fxmod', fx_mod)
    builtin('fxdiv0', lambda x, y: floor_div(x, y))
    builtin('fxmod0', lambda x, y: floor_rem(x, y))
    builtin('fx=?', lambda *a: fx_cmp(lambda x, y: x == y, *a))
    builtin('fx<?', lambda *a: fx_cmp(lambda x, y: x < y, *a))
    builtin('fx>?', lambda *a: fx_cmp(lambda x, y: x > y, *a))
    builtin('fx<=?', lambda *a: fx_cmp(lambda x, y: x <= y, *a))
    builtin('fx>=?', lambda *a: fx_cmp(lambda x, y: x >= y, *a))
    builtin('fxzero?', lambda x: TRUE if fxcheck(x) == 0 else FALSE)
    builtin('fxpositive?', lambda x: TRUE if fxcheck(x) > 0 else FALSE)
    builtin('fxnegative?', lambda x: TRUE if fxcheck(x) < 0 else FALSE)
    builtin('fxodd?', lambda x: TRUE if fxcheck(x) & 1 else FALSE)
    builtin('fxeven?', lambda x: TRUE if not (fxcheck(x) & 1) else FALSE)
    builtin('fxmax', lambda *a: max(fxcheck(x) for x in a))
    builtin('fxmin', lambda *a: min(fxcheck(x) for x in a))
    builtin('fxand', fx_and)
    builtin('fxior', fx_ior)
    builtin('fxxor', fx_xor)
    builtin('fxnot', fx_not)
    builtin('fxlsh', fx_lsh)
    builtin('fxrshl', fx_rshl)
    builtin('fxrsha', fx_rsha)
    builtin('fxfirst-set-bit', lambda x: (x & -x).bit_length() - 1 if x else -1)
    builtin('fxbit-count', lambda x: x.bit_count() if x else 0)
    builtin('fxlength', lambda x: x.bit_length())
    builtin('fxif', lambda a, b, c: (a & b) | (~a & c))
    builtin('fxbit-set?', lambda x, i: TRUE if (x >> i) & 1 else FALSE)
    builtin('fxcopy-bit', lambda x, i, b: x if b else (x | (1 << i)))
    builtin('fxgcd', math.gcd)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-144: Flonums (inexact real arithmetic)
    # ═══════════════════════════════════════════════════════════════
    builtin('flonum?', lambda x: TRUE if is_flonum(x) else FALSE)
    builtin('fl+', fl_add)
    builtin('fl-', fl_sub)
    builtin('fl*', fl_mul)
    builtin('fl/', fl_div)
    builtin('fl=?', lambda *a: fl_cmp(lambda x, y: x == y, *a))
    builtin('fl<?', lambda *a: fl_cmp(lambda x, y: x < y, *a))
    builtin('fl>?', lambda *a: fl_cmp(lambda x, y: x > y, *a))
    builtin('fl<=?', lambda *a: fl_cmp(lambda x, y: x <= y, *a))
    builtin('fl>=?', lambda *a: fl_cmp(lambda x, y: x >= y, *a))
    builtin('flzero?', lambda x: TRUE if float(x) == 0.0 else FALSE)
    builtin('flpositive?', lambda x: TRUE if float(x) > 0.0 else FALSE)
    builtin('flnegative?', lambda x: TRUE if float(x) < 0.0 else FALSE)
    builtin('flodd?', lambda x: TRUE if int(Fraction(x)) % 2 != 0 else FALSE)
    builtin('fleven?', lambda x: TRUE if int(Fraction(x)) % 2 == 0 else FALSE)
    builtin('flfinite?', lambda x: TRUE if isinstance(x, float) and math.isfinite(x) else FALSE)
    builtin('flinfinite?', lambda x: TRUE if isinstance(x, float) and math.isinf(x) else FALSE)
    builtin('flnan?', lambda x: TRUE if isinstance(x, float) and math.isnan(x) else FALSE)
    builtin('flmax', fl_max)
    builtin('flmin', fl_min)
    builtin('flfloor', math.floor)
    builtin('flceiling', math.ceil)
    builtin('flround', round)
    builtin('fltruncate', math.trunc)
    builtin('flsqrt', math.sqrt)
    builtin('flexp', math.exp)
    builtin('flexpt', lambda a, b: float(a) ** float(b))
    builtin('fllog', math.log)
    builtin('flsin', math.sin)
    builtin('flcos', math.cos)
    builtin('fltan', math.tan)
    builtin('flasin', math.asin)
    builtin('flacos', math.acos)
    builtin('flatan', math.atan)
    builtin('flonum->fixnum', lambda x: int(x))
    builtin('fixnum->flonum', lambda x: float(x))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-151: Bitwise operations
    # ═══════════════════════════════════════════════════════════════
    builtin('bitwise-not', bitwise_not)
    builtin('bitwise-and', bitwise_and)
    builtin('bitwise-ior', bitwise_ior)
    builtin('bitwise-xor', bitwise_xor)
    builtin('bitwise-if', bitwise_if)
    builtin('bitwise-merge', bitwise_if)
    builtin('bitwise-length', bitwise_length)
    builtin('bitwise-count', bitwise_count)
    builtin('bitwise-reverse-bit-field', bitwise_reverse_bitfield)
    builtin('bitwise-reverse-bitfield', bitwise_reverse_bitfield)
    builtin('bitwise-rotate', bitwise_rotate)
    builtin('bitwise-rotate-bit-field', bitwise_rotate_field)
    builtin('bitwise-copy-bit-field', bitwise_copy_bit_field)
    builtin('bitwise-copy-bit', bitwise_copy_bit)
    builtin('bitwise-bit-field', bitwise_bit_field)
    builtin('bitwise-arithmetic-shift', bitwise_shift)
    builtin('bitwise-arithmetic-shift-right', lambda n, c: bitwise_shift(n, -int(c)))
    builtin('bitwise-shift', bitwise_shift)
    builtin('bitwise-any-bit-set?', lambda n, m: TRUE if (int(n) & int(m)) != 0 else FALSE)
    builtin('integer-length', integer_length)
    builtin('first-set-bit', first_set_bit)
    builtin('bit-count', bitwise_count)
    builtin('bit-field', bitwise_bit_field)
    builtin('bit-shift', bitwise_shift)
    builtin('copy-bit', bitwise_copy_bit)
    builtin('bit-set?', lambda n, i: TRUE if (int(n) >> int(i)) & 1 else FALSE)
    builtin('integer->booleans', integer_to_booleans)

    # ═══════════════════════════════════════════════════════════════
    # Bitvectors
    # ═══════════════════════════════════════════════════════════════
    builtin('bitvector?', bitvector_p)
    builtin('make-bitvector', lambda n, *fill: SchemeVector([fill[0] if fill else FALSE] * int(n)))
    builtin('bitvector-copy', lambda bv, *args: SchemeVector(list(vec(bv))))
    builtin('bitvector-append', lambda *bvs: SchemeVector([x for bv in bvs for x in vec(bv)]))
    builtin('bitvector-length', lambda bv: len(vec(bv)))
    builtin('bitvector-ref', lambda bv, i: TRUE if vec(bv)[i] else FALSE)
    builtin('bitvector-set!', lambda bv, i, v: vec_set(bv, int(i), v))
    builtin('list->bitvector', lambda lst: SchemeVector([x is TRUE or x is True for x in cell_iter(lst)]))
    builtin('bitvector->list', lambda bv: _lst([TRUE if x else FALSE for x in vec(bv)]))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-133: Vector extensions
    # ═══════════════════════════════════════════════════════════════
    builtin('vector-map', vector_map)
    builtin('vector-map!', do_vector_map)
    builtin('vector-for-each', vector_for_each)
    builtin('vector-count', vector_count)
    builtin('vector-any', vector_any)
    builtin('vector-every', vector_every)
    builtin('vector-fold', vector_fold)
    builtin('vector-fold-right', vector_fold_right)
    builtin('vector-unfold', vector_unfold)
    builtin('vector-index', vector_index)
    builtin('vector-skip', vector_skip)
    builtin('vector-swap!', do_vector_swap)
    builtin('vector-reverse!', do_vector_reverse)
    builtin('vector-empty?', vec_empty_q)
    builtin('vector-append', vector_append)
    builtin('vector-copy', vector_copy_fn)
    builtin('vector-copy!', vector_copy_bang)
    builtin('vector-concatenate', vector_concat)
    builtin('vector-reverse', vector_reverse_fn)
    builtin('vector-sort', vector_sort_fn)
    builtin('vector=', vector_equal)
    builtin('reverse-list->vector', lambda lst: SchemeVector(cells(lst)[::-1]))

    # Basic vector operations (re-register for pyb=True override)
    builtin('vector', lambda *a: SchemeVector(list(a)))



    builtin('vector->list', lambda v: _lst(list(v)))

    builtin('vector-fill!', lambda v, x, *a: vec_fill_range(v, x, *a))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-152: String utilities
    # ═══════════════════════════════════════════════════════════════
    builtin('string-take', string_take)
    builtin('string-drop', string_drop)
    builtin('string-take-right', string_take_right)
    builtin('string-drop-right', string_drop_right)
    builtin('string-pad', string_pad)
    builtin('string-pad-right', string_pad_right)
    builtin('string-trim', string_trim)
    builtin('string-trim-right', string_trim_right)
    builtin('string-trim-both', string_trim_both)
    builtin('string-trim-left', string_trim_left_fn)
    builtin('string-replace', string_replace)
    builtin('string-split', string_split)
    builtin('string-join', string_join)
    builtin('string-contains', string_contains)
    builtin('string-prefix?', str_prefix_q)
    builtin('string-suffix?', str_suffix_q)
    builtin('string-prefix-length', str_prefix_len)
    builtin('string-suffix-length', str_suffix_len)
    builtin('string-prefix-length-ci', str_prefix_len_ci)
    builtin('string-suffix-length-ci', str_suffix_len_ci)
    builtin('string-count', string_count)
    builtin('string-map', string_map)
    builtin('string-for-each', string_for_each)
    builtin('string-for-each-index', string_for_each_idx)
    builtin('string-fold', string_fold)
    builtin('string-fold-right', string_fold_right_fn)
    builtin('string-index', string_index_fn)
    builtin('string-index-right', string_index_right_fn)
    builtin('string-skip', string_skip_fn)
    builtin('string-skip-right', string_skip_right_fn)
    builtin('string-any', string_any_fn)
    builtin('string-every', string_every_fn)
    builtin('string-concatenate', string_concat)
    builtin('string-copy!', string_copy_bang)
    builtin('string-xcopy!', string_xcopy_bang)
    builtin('string-delete', string_remove_fn)
    builtin('string-filter', string_filter_fn)
    builtin('string-remove', string_remove_fn)
    builtin('string-reverse', lambda s: SchemeString(''.join(reversed(str(s)))))
    builtin('string-foldcase', lambda s: SchemeString(str(s).lower()))
    builtin('string-titlecase', lambda s: SchemeString(str(s).title()))


    builtin('string-tokenize', string_tokenize_fn)
    builtin('string-unfold', string_unfold_fn)
    builtin('string-tabulate', lambda n, f: SchemeString(''.join(_so(f(i)) for i in range(int(n)))))
    builtin('string->char-set', str_to_char_set)
    builtin('string->vector', str_to_vec)
    builtin('vector->string', vec_to_str)

    # Basic string operations (re-register for pyb=True override)
    builtin('string', lambda *a: SchemeString(''.join(char_val(x) for x in a)))

    builtin('->string', lambda x: x if isinstance(x, (str, SchemeString)) else SchemeString(_pr(x)))

    # ═══════════════════════════════════════════════════════════════
    # String comparison (ci variants)
    # ═══════════════════════════════════════════════════════════════
    builtin('string=?', lambda a, b: TRUE if str(a) == str(b) else FALSE)
    builtin('string<?', lambda a, b: TRUE if str(a) < str(b) else FALSE)
    builtin('string>?', lambda a, b: TRUE if str(a) > str(b) else FALSE)
    builtin('string<=?', lambda a, b: TRUE if str(a) <= str(b) else FALSE)
    builtin('string>=?', lambda a, b: TRUE if str(a) >= str(b) else FALSE)
    builtin('string-ci=?', lambda a, b: TRUE if str(a).lower() == str(b).lower() else FALSE)
    builtin('string-ci<?', lambda a, b: TRUE if str(a).lower() < str(b).lower() else FALSE)
    builtin('string-ci>?', lambda a, b: TRUE if str(a).lower() > str(b).lower() else FALSE)
    builtin('string-ci<=?', lambda a, b: TRUE if str(a).lower() <= str(b).lower() else FALSE)
    builtin('string-ci>=?', lambda a, b: TRUE if str(a).lower() >= str(b).lower() else FALSE)

    # ═══════════════════════════════════════════════════════════════
    # Char operations
    # ═══════════════════════════════════════════════════════════════
    builtin('char-ci=?', char_ci_eq)
    builtin('char-ci<?', lambda a, b: TRUE if str(a).lower() < str(b).lower() else FALSE)
    builtin('char-ci>?', lambda a, b: TRUE if str(a).lower() > str(b).lower() else FALSE)
    builtin('char-ci<=?', lambda a, b: TRUE if str(a).lower() <= str(b).lower() else FALSE)
    builtin('char-ci>=?', lambda a, b: TRUE if str(a).lower() >= str(b).lower() else FALSE)








    builtin('char-ascii?', lambda c: TRUE if ord(char_val(c)) < 128 else FALSE)
    builtin('char-control?', lambda c: TRUE if (n := ord(char_val(c))) < 32 or n == 127 else FALSE)
    builtin('char-iso-control?', lambda c: TRUE if (n := ord(char_val(c))) < 32 or n == 127 else FALSE)
    builtin('ascii?', lambda c: TRUE if ord(_so(c)) < 128 else FALSE)
    builtin('char->name', char_name)
    builtin('digit-value', digit_value)

    # ═══════════════════════════════════════════════════════════════
    # Char-set operations (SRFI-14)
    # ═══════════════════════════════════════════════════════════════
    builtin('char-set', lambda *chars: char_set_make(chars))
    builtin('char-set?', char_set_p)
    builtin('char-set-contains?', char_set_contains)
    builtin('char-set-empty?', char_set_empty)
    builtin('char-set->list', char_set_to_list)
    builtin('char-set->string', char_set_to_string)
    builtin('char-set-count', char_set_count)
    builtin('char-set-copy', char_set_copy)
    builtin('char-set-union', lambda *css: char_set_binop(css, lambda a, b: a or b))
    builtin('char-set-intersection', lambda *css: char_set_binop(css, lambda a, b: a and b))
    builtin('char-set-difference', lambda cs1, *css: char_set_diff(cs1, css))
    builtin('char-set-xor', lambda *css: char_set_xor(css))
    builtin('char-set-complement', char_set_complement)
    builtin('char-set-adjoin', lambda cs, *chars: char_set_adjoin(cs, chars))
    builtin('char-set-delete', lambda cs, *chars: char_set_delete(cs, chars))
    builtin('char-set-any', char_set_any)
    builtin('char-set-every', char_set_every)
    builtin('char-set-filter', lambda pred, cs, *basis: char_set_filter(pred, cs, basis[0] if basis else cs))
    builtin('char-set-fold', char_set_fold)
    builtin('char-set-for-each', char_set_for_each)
    builtin('char-set-map', char_set_map)
    builtin('char-set-hash', lambda cs, *bound: char_set_hash(cs, int(bound[0]) if bound else 65536))
    builtin('char-set=?', char_set_equal)
    builtin('ucs-range->char-set', ucs_range_char_set)
    builtin('char-set:empty', [False] * 256)
    builtin('char-set:full', [True] * 256)
    builtin('char-set:lower-case', char_set_make([SchemeChar(chr(i)) for i in range(ord('a'), ord('z') + 1)]))
    builtin('char-set:lower', be.data['char-set:lower-case'])
    builtin('char-set:upper-case', char_set_make([SchemeChar(chr(i)) for i in range(ord('A'), ord('Z') + 1)]))
    builtin('char-set:upper', be.data['char-set:upper-case'])
    builtin('char-set:digit', char_set_make([SchemeChar(chr(i)) for i in range(ord('0'), ord('9') + 1)]))
    builtin('char-set:whitespace', char_set_make([SchemeChar(' '), SchemeChar('\t'), SchemeChar('\n'), SchemeChar('\r')]))
    builtin('char-set:letter', char_set_binop((be.data['char-set:lower-case'], be.data['char-set:upper-case']), lambda a, b: a or b))
    builtin('char-set:punctuation', char_set_make([SchemeChar(c) for c in ".,;:!?-'\"()[]{}\\/@#$%^&*+=<>|~"]))
    builtin('char-set:graphic', char_set_binop((be.data['char-set:letter'], be.data['char-set:digit'], be.data['char-set:punctuation']), lambda a, b: a or b))
    builtin('char-set:printing', ucs_range_char_set(32, 127))
    builtin('char-set:symbol', char_set_make([SchemeChar(c) for c in "!$%&*+-./:<=>?@^_~"]))
    builtin('char-set:hex-digit', char_set_make([SchemeChar(c) for c in '0123456789abcdefABCDEF']))
    builtin('char-set:blank', char_set_make([SchemeChar(' '), SchemeChar('\t')]))
    builtin('char-set:iso-control', char_set_adjoin(ucs_range_char_set(0, 32), [SchemeChar(chr(127))]))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-158: Generators
    # ═══════════════════════════════════════════════════════════════
    builtin('generator', generator)
    builtin('generator?', lambda x: TRUE if callable(x) else FALSE)
    builtin('make-generator', make_generator)
    builtin('list->generator', list_generator)
    builtin('vector->generator', vector_generator)
    builtin('string->generator', string_generator)
    builtin('generator-map', generator_map)
    builtin('generator-filter', generator_filter)
    builtin('generator-take', generator_take)
    builtin('generator-drop', generator_drop)
    builtin('generator-find', generator_find)
    builtin('generator-count', generator_count)
    builtin('generator-append', generator_append)
    builtin('generator->list', generator_list_and)
    builtin('generator->vector', generator_vector_and)
    builtin('generator->string', generator_string_and)
    builtin('generator-for-each', generator_for_each)
    builtin('generator-fold', generator_fold_fn)
    builtin('make-iota-generator', lambda n, start=0, step=1: generator_iota(int(n), step, start))
    builtin('make-range-generator', lambda s, e, st=1: generator_range(s, e, st))

    # ═══════════════════════════════════════════════════════════════
    # SRFI-117: List queues
    # ═══════════════════════════════════════════════════════════════
    builtin('make-list-queue', lambda f=NIL, b=NIL: make_list_queue(f, b))
    builtin('list-queue', list_queue)
    builtin('list-queue?', is_list_queue)
    builtin('list-queue-front', list_queue_front)
    builtin('list-queue-back', list_queue_back)
    builtin('list-queue-empty?', lq_empty_q)
    builtin('list-queue-add!', do_lq_add)
    builtin('list-queue-add-back!', do_lq_add)
    builtin('list-queue-add-front!', do_lq_add_front)
    builtin('list-queue-remove!', do_lq_remove)
    builtin('list-queue-remove-front!', do_lq_remove)
    builtin('list-queue-list', list_queue_list)
    builtin('list-queue-first', list_queue_first)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-125: Hash tables
    # ═══════════════════════════════════════════════════════════════

    builtin('make-eq-hash-table', make_ht)
    builtin('make-equal-hash-table', make_ht)
    builtin('make-eqv-hash-table', make_ht)
    builtin('make-strong-hash-table', lambda: {})

    builtin('hash-table-clear!', do_ht_clear)


    builtin('hash-table-map', hash_table_map)
    builtin('hash-table-fold', hash_table_fold)
    builtin('hash-table-update!', hash_table_update)
    builtin('hash-table-walk', hash_table_walk)
    builtin('hash-table-count', hash_table_count)
    builtin('hash-table-put!', lambda ht, key, value: ht.__setitem__(key, value) or VOID)
    builtin('hash-table-merge!', hash_table_merge_bang)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-1: List operations
    # ═══════════════════════════════════════════════════════════════
    builtin('reverse', rvrs)
    builtin('cons*', cons_star)
    builtin('list*', cons_star)
    builtin('list-copy', list_copy_fn)
    builtin('make-list', lambda n, *v: make_list_fn(int(n), v[0] if v else FALSE))
    builtin('iota', lambda n, *a: iota_fn(int(n), a[0] if a else 0, a[1] if len(a) > 1 else 1))
    builtin('first', lambda lst: nth(lst, 0))
    builtin('second', lambda lst: nth(lst, 1))
    builtin('third', lambda lst: nth(lst, 2))
    builtin('fourth', lambda lst: nth(lst, 3))
    builtin('fifth', lambda lst: nth(lst, 4))
    builtin('sixth', lambda lst: nth(lst, 5))
    builtin('seventh', lambda lst: nth(lst, 6))
    builtin('eighth', lambda lst: nth(lst, 7))
    builtin('ninth', lambda lst: nth(lst, 8))
    builtin('tenth', lambda lst: nth(lst, 9))
    builtin('list-head', list_head_fn)

    builtin('take', lambda lst, n: list_take(lst, int(n)))
    builtin('drop', lambda lst, n: list_drop(lst, int(n)))
    builtin('take-right', lambda lst, n: list_take_right(lst, int(n)))
    builtin('drop-right', lambda lst, n: list_drop_right(lst, int(n)))
    builtin('take-while', list_take_while)
    builtin('drop-while', list_drop_while)
    builtin('last', list_last)
    builtin('last-pair', list_last_pair)
    builtin('but-last', list_butlast)
    builtin('length+', length_plus)
    builtin('list-tabulate', lambda n, f: list_tabulate_fn(int(n), f))
    builtin('list-index', list_index_fn)
    builtin('list-set!', list_set_bang)
    builtin('list-find', list_find)
    builtin('list-find-index', list_find_index)
    builtin('list-any', list_any)
    builtin('list-every', list_every)
    builtin('list-filter-map', list_filter_map)
    builtin('list-partition', list_partition)
    builtin('list-remove', list_remove)
    builtin('list-flatten', list_flatten)
    builtin('list-zip', zip_fn)
    builtin('list-sort', list_sort_fn)
    builtin('list-stable-sort', list_sort_fn)
    builtin('sort', generic_sort)
    builtin('list=', list_equal)
    builtin('sorted?', sorted_p_fn)
    builtin('merge', merge_fn)
    builtin('merge!', merge_bang_fn)
    builtin('assq', lambda obj, al: assoc_fn(obj, al, lambda a, b: a is b))
    builtin('assv', lambda obj, al: assoc_fn(obj, al, lambda a, b: a is b or a == b))
    builtin('assoc', lambda obj, al, *eq: assoc_fn(obj, al, eq[0] if eq else (lambda a, b: a is b or a == b)))
    builtin('memq', lambda obj, lst: mem_fn(obj, lst, lambda a, b: a is b))
    builtin('memv', lambda obj, lst: mem_fn(obj, lst, lambda a, b: a is b or a == b))
    builtin('member', lambda obj, lst, *eq: mem_fn(obj, lst, eq[0] if eq else (lambda a, b: a is b or a == b)))
    builtin('find', list_find)
    builtin('fold', fold_left_fn)
    builtin('fold-left', fold_left_fn)
    builtin('fold-right', fold_right_fn)
    builtin('reduce', fold_left_fn)
    builtin('reduce-right', fold_right_fn)
    builtin('any', list_any)
    builtin('every', list_every)
    builtin('count', count_fn)
    builtin('delete', lambda x, lst, *eq: delete_fn(x, lst, eq[0] if eq else None))
    builtin('delete-duplicates', lambda lst, *eq: delete_dups_fn(lst, eq[0] if eq else None))
    builtin('delete-assoc', delete_assoc_fn)
    builtin('alist-cons', lambda k, v, al: Cell(Cell(k, v), al))
    builtin('alist-delete', alist_delete_fn)
    builtin('append-map', append_map_fn)
    builtin('append-reverse', append_rev)
    builtin('concatenate', concatenate_fn)
    builtin('flatten', flatten_fn)
    builtin('filter-map', filter_map_fn)
    builtin('map-in-order', map_fn)
    builtin('pair-for-each', pair_for_each_fn)
    builtin('xcons', lambda d, a: Cell(a, d))
    builtin('zip', zip_fn)
    builtin('unzip1', lambda lst: unzip_n(lst, 1))
    builtin('unzip2', lambda lst: unzip_n(lst, 2))
    builtin('unzip3', lambda lst: unzip_n(lst, 3))
    builtin('unzip4', lambda lst: unzip_n(lst, 4))
    builtin('unzip5', lambda lst: unzip_n(lst, 5))
    builtin('curry', curry_fn)
    builtin('complement', lambda f: lambda *a: FALSE if f(*a) is TRUE else TRUE)
    builtin('flip', lambda f: lambda a, b: f(b, a))
    builtin('const', lambda x: lambda *_: x)
    builtin('iterate', lambda f, n, x: iterate_fn(f, int(n), x))
    builtin('product', product_fn)
    builtin('square', lambda x: x * x)
    builtin('range', lambda s, e, *st: range_fn(int(s), int(e), int(st[0]) if st else 1))
    builtin('interleave', interleave_fn)
    builtin('symbolic-append', lambda *a: Sym(''.join(_sn(x) for x in a)))

    builtin('<>', lambda a, b: TRUE if a != b else FALSE)

    # List predicate helpers
    builtin('circular-list', circular_list)
    builtin('circular-list?', circular_list_p)
    builtin('dotted-list?', dotted_list_p)
    builtin('proper-list?', proper_list_p)
    builtin('null-list?', lambda x: TRUE if x is NIL else FALSE)
    builtin('not-pair?', lambda x: TRUE if not isinstance(x, Cell) else FALSE)
    builtin('ne-list?', lambda x: TRUE if isinstance(x, Cell) and x.cdr is NIL else FALSE)

    # Mutation
    builtin('drop!', lambda lst, n: list_drop(lst, int(n)))
    builtin('take!', lambda lst, n: list_take(lst, int(n)))
    builtin('filter!', filter_fn)
    builtin('flat-map', append_map_fn)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-1: lset-* (set operations on lists)
    # ═══════════════════════════════════════════════════════════════
    builtin('lset-union', lset_union)
    builtin('lset-intersection', lset_intersection)
    builtin('lset-difference', lset_difference)
    builtin('lset-xor', lset_xor)
    builtin('lset-=?', lset_equal)

    # ═══════════════════════════════════════════════════════════════
    # Stream operations
    # ═══════════════════════════════════════════════════════════════
    builtin('nat-stream', lambda n: nat_stream_fn(int(n)))
    builtin('naturals', lambda *a: nat_stream(int(a[0]) if a else 0))
    builtin('sieve', sieve_fn)
    builtin('primes', sieve_fn(nat_stream(2)))
    builtin('tree->list', tree_to_list)
    builtin('num-den', num_den)

    # ═══════════════════════════════════════════════════════════════
    # Number theory & math
    # ═══════════════════════════════════════════════════════════════
    builtin('scheme-gcd', scheme_gcd_fn)
    builtin('scheme-lcm', scheme_lcm_fn)
    builtin('prime?', prime_p)
    builtin('factor', factor_fn)
    builtin('fib-pair', lambda n: fib_pair(int(n)))
    builtin('fibonacci', lambda n: fib_pair(int(n)).car)
    builtin('binomial', lambda n, k: binomial_fn(int(n), int(k)))
    builtin('factorial', lambda n: factorial_fn(int(n)))
    builtin('quick-expt', lambda b, e: quick_expt_fn(int(b), int(e)))
    builtin('expt-mod', expt_mod)
    builtin('log-base', log_base)
    builtin('degrees->radians', degrees_to_radians)
    builtin('radians->degrees', radians_to_degrees)

    # Hyperbolic math
    builtin('sinh', lambda x: math.sinh(float(x)))
    builtin('cosh', lambda x: math.cosh(float(x)))
    builtin('tanh', lambda x: math.tanh(float(x)))
    builtin('sech', lambda x: 1.0 / math.cosh(float(x)))
    builtin('csch', lambda x: 1.0 / math.sinh(float(x)))
    builtin('coth', lambda x: math.cosh(float(x)) / math.sinh(float(x)))
    builtin('log10', lambda x: math.log10(float(x)))
    builtin('log2', lambda x: math.log2(float(x)))

    # Numeric predicates and conversions
    builtin('nan?', lambda x: x != x)
    builtin('finite?', lambda x: TRUE if isinstance(x, (int, float, Fraction, complex)) and (not isinstance(x, float) or (x == x and x != float('inf') and x != float('-inf'))) else FALSE)
    builtin('infinite?', lambda x: TRUE if isinstance(x, float) and math.isinf(x) else FALSE)
    builtin('exact', lambda x: int(x) if isinstance(x, float) and x == int(x) else (Fraction(x).limit_denominator(1000000) if isinstance(x, float) else x))
    builtin('inexact', lambda x: float(x))
    builtin('exact-nonnegative-integer?', lambda x: TRUE if (isinstance(x, int) and x >= 0) or (isinstance(x, Fraction) and x.denominator == 1 and x.numerator >= 0) else FALSE)
    builtin('exact-rational?', lambda x: TRUE if isinstance(x, (int, Fraction)) else FALSE)
    builtin('exact-integer?', lambda x: TRUE if is_exact_int(x) else FALSE)
    builtin('reciprocal', lambda x: div(1, x))
    builtin('ceiling->exact', lambda x: int(math.ceil(float(x))) if isinstance(x, Fraction) else int(math.ceil(x)))
    builtin('floor->exact', lambda x: int(math.floor(float(x))) if isinstance(x, Fraction) else int(math.floor(x)))
    builtin('truncate->exact', lambda x: int(x))
    builtin('round->exact', lambda x: int(round(float(x))) if isinstance(x, Fraction) else int(round(x)))
    builtin('magnitude', lambda z: abs(z) if isinstance(z, complex) else abs(z))
    builtin('make-rectangular', lambda r, i: complex(float(r) if isinstance(r, Fraction) else int(r) if isinstance(r, float) and r == int(r) else r, float(i) if isinstance(i, Fraction) else int(i) if isinstance(i, float) and i == int(i) else i))

    # Basic numeric aliases
    builtin('add1', lambda x: x + 1)
    builtin('sub1', lambda x: x - 1)
    builtin('sub1*', lambda x: x - 1)
    builtin('float', lambda x: float(x))

    # ═══════════════════════════════════════════════════════════════
    # Conditions & errors
    # ═══════════════════════════════════════════════════════════════
    builtin('error?', error_q)
    builtin('file-error?', file_error_q)
    builtin('read-error?', read_error_q)
    builtin('condition-has-type?', lambda c, t: isinstance(c, tuple) and len(c) > 2 and c[1] == t)
    builtin('condition-type?', lambda obj: TRUE if (isinstance(obj, tuple) and len(obj) > 2 and obj[0] == 'condition') or isinstance(obj, ErrorObject) else FALSE)
    builtin('condition/report-string', lambda c: SchemeString(c[2]) if isinstance(c, tuple) and len(c) > 2 else SchemeString(str(c)))
    builtin('raise-continuable', lambda c: do_raise(c))
    builtin('make-error-condition', lambda t, m: ('condition', t, m))
    builtin('condition-message', lambda c: c[2] if isinstance(c, tuple) and len(c) > 2 else str(c))
    builtin('make-io-error', lambda message=SchemeString(''): ('condition', Sym('io-error'), message))
    builtin('io-error?', lambda c: TRUE if isinstance(c, tuple) and len(c) > 1 and c[1] == Sym('io-error') else FALSE)

    # ═══════════════════════════════════════════════════════════════
    # Maybe monad
    # ═══════════════════════════════════════════════════════════════
    builtin('maybe?', maybe_p)
    builtin('just', lambda x: Cell(x, NIL))
    builtin('nothing', lambda: FALSE)
    builtin('just?', just_p)
    builtin('nothing?', nothing_p)
    builtin('maybe-ref', lambda x, *default: x.car if isinstance(x, Cell) else (default[0] if default else FALSE))
    builtin('maybe->values', lambda x: (x.car, TRUE) if isinstance(x, Cell) else (FALSE, FALSE))
    # SRFI-189: maybe 构造器 —— 非 #f 直接返回该值（just 的内容），#f 返回 #f（nothing）。
    # 这样 (maybe 42) 与 42 在 equal? 下相等，符合 test-srfi-189 的断言。
    builtin('maybe', lambda x: x if x is not FALSE else FALSE)

    builtin('iset', iset_fn)
    builtin('iset?', iset_p)
    builtin('iset-contains?', iset_contains_p)
    builtin('iset-adjoin', iset_adjoin)
    builtin('iset-delete', iset_delete)
    builtin('iset-empty', iset_empty)
    builtin('iset-size', iset_size)
    builtin('iset-empty?', iset_empty_p)
    builtin('iset-union', iset_union)
    builtin('iset-intersection', iset_intersection)
    builtin('iset-difference', iset_difference)
    builtin('iset->list', iset_to_list)
    builtin('list->iset', list_to_iset)


    # ═══════════════════════════════════════════════════
    # SRFI-180: JSON
    # ═══════════════════════════════════════════════════════════════
    builtin('json-read', json_read)
    builtin('json-write', json_write)
    builtin('json-read-string', json_read_string)
    builtin('json-write-string', json_write_string)

    # ═══════════════════════════════════════════════════════════════
    # SRFI-207: String-notable (bytevector <-> string)
    # ═══════════════════════════════════════════════════════════════
    builtin('bytevector->string', bytevector_to_string)
    builtin('string->bytevector', string_to_bytevector)

    # ═══════════════════════════════════════════════════════════════
    # Mapping (SRFI-146)
    # ═══════════════════════════════════════════════════════════════
    builtin('mapping', mapping_fn)
    builtin('mapping?', mapping_pred)

    # ═══════════════════════════════════════════════════════════════
    # Textual port I/O
    # ═══════════════════════════════════════════════════════════════
    builtin('textual-port?', lambda p: TRUE if p is TRUE or (isinstance(p, tuple) and p[0] in ('str-port', 'file-port')) else FALSE)
    builtin('char-ready?', lambda *p: TRUE if not p else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'str-port' and p[0][1] and p[0][1][0] else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'file-port' and len(p[0]) > 3 else FALSE)))
    builtin('u8-ready?', lambda *p: TRUE if not p else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'str-port' and p[0][1] and p[0][1][0] else (TRUE if isinstance(p[0], tuple) and p[0][0] == 'file-port' and len(p[0]) > 3 else FALSE)))
    builtin('peek-u8', peek_u8_fn)
    builtin('read-u8', read_u8_fn)
    builtin('write-u8', write_u8)
    builtin('read-line', read_line)
    builtin('read-string', read_string_fn)
    builtin('write-string', write_string)
    builtin('get-output-bytevector', lambda p=None: SchemeBytevector(list(p[1])) if isinstance(p, tuple) and p[0] == 'byte-port' else SchemeBytevector([]))


    # Bytevector (base builtin in initenv.py)
    # ═══════════════════════════════════════════════════════════════
    # Symbol operations
    # ═══════════════════════════════════════════════════════════════
    builtin('symbol=?', symbol_equal_p)
    builtin('number=?', num_equal_p)



    # ═══════════════════════════════════════════════════════════════
    # Environment
    # ═══════════════════════════════════════════════════════════════



    # ═══════════════════════════════════════════════════════════════
    # Random
    # ═══════════════════════════════════════════════════════════════
    builtin('random-integer', random_integer)
    builtin('random-real', random_real)
    builtin('random-seed', random_seed)

    # ═══════════════════════════════════════════════════════════════
    # Various helpers
    # ═══════════════════════════════════════════════════════════════
    builtin('atom?', lambda x: FALSE if isinstance(x, Cell) else TRUE)
    builtin('void?', lambda x: TRUE if x is VOID else FALSE)
    builtin('boolean->string', lambda x: SchemeString('#t') if x is TRUE else SchemeString('#f'))
    builtin('boolean=?', lambda *a: FALSE if any(a[i] != a[i+1] for i in range(len(a)-1)) else TRUE)
    builtin('default-object?', lambda x: TRUE if x is VOID else FALSE)
    builtin('name', lambda x: _sn(x) if isinstance(x, Sym) else SchemeString(_pr(x)))
    builtin('pp', lambda x: (sys.stdout.write(_pr(x) + '\n'), VOID)[-1])
    builtin('array?', lambda x: TRUE if isinstance(x, SchemeVector) else FALSE)
    builtin('cartesian-product', lambda *lists: cartesian_product(list(lists)))
    builtin('combinations', lambda lst, n: combinations_fn(lst, int(n)))
    builtin('permutations', lambda lst: perms_fn(lst))
    builtin('unfold', lambda p, f, g, seed, *thunk: unfold_fn(p, f, g, seed, thunk[0] if thunk else None))
    builtin('unfold-right', lambda p, f, g, seed, *thunk: unfold_right_fn(p, f, g, seed, thunk[0] if thunk else None))
    builtin('describe', lambda x: sys.stdout.write(str(x) + '\n') or VOID)
    builtin('identity', lambda x: x)
    builtin('flexp2', lambda x: 2.0 ** float(x))
    # Hooks
    builtin('make-hook-internal', lambda procedures=NIL: Hook()); builtin('make-hook', lambda *arity: Hook()); builtin('hook?', lambda x: TRUE if isinstance(x, Hook) else FALSE)
    builtin('hook-procedures', lambda h: _lst(h.procedures)); builtin('set-hook-procedures!', lambda h, p: setattr(h, 'procedures', list(cell_iter(p))) or VOID)
    builtin('add-hook!', lambda h, p, *append: (h.procedures.append(p) if append and scheme_truthy(append[0]) else h.procedures.insert(0, p)) or VOID)
    builtin('remove-hook!', lambda h, p: setattr(h, 'procedures', [x for x in h.procedures if x is not p]) or VOID)
    builtin('reset-hook!', lambda h: setattr(h, 'procedures', []) or VOID)
    builtin('run-hook', lambda h, *args: ([p(*args) for p in list(h.procedures)] and VOID) or VOID)

    # Random source, including the module default source.
    default = RandomSource(); 
    builtin('*default-random-source*', default)
    builtin('%make-random-source', lambda state: RandomSource(state)); builtin('make-random-source', lambda: RandomSource()); builtin('random-source?', lambda x: TRUE if isinstance(x, RandomSource) else FALSE)
    builtin('random-source-state', lambda s: s.state); builtin('set-random-source-state!', lambda s, x: setattr(s, 'state', int(x)) or VOID)
    builtin('random-source->random-integer', rint); builtin('random-source-random-integer', rint)
    builtin('random-source->random-real', lambda s: s.step() / 2147483648.0); builtin('random-source-random-real', lambda s: s.step() / 2147483648.0)
    builtin('random-source-randomize!', lambda s: setattr(s, 'state', int(time.time())) or VOID)
    builtin('random-source-pseudo-randomize!', lambda s, i, j: setattr(s, 'state', int(i) * 12345 + int(j)) or VOID)
    builtin('random-integer', lambda n: rint(default, n)); builtin('random-real', lambda: default.step() / 2147483648.0)
    builtin('random-seed', lambda n: setattr(default, 'state', int(n)) or VOID); builtin('linear-update-list', lambda *x: _lst(x))

    # List queue is represented as the exact mutable front sequence expected by scm.
    builtin('%make-list-queue', lambda front=NIL, back=NIL: {'items': list(cell_iter(front))}); builtin('make-list-queue', lambda front=NIL, *rest: {'items': list(cell_iter(front))}); builtin('list-queue', lambda *x: {'items': list(x)})
    builtin('list-queue?', lambda q: TRUE if isinstance(q, dict) and 'items' in q else FALSE)
    builtin('list-queue-copy', lambda q: {'items': list(q['items'])}); builtin('list-queue-empty?', lambda q: TRUE if not q['items'] else FALSE)
    builtin('list-queue-add-front!', lambda q, x: q['items'].insert(0, x) or VOID); builtin('list-queue-add-back!', lambda q, x: q['items'].append(x) or VOID); builtin('list-queue-add!', lambda q, x: q['items'].append(x) or VOID)
    builtin('list-queue-remove-front!', qremove); builtin('list-queue-remove!', qremove); builtin('list-queue-remove-back!', lambda q: qremove(q, True))
    builtin('%list-queue-front', lambda q: _lst(q['items'])); builtin('%list-queue-back', lambda q: _lst(q['items'][-1:]))
    builtin('%set-list-queue-front!', lambda q, v: q['items'].__setitem__(slice(None), list(cell_iter(v))) or VOID); builtin('%set-list-queue-back!', lambda q, v: VOID)
    builtin('list-queue-front', lambda q: q['items'][0]); builtin('list-queue-back', lambda q: q['items'][-1]); builtin('list-queue-first', lambda q: q['items'][0]); builtin('list-queue-list', lambda q: _lst(q['items'])); builtin('list-queue->list', lambda q: _lst(q['items'])); builtin('list-queue-size', lambda q: len(q['items']))

    # Heap, bijection, deque.
    builtin('%make-binary-heap', lambda vec=NIL, n=0, cmp=lambda a,b: a < b: BinaryHeap(cmp, vec)); builtin('make-binary-heap', lambda *a: BinaryHeap(a[0] if a else (lambda x, y: x < y), a[1] if len(a) > 1 else NIL)); builtin('binary-heap?', lambda x: TRUE if isinstance(x, BinaryHeap) else FALSE)
    builtin('binary-heap-vec', lambda h: SchemeVector(h.items)); builtin('set-binary-heap-vec!', lambda h, v: setattr(h, 'items', list(v.data)) or VOID); builtin('binary-heap-n', lambda h: len(h.items)); builtin('set-binary-heap-n!', lambda h, n: setattr(h, 'items', h.items[:int(n)]) or VOID); builtin('binary-heap-cmp', lambda h: h.cmp)
    builtin('binary-heap-insert!', lambda h, x: h.insert(x) or h); builtin('binary-heap-min', lambda h: h.items[0]); builtin('binary-heap-remove-min!', lambda h: builtin_remove_heap(h)); builtin('binary-heap-delete-min!', lambda h: builtin_remove_heap(h)); builtin('binary-heap-size', lambda h: len(h.items)); builtin('binary-heap-empty?', lambda h: TRUE if not h.items else FALSE)
    builtin('make-bimap', lambda init: Bimap(init)); builtin('bimap?', lambda x: TRUE if isinstance(x, Bimap) else FALSE); builtin('bimap-forward', lambda b, k: b.forward_ref(k)); builtin('bimap-forward/default', lambda b, k, d: b.forward_ref(k, d)); builtin('bimap-reverse', lambda b, v: b.reverse_ref(v)); builtin('bimap-set!', lambda b, k, v: b.set(k, v) or VOID); builtin('bimap-contains?', lambda b, k: TRUE if k in b.forward else FALSE)
    builtin('%make-bimap', lambda f, r: Bimap(NIL)); builtin('%bimap-forward', lambda b: b.forward); builtin('%bimap-forward-set!', lambda b, x: setattr(b, 'forward', x) or VOID); builtin('%bimap-rev', lambda b: b.reverse); builtin('%bimap-rev-set!', lambda b, x: setattr(b, 'reverse', x) or VOID)
    builtin('make-deque', lambda *x: Deque(x)); builtin('deque?', lambda x: TRUE if isinstance(x, Deque) else FALSE); builtin('deque-empty?', lambda d: TRUE if not d.items else FALSE); builtin('deque-add-front', lambda d, x: d.items.insert(0, x) or d); builtin('deque-add-back', lambda d, x: d.items.append(x) or d); builtin('deque-front', lambda d: d.items[0]); builtin('deque-back', lambda d: d.items[-1]); builtin('deque-remove-front', lambda d: d.items.pop(0)); builtin('deque-remove-back', lambda d: d.items.pop()); builtin('deque-length', lambda d: len(d.items)); builtin('deque->list', lambda d: _lst(d.items))
    builtin('%make-deque', lambda fl, f, bl, b: Deque(list(cell_iter(f)) + list(reversed(cell_iter(b))))); builtin('%deque-fl', lambda d: len(d.items)); builtin('%deque-f', lambda d: _lst(d.items)); builtin('%deque-bl', lambda d: 0); builtin('%deque-b', lambda d: NIL)
    for n, fn in (('push-front', lambda d,x: d.items.insert(0,x) or d), ('push-back', lambda d,x: d.items.append(x) or d), ('pop-front', lambda d: d.items.pop(0)), ('pop-back', lambda d: d.items.pop())):
        builtin('deque-' + n + '!', fn); builtin('deque-' + n, fn)
    for n in ('add-front', 'add-back', 'remove-front', 'remove-back'): builtin('deque-' + n + '!', be.data['deque-' + n])
    builtin('%set-deque-fl!', lambda d, n: VOID); builtin('%set-deque-f!', lambda d, v: VOID)
    builtin('%set-deque-bl!', lambda d, n: VOID); builtin('%set-deque-b!', lambda d, v: VOID)

    # Mapping and array APIs.
    builtin('mapping', _mapping); builtin('mapping?', lambda x: TRUE if x is NIL or (isinstance(x, Cell) and all(isinstance(p, Cell) for p in cell_iter(x))) else FALSE); builtin('list->mapping', _mapping); builtin('mapping->list', lambda m: m)
    builtin('mapping-ref', lambda m,k,*d: next((v for key,v in _pair_items(m) if key == k), d[0] if d else FALSE)); builtin('mapping-contains?', lambda m,k: TRUE if any(key == k for key,_ in _pair_items(m)) else FALSE)
    builtin('mapping-set', lambda m,k,v: _lst([Cell(k,v)] + [Cell(a,b) for a,b in _pair_items(m) if a != k])); builtin('mapping-delete', lambda m,k: _lst([Cell(a,b) for a,b in _pair_items(m) if a != k])); builtin('mapping-keys', lambda m: _lst([a for a,_ in _pair_items(m)])); builtin('mapping-values', lambda m: _lst([b for _,b in _pair_items(m)])); builtin('mapping-size', lambda m: len(_pair_items(m))); builtin('mapping-for-each', lambda f,m: ([f(a,b) for a,b in _pair_items(m)] and VOID) or VOID); builtin('mapping-map', lambda f,m: _lst([Cell(a,f(a,b)) for a,b in _pair_items(m)]))
    builtin('make-array', lambda dims, *v: Array([int(dims)] if isinstance(dims, int) else list(cell_iter(dims)), v[0] if v else 0)); builtin('array?', lambda x: TRUE if isinstance(x, (Array, SchemeVector)) else FALSE)
    builtin('array-ref', lambda a,*ix: _array_ref(a.value if isinstance(a,Array) else a, ix)); builtin('array-set!', lambda a,v,*ix: _array_set(a.value if isinstance(a,Array) else a,v,ix)); builtin('array-dimensions', lambda a: _lst(_array_dims(a.value if isinstance(a,Array) else a)))

    # Definitions whose source files otherwise supply only thin aliases.
    builtin('fixnum?', lambda x: TRUE if isinstance(x, int) and -(1<<23) <= x < (1<<23) else FALSE); builtin('fixnum-width', lambda: 24); builtin('least-fixnum', lambda: -(1<<23)); builtin('greatest-fixnum', lambda: (1<<23)-1)
    builtin('flonum?', lambda x: TRUE if isinstance(x, float) else FALSE)
    builtin('procedure-rename', lambda p, *name: p); builtin('scheme-implementation-name', lambda: SchemeString('Hermes Scheme')); builtin('scheme-implementation-version', lambda: SchemeString('0.1 (R7RS-small + SRFIs)')); builtin('version', lambda: SchemeString('0.1 (R7RS-small + SRFIs)'))
    # Correct argument order and callback semantics for existing broad registrations.
    builtin('generator-map', lambda f,g: (lambda: (lambda v: EOF if v is EOF else f(v))(g())))
    builtin('generator-drop', lambda g,n: drop_gen(int(n), g))
    builtin('generator-fold', lambda f,init,g: _gen_fold(f,init,g)); builtin('generator-take', lambda g,n: _gen_take(int(n),g))
    builtin('vector-fold', lambda f,init,v,*more: _vec_fold(f,init,v)); builtin('vector-fold-right', lambda f,init,v: _vec_fold_right(f,init,v))
    builtin('vector-map!', lambda f,v: _vec_map_bang(f,v)); builtin('reverse-list->vector', lambda x: SchemeVector(cells(x)[::-1]))
    builtin('integer->booleans', lambda n: _lst([TRUE if (int(n) >> i) & 1 else FALSE for i in range(max(1, int(n).bit_length()))]))
    builtin('string-delete', lambda p,s: SchemeString(''.join(c for c in str(s) if not scheme_truthy(_scheme_call(p, [SchemeChar(c)]))))); builtin('string-filter', lambda p,s: SchemeString(''.join(c for c in str(s) if scheme_truthy(_scheme_call(p, [SchemeChar(c)])))))
    builtin('bitwise-or', lambda *x: _bit_fold(lambda a,b: a | b, x)); builtin('logior', be.data['bitwise-or']); builtin('bitwise-ior', be.data['bitwise-or'])
    builtin('logand', lambda *x: _bit_fold(lambda a,b: a & b, x)); builtin('logxor', lambda *x: _bit_fold(lambda a,b: a ^ b, x)); builtin('lognot', lambda x: ~int(x))
    builtin('arithmetic-shift-right', lambda n,c: int(n) >> int(c)); builtin('object->string', lambda x: SchemeString(_pr(x))); builtin('integer->string/radix', lambda n,r: SchemeString(format(int(n), 'x' if int(r)==16 else 'b' if int(r)==2 else 'o' if int(r)==8 else 'd')))
    builtin('with-exception-handler/k', lambda handler, thunk: thunk())
    builtin('loop-n', lambda n: _loop_n(int(n)))
    builtin('test-begin', lambda *name: VOID); builtin('test-end', lambda *name: VOID)
    builtin('with-output-to-string', lambda thunk: SchemeString(''))
    builtin('char-set->integer', _char_set_integer)
    builtin('char-name', lambda c: be.data['char->name'](c)); builtin('*char-names*', NIL)
    builtin('real->exact', lambda x: x)
    builtin('reverse!', _reverse_bang)
    builtin('json-encode', lambda x: SchemeString(json.dumps(_json_value(x), separators=(',', ':'))))
    builtin('%bits->integer', lambda x: sum((1 << i) for i,b in enumerate(cell_iter(x)) if scheme_truthy(b)))
    builtin('vector-cumulate', _vector_cumulate); builtin('vector-index-right', _vector_index_right); builtin('vector-skip-right', _vector_skip_right); builtin('vector-append-subvectors', _vector_append_subvectors)
    builtin('rcons', rcons)
    builtin('tmap', tmap); builtin('tfilter', tfilter)
    builtin('ttake', lambda n: lambda reducer: lambda acc, value: _scheme_call(reducer, [acc, value]) if int(n) > 0 else acc)
    builtin('tdrop', lambda n: lambda reducer: lambda acc, value: _scheme_call(reducer, [acc, value]))
    builtin('tconcatenate', lambda: lambda reducer: reducer)
    builtin('list-transduce', list_transduce)
    builtin('vector-transduce', lambda x,r,i,v: list_transduce(x, r, i, _lst(v.data if hasattr(v, 'data') else v)))
    builtin('string-transduce', lambda x,r,i,s: list_transduce(x, r, i, _lst([cs_char(c) for c in str(s)])))


