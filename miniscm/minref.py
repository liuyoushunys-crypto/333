# ============================================================================
# minref.py — min.py.txt 的可运行版 (boot-min2.scm 的原生 Python 语义等价改写)
# 来源   : min.py.txt (参考实现) — 正文逐函数一致, 仅头部桥接改为真实依赖
# 说明   : REPL ,expand 命令使用本模块的 my_macro_expand 显示宏展开结果
# ============================================================================

from mtypes import (Sym, Cell, NIL, VOID, FALSE, SchemeVector,
                    SchemeException as SchemeError, _UNBOUND,
                    _cells, _cell_len)

# syntax-rules 模式匹配 / 模板展开 / set! 变异收集 与 native_syntax.py 完全重叠,
# 以 native_syntax.py 为单一事实源, 本模块不再重复实现。
from native_syntax import (
    _length, _list_ref, _memq, _assq, _is_procedure, _scheme_equal,
    sx_match, sx_match_sym, sx_match_pair,
    sx_match_ellipsis, sx_match_ellipsis_finish, sx_match_ellipsis_loop,
    sx_pattern_vars, sx_accum_ellipsis,
    sx_expand, sx_expand_pair, sx_expand_sym,
    sx_expand_ellipsis, sx_ellipsis_vars, sx_find_list_count,
    sx_repeat, sx_sub_bindings, sx_collect_set_targets,
)

def eval(expr, env):                    # 依赖原语: (eval expr env)
    from miniscm import _eval
    return _eval(expr, env)

def eval_qs(expr, env):
    """Evaluate an unsyntax expression with syntax-case bindings in scope."""
    if isinstance(expr, Sym):
        value = sx_lookup(expr, sx_get_bindings())
        if value is not None:
            return value
    return eval(expr, env)

def sx_expand_call(expr, env):          # (sx-expand-call expr env)
    from prim import _sx_expand_call
    return _sx_expand_call(expr, env)

def sx_expand_env():                    # (sx-expand-env)
    from prim import _sx_expand_env
    return _sx_expand_env()

def sx_def_env():                       # (sx-def-env)
    from prim import _sx_def_env
    return _sx_def_env()

def sx_defined(tmpl, env):              # (sx-defined? tmpl env)
    return env.lookup_silent(tmpl.name, _UNBOUND) is not _UNBOUND

def list_to_vector(lst):                # (list->vector lst)
    return SchemeVector(_cells(lst))

def vector_to_list(v):                  # (vector->list v)
    return _to_cell(v.data)

def is_vector(x):                       # (vector? x)
    return isinstance(x, SchemeVector)

def string_to_symbol(s):                # (string->symbol s)
    return Sym(s)


# ── 符号常量 ──────────────────────────────────────────────────────────────
SYM_UNDERSCORE = Sym('_')
SYM_ELLIPSIS = Sym('...')
SYM_QUOTE = Sym('quote')
SYM_QUASIQUOTE = Sym('quasiquote')
SYM_UNQUOTE = Sym('unquote')
SYM_UNQUOTE_SPLICING = Sym('unquote-splicing')
SYM_UNSYNTAX = Sym('unsyntax')
SYM_UNSYNTAX_SPLICING = Sym('unsyntax-splicing')
SYM_QUASISYNTAX = Sym('quasisyntax')
SYM_SYNTAX_RULES = Sym('syntax-rules')
SYM_LAMBDA = Sym('lambda')
SYM_SETBANG = Sym('set!')
SYM_SX_HYGIENE = Sym('sx-hygiene')
SYM_DEFINE_MACRO = Sym('define-macro')
SYM_SX_DISPATCH = Sym('sx-dispatch')
SYM_CONS = Sym('cons')
SYM_ARGS = Sym('args')


# ── 基础工具 ──────────────────────────────────────────────────────────────
# _scheme_equal/_is_procedure/_length/_list_ref/_memq/_assq 见 native_syntax.py (导入)

def _to_cell(items):                            # list → Cell 链
    out = NIL
    for x in reversed(items):
        out = Cell(x, out)
    return out


def _iter_cells(lst):                           # Cell 链 → list
    out = []
    cur = lst
    while isinstance(cur, Cell):
        out.append(cur.car)
        cur = cur.cdr
    return out


# ── 基础 ──────────────────────────────────────────────────────────────────

