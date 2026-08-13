"""Host implementations for the ordinary procedures in the twelve scm libraries.

This module is deliberately imported by initenv_ext rather than loaded as Scheme.
The values here use the interpreter's Cell/Vector sentinels, so mutation remains
visible to Scheme code and callbacks can be ordinary Scheme procedures.
"""

import math
import json
import random
import time
from functools import cmp_to_key

from mtypes import Cell, SchemeChar, SchemeString, SchemeVector, NIL, VOID, TRUE, FALSE, EOF, Sym, _lst, builtin, be
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


def _register_record_hosts():
    # Hooks
    builtin('make-hook-internal', lambda procedures=NIL: Hook()); builtin('make-hook', lambda *arity: Hook()); builtin('hook?', lambda x: TRUE if isinstance(x, Hook) else FALSE)
    builtin('hook-procedures', lambda h: _lst(h.procedures)); builtin('set-hook-procedures!', lambda h, p: setattr(h, 'procedures', list(cell_iter(p))) or VOID)
    builtin('add-hook!', lambda h, p, *append: (h.procedures.append(p) if append and scheme_truthy(append[0]) else h.procedures.insert(0, p)) or VOID)
    builtin('remove-hook!', lambda h, p: setattr(h, 'procedures', [x for x in h.procedures if x is not p]) or VOID)
    builtin('reset-hook!', lambda h: setattr(h, 'procedures', []) or VOID)
    builtin('run-hook', lambda h, *args: ([p(*args) for p in list(h.procedures)] and VOID) or VOID)

    # Random source, including the module default source.
    default = RandomSource(); builtin('*default-random-source*', default)
    builtin('%make-random-source', lambda state: RandomSource(state)); builtin('make-random-source', lambda: RandomSource()); builtin('random-source?', lambda x: TRUE if isinstance(x, RandomSource) else FALSE)
    builtin('random-source-state', lambda s: s.state); builtin('set-random-source-state!', lambda s, x: setattr(s, 'state', int(x)) or VOID)
    def rint(s, n): return int(round(s.step() / 2147483648.0 * int(n))) % int(n)
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
    def qremove(q, end=False):
        if not q['items']: raise ValueError('empty list queue')
        return q['items'].pop(-1 if end else 0)
    builtin('list-queue-remove-front!', qremove); builtin('list-queue-remove!', qremove); builtin('list-queue-remove-back!', lambda q: qremove(q, True))
    builtin('%list-queue-front', lambda q: _lst(q['items'])); builtin('%list-queue-back', lambda q: _lst(q['items'][-1:]))
    builtin('%set-list-queue-front!', lambda q, v: q['items'].__setitem__(slice(None), list(cell_iter(v))) or VOID); builtin('%set-list-queue-back!', lambda q, v: VOID)
    builtin('list-queue-front', lambda q: q['items'][0]); builtin('list-queue-back', lambda q: q['items'][-1]); builtin('list-queue-first', lambda q: q['items'][0]); builtin('list-queue-list', lambda q: _lst(q['items'])); builtin('list-queue->list', lambda q: _lst(q['items'])); builtin('list-queue-size', lambda q: len(q['items']))

    # Heap, bijection, deque.
    builtin('%make-binary-heap', lambda vec=NIL, n=0, cmp=lambda a,b: a < b: BinaryHeap(cmp, vec)); builtin('make-binary-heap', lambda *a: BinaryHeap(a[0] if a else (lambda x, y: x < y), a[1] if len(a) > 1 else NIL)); builtin('binary-heap?', lambda x: TRUE if isinstance(x, BinaryHeap) else FALSE)
    builtin('binary-heap-vec', lambda h: SchemeVector(h.items)); builtin('set-binary-heap-vec!', lambda h, v: setattr(h, 'items', list(v.data)) or VOID); builtin('binary-heap-n', lambda h: len(h.items)); builtin('set-binary-heap-n!', lambda h, n: setattr(h, 'items', h.items[:int(n)]) or VOID); builtin('binary-heap-cmp', lambda h: h.cmp)
    builtin('binary-heap-insert!', lambda h, x: h.insert(x) or h); builtin('binary-heap-min', lambda h: h.items[0]); builtin('binary-heap-remove-min!', lambda h: builtin_remove_heap(h)); builtin('binary-heap-delete-min!', lambda h: builtin_remove_heap(h)); builtin('binary-heap-size', lambda h: len(h.items)); builtin('binary-heap-empty?', lambda h: TRUE if not h.items else FALSE)
    builtin('make-bimap', lambda init: Bimap(init)); builtin('bimap?', lambda x: TRUE if isinstance(x, Bimap) else FALSE); builtin('bimap-forward', lambda b, k: b.forward[k]); builtin('bimap-forward/default', lambda b, k, d: b.forward.get(k, d)); builtin('bimap-reverse', lambda b, v: b.reverse[v]); builtin('bimap-set!', lambda b, k, v: b.set(k, v) or VOID); builtin('bimap-contains?', lambda b, k: TRUE if k in b.forward else FALSE)
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


