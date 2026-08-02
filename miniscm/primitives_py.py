from mtypes import (
    Sym, Cell, TRUE, _sn,
    builtin, be
)

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