def atom(x):                                    # 1: (define (atom? x) (not (pair? x)))
    return not isinstance(x, Cell)


void_sentinel = VOID                            # 2: (define void-sentinel (void))


def is_void(x):                                 # 3: (define (void? x) (eq? x void-sentinel))
    return x is void_sentinel


nil = NIL                                       # 4: (define nil '())


# ── 宏展开入口 ────────────────────────────────────────────────────────────

def my_macro_expand(expr, env):                 # 5: (define (my-macro-expand expr env) ...)
    return my_macro_expand_helper(expr, env)


def my_macro_expand_helper(expr, env):          # 6: (define (my-macro-expand-helper expr env) ...)
    if not isinstance(expr, Cell):
        return expr
    if expr.car is SYM_QUOTE:                   # (eq? (car expr) 'quote)
        return expr
    if expr.car is SYM_QUASIQUOTE:              # (eq? (car expr) 'quasiquote)
        return expr
    expanded = sx_expand_call(expr, env)        # (sx-expand-call expr env)
    if expanded is FALSE:                       # (eq? expanded #f) → 非宏调用, 递归展开子结构
        return Cell(my_macro_expand(expr.car, env),
                    my_macro_expand(expr.cdr, env))
    if _scheme_equal(expanded, expr):           # (equal? expanded expr) → 恒等展开, 停止
        return expr
    return my_macro_expand_helper(expanded, env)


# ── 模式绑定 (my-definemacro 机制) ────────────────────────────────────────

def my_bind_pattern(pattern, args):             # 7: (define (my-bind-pattern pattern args) ...)
    if isinstance(pattern, Sym):
        if pattern is SYM_UNDERSCORE:           # (eq? pattern (quote _))
            return []
        return [(pattern, args)]                # (list (cons pattern args))
    if not isinstance(pattern, Cell) or pattern is NIL:
        return []
    return my_bind_elem(pattern.car, args.car) + \
        my_bind_pattern(pattern.cdr, args.cdr)


def my_bind_elem(elem, arg):                    # 8: (define (my-bind-elem elem arg) ...)
    if elem is SYM_UNDERSCORE:
        return []
    if isinstance(elem, Sym):                   # (symbol? elem)
        return [(elem, arg)]
    if (isinstance(elem, Cell) and isinstance(elem.car, Sym)
            and elem.cdr is NIL):               # (pair? elem) (symbol? (car elem)) (null? (cdr elem))
        return [(elem.car, arg)]
    if isinstance(elem, Cell):
        return my_bind_pattern(elem, arg)
    return []


def sx_macro_expand(pattern, body, args, callenv):  # 9: (define (sx-macro-expand pattern body args callenv) ...)
    # 原 ((lambda (bindings) ((lambda (app-form) (eval app-form callenv)) ...)) ...)
    # 还原 let: bindings → params/quoted-vals → app-form → eval
    bindings = my_bind_pattern(pattern, args)
    params = [b[0] for b in bindings]           # (map (lambda (b) (car b)) bindings)
    quoted_vals = [Cell(SYM_QUOTE, Cell(b[1], NIL)) for b in bindings]
    app_form = Cell(Cell(SYM_LAMBDA, Cell(_to_cell(params), body)),
                    _to_cell(quoted_vals))
    return eval(app_form, callenv)              # (eval app-form callenv)


# 10-11 忽略: define-macro 机制 (my-definemacro 及 define-macro 语法注册)


# ── quasiquote 处理 ────────────────────────────────────────────────────────

def qq_reverse_helper(src, dst):                # 12: (define (qq-reverse-helper src dst) ...)
    if src is NIL:
        return dst
    return qq_reverse_helper(src.cdr, Cell(src.car, dst))


def qq_reverse(l):                              # 13: (define (qq-reverse l) ...)
    return qq_reverse_helper(l, NIL)


def qq_append_lists(a, b):                      # 14: (define (qq-append-lists a b) ...)
    if a is NIL:
        return b
    return Cell(a.car, qq_append_lists(a.cdr, b))


def qq_build_list(items, tail):                 # 15: (define (qq-build-list items tail) ...)
    if items is NIL:
        return tail
    return qq_build_list(items.cdr, Cell(items.car, tail))


def qq_unquote(x):                              # 16: (define (qq-unquote? x) ...)
    return isinstance(x, Cell) and x.car is SYM_UNQUOTE


