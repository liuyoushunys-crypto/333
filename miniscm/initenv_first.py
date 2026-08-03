# initenv_first.py — 宏系统自举核心 builtin 注册
# 注册 scm/my-definemacro2.scm + scm/boot-min2.scm 宏系统自举依赖的最小 builtin 集。
# 只依赖 primitives_first.py 和 mtypes.py，不依赖 primitives.py/initenv.py 的其他部分。
import sys
from mtypes import (
    Sym, Cell, SchemeString, SchemeVector,
    NIL, VOID, TRUE, FALSE,
    _cell_len, _cells, _lst, builtin
)
from primitives_first import (
    cons, car, cdr, caar, cadr, cdar, cddr, lst, add, sub, eqv, equal,
    memq, assq, map_, list_ref, append, is_list,
    eq_num, lt, gt, le, ge,
    for_each_fn, error, dsp, filter_,
    _eval_bridge, _sx_def_env, _sx_expand_env, _sx_defined, _sx_defmacro, _sx_expand_call,
)


def initenv_first():
    # ── 数值/逻辑 ──
    builtin('+', add)
    builtin('-', sub)
    builtin('=', eq_num)
    builtin('<', lt)
    builtin('>', gt)
    builtin('<=', le)
    builtin('>=', ge)
    builtin('number->string', str)

    # ── 谓词 ──
    builtin('eq?', lambda a, b: TRUE if a is b else FALSE)
    builtin('eqv?', eqv)
    builtin('equal?', equal)
    builtin('symbol?', lambda x: TRUE if isinstance(x, Sym) else FALSE)
    builtin('procedure?', lambda x: TRUE if callable(x) or isinstance(x, tuple) else FALSE)
    builtin('not', lambda x: TRUE if x is FALSE else FALSE)

    # ── 对与列表 ──
    builtin('pair?', lambda x: TRUE if isinstance(x, Cell) else FALSE)
    builtin('null?', lambda x: TRUE if x is NIL else FALSE)
    builtin('cons', cons)
    builtin('car', car)
    builtin('cdr', cdr)
    builtin('caar', caar)
    builtin('cadr', cadr)
    builtin('cdar', cdar)
    builtin('cddr', cddr)
    builtin('list', lst)
    builtin('list?', is_list)
    builtin('length', lambda lst: _cell_len(lst) if isinstance(lst, Cell) else 0)
    builtin('append', append)
    builtin('list-ref', list_ref)
    builtin('map', map_)
    builtin('memq', memq)
    builtin('assq', assq)
    builtin('for-each', for_each_fn)
    builtin('filter', filter_)
    builtin('vector?', lambda x: TRUE if isinstance(x, (list, SchemeVector)) else FALSE)
    builtin('vector->list', lambda v: _lst(list(v.data if hasattr(v, 'data') else v)))
    builtin('list->vector', lambda lst: SchemeVector(_cells(lst)))

    # ── 字符串 ──
    builtin('string->symbol', lambda s: Sym(str(s)) if isinstance(s, (str, SchemeString)) else s)
    builtin('string-append', lambda *a: SchemeString(''.join(str(x) for x in a)))
    builtin('display', dsp)
    builtin('newline', lambda: (sys.stdout.write("\n"), VOID)[-1])

    # ── 其他 ──
    builtin('void', lambda *a: VOID)
    builtin('error', error)

    # ── 桥接（实现在 primitives_first.py, 惰性导入 miniscm 避免循环依赖）──
    builtin('eval', _eval_bridge)
    builtin('sx-def-env', _sx_def_env)
    builtin('sx-expand-env', _sx_expand_env)
    builtin('sx-defined?', _sx_defined)
    builtin('sx-defmacro', _sx_defmacro)
    builtin('sx-expand-call', _sx_expand_call)
