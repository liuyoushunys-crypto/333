# native_syntax.py — 原生 syntax-rules 编译器
# 把 syntax-rules 宏直接编译成原生 Python 模式匹配+模板展开器。
# 绕过 Scheme 宏引擎 (boot-min2.scm 的 sx-* 系列), 展开时零解释器参与。
# 语义与 boot-min2.scm 的 sx-match/sx-expand 完全等价 (含省略号/卫生/set! 变异)。

from mtypes import (
    Sym, Cell, NIL, Env, _UNBOUND,
)

SYM_UNDERSCORE = Sym('_')
SYM_ELLIPSIS = Sym('...')
SYM_SX_HYGIENE = Sym('sx-hygiene')
SYM_SETBANG = Sym('set!')


def _is_procedure(v):
    return callable(v) or isinstance(v, tuple)


def _scheme_equal(a, b):
    if a is b:
        return True
    if a is NIL or b is NIL:
        return a is b
    if isinstance(a, Sym) and isinstance(b, Sym):
        return a is b
    if isinstance(a, Cell) and isinstance(b, Cell):
        return _scheme_equal(a.car, b.car) and _scheme_equal(a.cdr, b.cdr)
    if isinstance(a, (bool, int, float, complex, str)) and isinstance(b, (bool, int, float, complex, str)):
        return a == b
    return a == b


def _length(cell):
    n = 0
    cur = cell
    while isinstance(cur, Cell):
        n += 1
        cur = cur.cdr
    return n


def _list_ref(cell, i):
    cur = cell
    for _ in range(i):
        cur = cur.cdr
    return cur.car


def _memq(x, lst):
    for item in lst:
        if item is x:
            return True
    return False


def _assq(var, bindings):
    for (v, val) in bindings:
        if v is var:
            return (v, val)
    return None


# ── 模式匹配 (sx-match 等价) ─────────────────────────────────

def sx_match(pat, inp, lits):
    if pat is NIL:
        return [] if inp is NIL else None
    if isinstance(pat, Sym):
        return sx_match_sym(pat, inp, lits)
    if not isinstance(pat, Cell):
        return [] if _scheme_equal(pat, inp) else None
    cdr = pat.cdr
    if isinstance(cdr, Cell) and isinstance(cdr.car, Sym) and cdr.car is SYM_ELLIPSIS:
        return sx_match_ellipsis(pat.car, cdr.cdr, inp, lits)
    return sx_match_pair(pat, inp, lits)


def sx_match_sym(pat, inp, lits):
    if pat is SYM_UNDERSCORE:
        return []
    if _memq(pat, lits):
        if isinstance(inp, Sym) and inp is pat:
            return []
        return None
    return [(pat, inp)]


def sx_match_pair(pat, inp, lits):
    if not isinstance(inp, Cell):
        return None
    b1 = sx_match(pat.car, inp.car, lits)
    if b1 is None:
        return None
    b2 = sx_match(pat.cdr, inp.cdr, lits)
    if b2 is None:
        return None
    return list(b2) + list(b1)


def sx_match_ellipsis(prefix, rest_pat, inp, lits):
    res = sx_match_ellipsis_loop(prefix, rest_pat, inp, lits, [])
    return sx_match_ellipsis_finish(prefix, rest_pat, res, lits)


def sx_match_ellipsis_loop(prefix, rest_pat, inp, lits, groups):
    if not isinstance(inp, Cell):
        return (inp, groups)
    b = sx_match(prefix, inp.car, lits)
    if b is not None:
        if rest_pat is NIL:
            return sx_match_ellipsis_loop(prefix, rest_pat, inp.cdr, lits, groups + [b])
        if sx_match(rest_pat, inp, lits) is not None:
            return (inp, groups)
        return sx_match_ellipsis_loop(prefix, rest_pat, inp.cdr, lits, groups + [b])
    return (inp, groups)


def sx_match_ellipsis_finish(prefix, rest_pat, res, lits):
    remaining_in, groups = res
    evars = sx_pattern_vars(prefix)
    if rest_pat is NIL:
        if remaining_in is NIL:
            return sx_accum_ellipsis(evars, groups, [])
        return None
    rb = sx_match(rest_pat, remaining_in, lits)
    if rb is not None:
        return sx_accum_ellipsis(evars, groups, rb)
    return None


def sx_pattern_vars(pat):
    stack = [pat]
    acc = []
    while stack:
        curr = stack.pop()
        if isinstance(curr, Sym):
            if curr is not SYM_UNDERSCORE and curr is not SYM_ELLIPSIS:
                acc.append(curr)
        elif isinstance(curr, Cell):
            stack.append(curr.cdr)
            stack.append(curr.car)
    return acc


def sx_accum_ellipsis(vars_, groups, base):
    if not vars_:
        return base
    v = vars_[0]
    vals = []
    for g in groups:
        p = _assq(v, g)
        vals.append(p[1] if p is not None else NIL)
    new_binding = (v, _reverse_list(vals))
    rest = sx_accum_ellipsis(vars_[1:], groups, base)
    return rest + [new_binding]