def qq_unsplice(x):                             # 17: (define (qq-unsplice? x) ...)
    return isinstance(x, Cell) and x.car is SYM_UNQUOTE_SPLICING


def qq_tail_unquote(tail):                      # 18: (define (qq-tail-unquote? tail) ...)
    return isinstance(tail, Cell) and tail.car is SYM_UNQUOTE


def qq_tail_unsplice(tail):                     # 19: (define (qq-tail-unsplice? tail) ...)
    return isinstance(tail, Cell) and tail.car is SYM_UNQUOTE_SPLICING


def qq_process_el(el, items, env):              # 20: (define (qq-process-el el items env) ...)
    if qq_unquote(el):                          # (qq-unquote? el)
        return Cell(eval(el.cdr.car, env), items)       # (cons (eval (cadr el) env) items)
    if qq_unsplice(el):                         # (qq-unsplice? el)
        # 还原 let: v = (eval (cadr el) env)
        v = eval(el.cdr.car, env)
        if isinstance(v, Cell):                 # (pair? v)
            return qq_append_lists(qq_reverse(v), items)
        if v is NIL:                            # (null? v)
            return items
        return Cell(v, items)                   # (cons v items)
    if isinstance(el, Cell):                    # (pair? el)
        if el.car is SYM_QUASIQUOTE:            # (eq? (car el) 'quasiquote)
            return Cell(el, items)
        return Cell(qq_walk(el, env), items)
    return Cell(qq_walk(el, env), items)


def qq_walk_list_helper(cur, items, env):       # 21: (define (qq-walk-list-helper cur items env) ...)
    if cur is NIL:                              # (null? cur)
        return qq_reverse(items)
    if not isinstance(cur, Cell):               # (not (pair? cur))
        return qq_build_list(qq_reverse(items), cur)
    # 还原 let: new-items / tail
    new_items = qq_process_el(cur.car, items, env)
    tail = cur.cdr
    if qq_tail_unquote(tail):                   # (qq-tail-unquote? tail)
        v = eval(tail.cdr.car, env)             # (eval (cadr tail) env)
        return qq_build_list(qq_reverse(new_items), v)
    if qq_tail_unsplice(tail):                  # (qq-tail-unsplice? tail)
        v = eval(tail.cdr.car, env)
        if isinstance(v, Cell):                 # (pair? v)
            return qq_walk_list_helper(tail, qq_append_lists(qq_reverse(v), new_items), env)
        if v is NIL:                            # (null? v)
            return qq_walk_list_helper(tail, new_items, env)
        return qq_walk_list_helper(tail, Cell(v, new_items), env)   # (cons v new-items)
    return qq_walk_list_helper(tail, new_items, env)


def qq_walk_list(e, env):                       # 22: (define (qq-walk-list e env) ...)
    return qq_walk_list_helper(e, NIL, env)


def qq_walk_vector_helper(cur, items, env):     # 23: (define (qq-walk-vector-helper cur items env) ...)
    if cur is NIL:                              # (null? cur)
        return list_to_vector(qq_reverse(items))  # (list->vector (qq-reverse items))
    el = cur.car
    if qq_unquote(el):                          # (qq-unquote? el)
        return qq_walk_vector_helper(cur.cdr,
                                     Cell(eval(el.cdr.car, env), items), env)
    if qq_unsplice(el):                         # (qq-unsplice? el)
        # 还原 let: v = (eval (cadr el) env)
        v = eval(el.cdr.car, env)
        if isinstance(v, Cell):                 # (pair? v)
            return qq_walk_vector_helper(cur.cdr, qq_append_lists(qq_reverse(v), items), env)
        if v is NIL:                            # (null? v)
            return qq_walk_vector_helper(cur.cdr, items, env)
        return qq_walk_vector_helper(cur.cdr, Cell(v, items), env)  # (cons v items)
    return qq_walk_vector_helper(cur.cdr,
                                 Cell(qq_walk(el, env), items), env)


def qq_walk_vector(v, env):                     # 24: (define (qq-walk-vector v env) ...)
    return qq_walk_vector_helper(vector_to_list(v), NIL, env)


def qq_walk(e, env):                            # 25: (define (qq-walk e env) ...)
    if isinstance(e, Cell):                     # (pair? e)
        return qq_walk_list(e, env)
    if is_vector(e):                            # (vector? e)
        return qq_walk_vector(e, env)
    return e