def register_scm12_host():
    _register_record_hosts()
    # Correct argument order and callback semantics for existing broad registrations.
    builtin('generator-map', lambda f,g: (lambda: (lambda v: EOF if v is EOF else f(v))(g())))
    def drop_gen(n, g):
        for _ in range(n):
            if g() is EOF: break
        return g
    builtin('generator-drop', lambda n,g: drop_gen(int(n), g))
    builtin('generator-fold', lambda f,init,g: _gen_fold(f,init,g)); builtin('generator-take', lambda n,g: _gen_take(int(n),g))
    builtin('vector-fold', lambda f,init,v,*more: _vec_fold(f,init,v)); builtin('vector-fold-right', lambda f,init,v: _vec_fold_right(f,init,v))
    builtin('vector-map!', lambda f,v: _vec_map_bang(f,v)); builtin('reverse-list->vector', lambda x: SchemeVector(cells(x)[::-1]))
    builtin('integer->booleans', lambda n: _lst([TRUE if b else FALSE for b in reversed([(int(n)>>i)&1 for i in range(max(1,int(n).bit_length()))])]))
    builtin('string-delete', lambda p,s: SchemeString(''.join(c for c in str(s) if not scheme_truthy(_scheme_call(p, [SchemeChar(c)]))))); builtin('string-filter', lambda p,s: SchemeString(''.join(c for c in str(s) if scheme_truthy(_scheme_call(p, [SchemeChar(c)])))))
    builtin('bitwise-or', lambda *x: _bit_fold(lambda a,b: a | b, x)); builtin('logior', be.data['bitwise-or']); builtin('bitwise-ior', be.data['bitwise-or'])
    builtin('logand', lambda *x: _bit_fold(lambda a,b: a & b, x)); builtin('logxor', lambda *x: _bit_fold(lambda a,b: a ^ b, x)); builtin('lognot', lambda x: ~int(x))
    builtin('arithmetic-shift-right', lambda n,c: int(n) >> int(c)); builtin('object->string', lambda x: SchemeString(str(x))); builtin('integer->string/radix', lambda n,r: SchemeString(format(int(n), 'x' if int(r)==16 else 'b' if int(r)==2 else 'o' if int(r)==8 else 'd')))
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
    builtin('tmap', lambda f: lambda g: (lambda: _map_value(f, g()))); builtin('tfilter', lambda p: lambda g: _filter_value(p,g)); builtin('ttake', lambda n: lambda g: _gen_take(int(n),g)); builtin('tdrop', lambda n: lambda g: drop_gen(int(n),g)); builtin('tconcatenate', lambda: lambda *g: be.data['generator-append'](*g))
    builtin('list-transduce', lambda x,r,i,l: _lst([r(x, i, v) for v in cell_iter(l)])); builtin('vector-transduce', lambda x,r,i,v: _vec_fold(lambda j,a,z:r(x,a,z),i,v)); builtin('string-transduce', lambda x,r,i,s: _vec_fold(lambda j,a,z:r(x,a,z),i,SchemeVector([cs_char(c) for c in str(s)])))


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