def _reverse_list(items):
    out = NIL
    for x in reversed(items):
        out = Cell(x, out)
    return out


# ── 模板展开 (sx-expand 等价) ─────────────────────────────────

def sx_expand(tmpl, bindings, mutated, def_env):
    if isinstance(tmpl, Sym):
        return sx_expand_sym(tmpl, bindings, mutated, def_env)
    if not isinstance(tmpl, Cell):
        return tmpl
    cdr = tmpl.cdr
    if isinstance(cdr, Cell) and isinstance(cdr.car, Sym) and cdr.car is SYM_ELLIPSIS:
        return sx_expand_ellipsis(tmpl.car, cdr.cdr, bindings, mutated, def_env)
    return sx_expand_pair(tmpl, bindings, mutated, def_env)


def sx_expand_pair(tmpl, bindings, mutated, def_env):
    return Cell(
        sx_expand(tmpl.car, bindings, mutated, def_env),
        sx_expand(tmpl.cdr, bindings, mutated, def_env),
    )


def sx_expand_sym(tmpl, bindings, mutated, def_env):
    p = _assq(tmpl, bindings)
    if p is not None:
        return p[1]
    if tmpl is SYM_UNDERSCORE or tmpl is SYM_ELLIPSIS:
        return tmpl
    if _memq(tmpl, mutated):
        return tmpl
    v = def_env.lookup_silent(tmpl.name, _UNBOUND)
    if v is not _UNBOUND and not _is_procedure(v):
        return Cell(SYM_SX_HYGIENE, Cell(tmpl, NIL))
    return tmpl


def sx_expand_ellipsis(sub, rest, bindings, mutated, def_env):
    evars = sx_ellipsis_vars(sub, bindings)
    if evars:
        p = _assq(evars[0], bindings)
        cnt = _length(p[1]) if p is not None else 0
    else:
        cnt = sx_find_list_count(bindings)
    return sx_repeat(sub, rest, bindings, evars, cnt, mutated, def_env)


def sx_ellipsis_vars(sub, bindings):
    out = []
    for v in sx_pattern_vars(sub):
        p = _assq(v, bindings)
        if p is not None:
            val = p[1]
            if isinstance(val, Cell) or val is NIL:
                out.append(v)
    return out


def sx_find_list_count(bindings):
    for (v, val) in bindings:
        if isinstance(val, Cell):
            return _length(val)
    return 0


def sx_repeat(sub, rest, bindings, evars, cnt, mutated, def_env):
    res = sx_expand(rest, bindings, mutated, def_env)
    i = cnt - 1
    while i >= 0:
        if evars:
            sub_b = sx_sub_bindings(evars, bindings, i)
            res = Cell(sx_expand(sub, sub_b, mutated, def_env), res)
        else:
            res = Cell(sx_expand(sub, bindings, mutated, def_env), res)
        i -= 1
    return res


def sx_sub_bindings(evars, bindings, i):
    out = []
    for v in evars:
        p = _assq(v, bindings)
        lst = p[1] if p is not None else NIL
        val = _list_ref(lst, i) if i < _length(lst) else NIL
        out.append((v, val))
    return out


# ── set! 变异收集 (sx-collect-set-targets 等价) ──────────────

def sx_collect_set_targets(tmpl, acc):
    if not isinstance(tmpl, Cell):
        return acc
    if isinstance(tmpl.car, Sym) and tmpl.car is SYM_SETBANG:
        cdr = tmpl.cdr
        if isinstance(cdr, Cell) and isinstance(cdr.car, Sym):
            return sx_collect_set_targets(cdr, [cdr.car] + acc)
        return sx_collect_set_targets(cdr, acc)
    return sx_collect_set_targets(tmpl.car, sx_collect_set_targets(tmpl.cdr, acc))


# ── 编译器入口 ──────────────────────────────────────────────

def compile_syntax_rules(lits, rules, def_env):
    """rules: Cell of (pat tmpl) 序对列表。返回 native_expand(args) 或 None。"""
    rule_list = []
    cur = rules
    while isinstance(cur, Cell):
        rule = cur.car
        if isinstance(rule, Cell):
            pat = rule.car
            tmpl = rule.cdr.car if (isinstance(rule.cdr, Cell)) else NIL
            pat_args = pat.cdr if isinstance(pat, Cell) else NIL
            mutated = sx_collect_set_targets(tmpl, [])
            rule_list.append((pat_args, tmpl, mutated))
        cur = cur.cdr
    lits_list = []
    cur = lits
    while isinstance(cur, Cell):
        lits_list.append(cur.car)
        cur = cur.cdr

    def native_expand(args):
        for (pat_args, tmpl, mutated) in rule_list:
            b = sx_match(pat_args, args, lits_list)
            if b is not None:
                return sx_expand(tmpl, b, mutated, def_env)
        raise Exception("syntax-rules: no match")

    return native_expand