# ── syntax-rules 模式匹配 ─────────────────────────────────────────────────
# sx_pattern_vars / sx_accum_ellipsis / sx_match 系列见 native_syntax.py (导入)

def sx_lookup(var, bindings):                   # 26: (define (sx-lookup var bindings) ...)
    p = _assq(var, bindings)                    # (assq var bindings)
    return p[1] if p is not None else None      # (if b (cdr b) #f)


def sx_merge_bindings(b1, b2):                  # 27: (define (sx-merge-bindings b1 b2) (append b2 b1))
    return b2 + b1


def sx_rev_append(src, acc):                    # 28: (define (sx-rev-append src acc) ...)
    out = acc
    cur = src
    while isinstance(cur, Cell):                # 尾递归 → 迭代
        out = [cur.car] + out                   # (cons (car src) acc)
        cur = cur.cdr
    return out


def sx_reverse(l):                              # 29: (define (sx-reverse l) ...)
    return sx_rev_append(l, [])


def sx_merge_vars(a, b):                        # 32: (define (sx-merge-vars a b) ...)
    out = a
    cur = b
    while isinstance(cur, Cell):
        if not _memq(cur.car, out):             # (memq (car b) a)
            out = [cur.car] + out               # (cons (car b) a)
        cur = cur.cdr
    return out


# ── syntax-rules 模板展开 ─────────────────────────────────────────────────
# sx_expand 系列 / sx_collect_set_targets / sx_ellipsis_vars / sx_find_list_count
# / sx_repeat / sx_sub_bindings 见 native_syntax.py (导入)

_sx_mutated_vars = []                           # 41: (define *sx-mutated-vars* '())

def sx_dispatch(args, lits, rules):             # 54: (define (sx-dispatch args lits rules) ...)
    global _sx_mutated_vars
    lits_list = _iter_cells(lits)               # lits 是 Cell 链 (Scheme 数据) → list (sx_match 需要)
    cur = rules
    while isinstance(cur, Cell):                # (if (null? rules) error ...) → 迭代
        rule = cur.car
        pat = rule.car if isinstance(rule, Cell) else NIL   # (car rule)
        tmpl = sx_rule_tmpl(rule)               # (sx-rule-tmpl rule)
        pat_args = pat.cdr if isinstance(pat, Cell) else NIL  # (if (pair? pat) (cdr pat) '())
        b = sx_match(pat_args, args, lits_list) # (sx-match pat-args args lits)
        if b is not None:
            old_mut = _sx_mutated_vars          # 还原 let: old-mut
            _sx_mutated_vars = sx_collect_set_targets(tmpl, [])  # (set! *sx-mutated-vars* ...)
            r = sx_expand(tmpl, b, _sx_mutated_vars, sx_def_env())  # (sx-expand tmpl b)
            _sx_mutated_vars = old_mut          # (set! *sx-mutated-vars* old-mut)
            return r
        cur = cur.cdr                           # (sx-dispatch args lits (cdr rules))
    raise SchemeError("syntax-rules: no match")


def sx_rule_tmpl(rule):                         # 55: (define (sx-rule-tmpl rule) ...)
    if isinstance(rule, Cell) and isinstance(rule.cdr, Cell):  # (pair? (cdr rule))
        return rule.cdr.car                     # (cadr rule)
    return NIL                                  # '()


# ── 展开状态 (sx-with-bindings) ───────────────────────────────────────────

_sx_bindings = []                               # 56: (define *sx-bindings* '())


def sx_get_bindings():                          # 57: (define (sx-get-bindings) *sx-bindings*)
    return _sx_bindings


def sx_set_bindings(b):                         # 58: (define (sx-set-bindings! b) (set! *sx-bindings* b))
    global _sx_bindings
    _sx_bindings = b


def sx_with_bindings(b, thunk):                 # 59: (define (sx-with-bindings b thunk) ...)
    old = _sx_bindings                          # 还原 let: old
    sx_set_bindings(b)
    try:
        return thunk()                          # (thunk)
    finally:
        sx_set_bindings(old)                    # (set! *sx-bindings* old)


_sx_gensym_counter = 0                          # 60: (define *sx-gensym-counter* 0)


