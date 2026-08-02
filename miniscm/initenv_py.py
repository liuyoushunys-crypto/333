# initenv.py — builtin registration extracted from primitives.py
from mtypes import (
    Cell, SchemeString, NIL, VOID, TRUE, FALSE,
    _plist, _lst, builtin
)
from primitives_py import py_curry, py_from_import, py_import_mod, pyslice
from primitives import *



# ── Python 接口支持 ──
def initenv_py():
    builtin('py-import', py_import_mod)
    builtin('py-from', py_from_import)

    # (py-get obj "attr") → 属性访问
    builtin('py-get', lambda obj, attr: getattr(obj, str(attr)))
    # (py-set! obj "attr" val) → 属性赋值
    builtin('py-set!', lambda obj, attr, val: setattr(obj, str(attr), val) or VOID)
    # (py-call obj "method" args...) → 方法调用
    builtin('py-call', lambda obj, method, *args: getattr(obj, str(method))(*args))
    # (py-slice obj "args") → 切片访问 obj["args"]
    builtin('py-getitem', lambda obj, key: obj[key])
    builtin('py-setitem', lambda obj, key, val: obj.__setitem__(key, val) or VOID)
    # (py-new Class args...) — 创建实例
    builtin('py-new', lambda cls, *args: cls(*args))
    # (py-dir obj) — 列出属性
    builtin('py-dir', lambda obj: _lst([SchemeString(n) for n in dir(obj) if not n.startswith('_')]))
    # (py-exec "code") — 执行 Python 语句
    builtin('py-exec', lambda code: exec(str(code)) or VOID)
    # (py-eval "expr") — 求值 Python 表达式，返回结果
    builtin('py-eval', lambda expr: eval(str(expr)))
    # (py-len obj) — Python 长度
    builtin('py-len', lambda obj: len(obj))
    # (py-str obj) — Python 字符串表示
    builtin('py-str', lambda obj: SchemeString(str(obj)))
    # (py-repr obj) — Python 可读表示
    builtin('py-repr', lambda obj: str(repr(obj)))
    # (py-type obj) — Python 类型名
    builtin('py-type', lambda obj: str(type(obj).__name__))
    # (py-hasattr? obj "attr") — 属性存在检测
    builtin('py-hasattr?', lambda obj, attr: TRUE if hasattr(obj, str(attr)) else FALSE)
    # (py->list obj) — Python 可迭代对象转 Scheme 列表
    builtin('py->list', lambda obj: _lst([x for x in obj]))
    # (py->str obj) — Python 对象转 Scheme 字符串
    builtin('py->str', lambda obj: SchemeString(str(obj)))
    # (list->py obj) — Scheme 列表转 Python list
    builtin('list->py', lambda lst: [x for x in (_plist(lst) if isinstance(lst, Cell) else [lst]) if x is not NIL])
    # (py: spec) → slice(start, end, step) — 解析 Python 风格切片元组
    # (py:partial fn . args) — Python 部分应用
    builtin('py:partial', lambda fn, *args: (lambda *rest: fn(*(list(args) + list(rest)))))
    # (py:rpartial fn . args) — Python 右部分应用
    builtin('py:rpartial', lambda fn, *args: (lambda *rest: fn(*(list(rest) + list(args)))))
    # (py:curry fn n) — Python 柯里化, n 为参数个数
    builtin('py:curry', lambda fn, n: py_curry(fn, int(n)))
    # (pyslice obj spec) → obj[spec] — 切片应用
    builtin('pyslice', pyslice)