def sx_gensym():                                # 61: (define (sx-gensym) ...)
    global _sx_gensym_counter
    _sx_gensym_counter += 1                     # (set! *sx-gensym-counter* (+ ... 1))
    return string_to_symbol("__t" + str(_sx_gensym_counter))
    # (string->symbol (string-append "__t" (number->string ...)))


# ── quasisyntax ───────────────────────────────────────────────────────────

def qs_unquote(x):                              # 62: (define (qs-unquote? x) ...)
    return isinstance(x, Cell) and x.car is SYM_UNSYNTAX


def qs_unsplice(x):                             # 63: (define (qs-unsplice? x) ...)
    return isinstance(x, Cell) and x.car is SYM_UNSYNTAX_SPLICING


def qs_walk_list(cur):                          # 64: (define (qs-walk-list cur) ...)
    if cur is NIL:                              # (null? cur)
        return NIL
    if not isinstance(cur, Cell):               # (not (pair? cur))
        return qs_expand(cur)                   # (qs-expand cur)
    if qs_unsplice(cur.car):                    # (qs-unsplice? (car cur))
        # 还原 let: v = (eval (cadr (car cur)) (sx-expand-env))
        v = eval_qs(cur.car.cdr.car, sx_expand_env())
        return qq_append_lists(qq_reverse(v), qs_walk_list(cur.cdr))
    if qs_unquote(cur.car):                     # (qs-unquote? (car cur))
        return Cell(eval_qs(cur.car.cdr.car, sx_expand_env()),
                    qs_walk_list(cur.cdr))
    return Cell(qs_expand(cur.car), qs_walk_list(cur.cdr))


def qs_expand(x):                               # 65: (define (qs-expand x) ...)
    if isinstance(x, Sym):                      # (symbol? x)
        return sx_expand_sym(x, sx_get_bindings(),
                             _sx_mutated_vars, sx_def_env())
        # (sx-expand-sym x (sx-get-bindings)) — mutated/def-env 显式传参
    if not isinstance(x, Cell):                 # (not (pair? x))
        return x
    if qs_unquote(x):                           # (qs-unquote? x)
        return eval_qs(x.cdr.car, sx_expand_env()) # (eval (cadr x) (sx-expand-env))
    if qs_unsplice(x):                          # (qs-unsplice? x)
        return eval(x.cdr.car, sx_expand_env())
    if isinstance(x.car, Sym) and x.car is SYM_QUASISYNTAX:  # (eq? (car x) 'quasisyntax)
        return x
    return qs_walk_list(x)                      # (qs-walk-list x)


def sx_gen_temps(lst):                          # 66: (define (sx-gen-temps lst) ...)
    n = _length(lst)                            # (length lst)
    acc = NIL
    for _ in range(n):                          # (if (= n 0) acc (loop (- n 1) (cons (sx-gensym) acc)))
        acc = Cell(sx_gensym(), acc)
    return acc


# ── syntax-case / with-syntax / let-syntax ────────────────────────────────

def sx_syntax_case(expr, lits, clauses):        # 67: (define (sx-syntax-case expr lits clauses) ...)
    datum = expr                                # (datum expr)
    lits_list = _iter_cells(lits)               # lits 是 Cell 链 (Scheme 数据) → list (sx_match 需要)
    while clauses is not NIL:                   # (if (null? clauses) error ...)
        cl = clauses.car
        rest_cl = cl.cdr                        # (cdr cl)
        pat = cl.car                            # (car cl)
        has_fender = (isinstance(rest_cl, Cell)
                      and isinstance(rest_cl.cdr, Cell))  # (if (pair? rest-cl) (pair? (cdr rest-cl)) #f)
        fender = rest_cl.car if has_fender else None     # (if has-fender (car rest-cl) #f)
        tmpl = rest_cl.cdr.car if has_fender else rest_cl.car  # (if has-fender (cadr rest-cl) (car rest-cl))
        b = sx_match(pat, datum, lits_list)     # (sx-match pat datum lits)
        # (if b (if (or (not has-fender) (sx-check-fender fender b)) (sx-eval-tmpl tmpl b) 递归)
        if b is not None and (not has_fender or sx_check_fender(fender, b)):
            return sx_eval_tmpl(tmpl, b)
        clauses = clauses.cdr                   # (sx-syntax-case datum lits (cdr clauses))
    raise SchemeError("syntax-case: no match")


def sx_check_fender(fender, b):                 # 68: (define (sx-check-fender fender b) ...)
    # (not (eq? (eval fender (sx-expand-env)) #f))
    return sx_with_bindings(b,
                            lambda: not (eval(fender, sx_expand_env()) is FALSE))


def sx_eval_tmpl(tmpl, b):                      # 69: (define (sx-eval-tmpl tmpl b) ...)
    # 还原 let: r = (eval tmpl (sx-expand-env))
    r = sx_with_bindings(b, lambda: eval(tmpl, sx_expand_env()))
    if isinstance(r, Sym):                      # (symbol? r)
        return Cell(SYM_QUOTE, Cell(r, NIL))    # (list 'quote r)
    return r


def sx_with_syntax(pairs, body):                # 70: (define (sx-with-syntax pairs body) ...)
    acc = []
    ps = pairs
    while ps is not NIL:                        # (if (null? ps) ...) → 迭代
        p = ps.car
        pat = p.car                             # (caar ps)
        val = p.cdr.car                          # (cadar ps)
        b = sx_match(pat, val, [])              # (sx-match pat val '())
        if b is None:
            raise SchemeError("with-syntax: no match")
        acc = sx_merge_bindings(acc, b)         # (loop (cdr ps) (sx-merge-bindings acc b))
        ps = ps.cdr
    # (sx-with-bindings (sx-merge-bindings acc (sx-get-bindings))
    #                   (lambda () (sx-eval-body body (sx-expand-env))))
    return sx_with_bindings(sx_merge_bindings(acc, sx_get_bindings()),
                            lambda: sx_eval_body(body, sx_expand_env()))


def sx_eval_body(body, env):                    # 71: (define (sx-eval-body body env) ...)
    last = VOID                                 # (last (void))
    cur = body
    while isinstance(cur, Cell):                # (for-each (lambda (form) (set! last (eval form env))) body)
        last = eval(cur.car, env)
        cur = cur.cdr
    return last


def sx_let_syntax(bindings, body):              # 72: (define (sx-let-syntax bindings body) ...)
    # (append (map sx-make-macro-binding bindings) body)
    inner = [sx_make_macro_binding(b) for b in _iter_cells(bindings)] + _iter_cells(body)
    return Cell(Cell(SYM_LAMBDA, Cell(NIL, _to_cell(inner))), NIL)
    # (list (cons 'lambda (cons '() ...)))


def sx_make_macro_binding(binding):             # 73: (define (sx-make-macro-binding binding) ...)
    name = binding.car                          # (car binding)
    trans = binding.cdr.car                     # (cadr binding)
    if isinstance(trans, Cell) and trans.car is SYM_SYNTAX_RULES:
        # (if (pair? trans) (eq? (car trans) 'syntax-rules) #f)
        lits = trans.cdr.car if isinstance(trans.cdr, Cell) else NIL  # (if (pair? (cdr trans)) (cadr trans) '())
        rules = trans.cdr.cdr                   # (cddr trans)
        # (list 'define-macro (cons name 'args)
        #       (list 'sx-dispatch 'args (list 'quote lits) (list 'quote rules)))
        return Cell(SYM_DEFINE_MACRO, Cell(Cell(name, SYM_ARGS), Cell(
            Cell(SYM_SX_DISPATCH, Cell(SYM_ARGS, Cell(
                Cell(SYM_QUOTE, Cell(lits, NIL)),
                Cell(Cell(SYM_QUOTE, Cell(rules, NIL)), NIL)))),
            NIL)))
    # (list 'define-macro (cons name 'args)
    #       (list (cons 'lambda (cdr trans)) (list 'cons (list 'quote name) 'args)))
    return Cell(SYM_DEFINE_MACRO, Cell(Cell(name, SYM_ARGS), Cell(
        Cell(Cell(SYM_LAMBDA, trans.cdr),
             Cell(Cell(SYM_CONS, Cell(Cell(SYM_QUOTE, Cell(name, NIL)),
                                      Cell(SYM_ARGS, NIL))), NIL)),
        NIL)))


# ── 公开入口 (REPL ,expand) ────────────────────────────────────────────────

def expand(expr, env):
    """完整展开一个表达式: 反复展开直到不动点 (my-macro-expand 已递归到不动点)。"""
    return my_macro_expand(expr, env)
