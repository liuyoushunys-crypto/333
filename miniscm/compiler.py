# compiler.py
# ═══════════════════════════════════════════════════════════════════════
# miniscm JIT 编译器 — 20轮深度复盘优化版
# 核心流水线：
#   Cell/S-expr → to_ast → AST → fold_constants → compile_expr/stmt → Python AST → compile() → exec()
#
# 优化清单：
#   ✓ 修复 _compile_VarAst 内存泄漏 (复盘1)
#   ✓ 分发器从字符串+getattr改为直接类型链 (复盘2)
#   ✓ 修正 _INLINE_OPS 移除 '/' 和 '==' (复盘3)
#   ✓ _IS_COMPILING 改用 contextmanager + finally (复盘4)
#   ✓ LambdaProc.__call__ 延迟导入缓存 (复盘5)
#   ✓ 闭包检测三函数合并为两个 (复盘6)
#   ✓ 常量折叠添加类型安全 (复盘7)
#   ✓ serialize_val 用 type() is 精确匹配 (复盘8)
#   ✓ 修复 _stmt_IfAst 的 Scheme 真值语义 (复盘9) ← 致命 BUG
#   ✓ LambdaProc fallback 解包 TailCall (复盘10)
#   ✓ compile_lambda_proc 异常在 MSCM_JIT_DEBUG 下打印 traceback (复盘11)
#   ✓ _compile_DefineAst 直接用 ast.Constant (复盘13)
#   ✓ 临时变量用 __mscm_t_ 前缀 (复盘14)
#   ✓ 提取 _make_jit_globals 公共函数 (复盘15)
#   ✓ _compile_BeginAst 单表达式快速路径 (复盘16)
#   ✓ _INLINE_ARITH/_INLINE_CMP 提升为模块级常量 (复盘17)
#   ✓ AST 节点全部添加 __slots__ (复盘18)
#   ✓ 清理三重 os 导入 (复盘19)
#   ✓ CompiledLambda 预计算 _n_regular (复盘20)
# ═══════════════════════════════════════════════════════════════════════

import ast
import os
import sys
import hashlib
import json
from contextlib import contextmanager
from fractions import Fraction

CACHE_VERSION = 5  # 缓存格式版本，更改缓存格式时递增

PYB_MODE = 'scm'

from mtypes import (
    Sym, Cell, Env, NIL, VOID, FALSE, TRUE, _bind_params, _sn, TailCall, be,
    SchemeVector, SchemeChar, SchemeString, SchemeBytevector, _cells, _cell_len,
    SYM_QUOTE, SYM_IF, SYM_LAMBDA, SYM_BEGIN, SYM_DEFINE, SYM_SETBANG, _lst,
    SyntaxObject, _pr
)
from primitives import vec_set_elem






# ═══════════════════════════════════════════════════════════════
# 全局开关及 JIT 配置
# ═══════════════════════════════════════════════════════════════

USE_JIT = True

# pyb=False 时禁用 JIT：Scheme 实现的函数在 JIT 编译下
# __mscm_make_tail_call__ 会错误估值已估值参数  
# box 是 initenv_ext 注册的函数，pyb=True 时存在
_JIT_ALLOWED = lambda: USE_JIT
# 与 minischeme Compiler.SkipJitNames 一致 — 跳过 JIT 编译的辅助/宏引擎函数
SKIP_JIT_NAMES = frozenset({
    "flip", "complement", "const", "identity",
    "check", "test", "t-eq"
})

# 不可变原语集合 — 用 frozenset 加速成员检测
_IMMUTABLE_PRIMITIVES = frozenset({
    'car', 'cdr', 'cons', 'null?', 'pair?', 'list?',
    'map', 'apply', 'list', 'append', 'reverse', 'length',
    'boolean?', 'procedure?', 'symbol?', 'number?', 'string?', 'vector?',
    'char?', 'bytevector?', 'eof-object?', 'assq', 'assoc', 'memq', 'member',
    'caar', 'cadr', 'cdar', 'cddr', 'caaar', 'caadr', 'cadar', 'caddr',
    'cdaar', 'cdadr', 'cddar', 'cdddr', 'string-append', 'string-length',
    'vector-ref', 'vector-set!', 'vector-length',
    'string-ref', 'string-set!', 'display', 'write', 'newline',
    'eq?', 'eqv?', 'equal?', 'not',
    'zero?', 'positive?', 'negative?', 'even?', 'odd?',
    'inexact?', 'exact?', 'integer?', 'real?', 'complex?', 'rational?',
    'abs', 'max', 'min', 'quotient', 'remainder', 'modulo',
    'gcd', 'lcm', 'expt', 'sqrt',
    'floor', 'ceiling', 'truncate', 'round',
    'sin', 'cos', 'tan', 'log', 'exp', 'asin', 'acos', 'atan',
    'numerator', 'denominator', 'imag-part', 'real-part',
    'make-rectangular', 'make-polar', 'magnitude', 'angle',
    'exact-integer-sqrt', 'exact-integer?',
    'list-tail', 'list-ref', 'list-set!', 'list-copy',
    'memv', 'assv', 'member', 'assoc',
    'string?', 'string=?', 'string<?', 'string>?', 'string<=?', 'string>=?',
    'string-append', 'string-length', 'string-ref', 'string-set!',
    'substring', 'string-copy', 'string->number', 'number->string',
    'symbol->string', 'string->symbol',
    'char->integer', 'integer->char', 'char?',
    'call-with-current-continuation', 'call/cc',
    'dynamic-wind', 'values', 'call-with-values',
    'error', 'raise', 'assert',
    'current-error-port', 'current-output-port', 'current-input-port',
    'open-input-string', 'open-output-string', 'get-output-string',
    'eof-object?', 'eof-object',
    'load', 'eval', 'apply',
})

# ── 复盘3 修正：移除 '/'（不内联，Scheme 需返回 Fraction）和 '=='（非标准 Scheme 操作符） ──
_INLINE_OPS = frozenset({
    '+', '-', '*', '<', '>', '<=', '>=', '=',
    'eq?', 'not', 'car', 'cdr', 'null?', 'pair?',
    'zero?', 'positive?', 'negative?', 'even?', 'odd?',
    'string-length', 'vector-length',
})

# ── 复盘17：提升为模块级常量，避免每次内联时创建字典 ──
_INLINE_ARITH = {'+': ast.Add, '-': ast.Sub, '*': ast.Mult}
_INLINE_CMP = {
    '<': ast.Lt, '>': ast.Gt, '<=': ast.LtE, '>=': ast.GtE,
    '=': ast.Eq, 'eq?': ast.Is
}

def should_jit(lambda_proc):
    # 与 minischeme Compiler.ShouldJit 一致
    name = lambda_proc.name
    if name is None:
        return False
    if name in SKIP_JIT_NAMES:
        return False
    return True

# ── 复盘4：用 contextmanager 替代手动 try/finally，保证 _IS_COMPILING 安全重置 ──
_IS_COMPILING = False

@contextmanager
def _compiling_guard():
    global _IS_COMPILING
    old = _IS_COMPILING
    _IS_COMPILING = True
    try:
        yield
    finally:
        _IS_COMPILING = old

# JIT 日志
if os.environ.get("MSCM_JIT_DEBUG"):
    def _JIT_LOG(*a):
        sys.stderr.write(" ".join(str(x) for x in a) + "\n")
        sys.stderr.flush()
else:
    def _JIT_LOG(*a):
        pass

CACHE_DIR = "./.mscm_cache"
os.makedirs(CACHE_DIR, exist_ok=True)

# ═══════════════════════════════════════════════════════════════
# Scheme AST Nodes — 复盘18：全部添加 __slots__
# ═══════════════════════════════════════════════════════════════

class ASTNode:
    __slots__ = ()

class LiteralAst(ASTNode):
    __slots__ = ('val',)
    def __init__(self, val):
        self.val = val

class VarAst(ASTNode):
    __slots__ = ('name',)
    def __init__(self, name):
        self.name = name

class IfAst(ASTNode):
    __slots__ = ('test', 'then', 'else_')
    def __init__(self, test, then, else_):
        self.test = test
        self.then = then
        self.else_ = else_

class DefineAst(ASTNode):
    __slots__ = ('name', 'val')
    def __init__(self, name, val):
        self.name = name
        self.val = val

class SetBangAst(ASTNode):
    __slots__ = ('name', 'val')
    def __init__(self, name, val):
        self.name = name
        self.val = val

class LambdaAst(ASTNode):
    __slots__ = ('params', 'body', 'is_simple', 'original_cell')
    def __init__(self, params, body, is_simple=True, original_cell=None):
        self.params = params
        self.body = body
        self.is_simple = is_simple
        self.original_cell = original_cell

class BeginAst(ASTNode):
    __slots__ = ('exprs',)
    def __init__(self, exprs):
        self.exprs = exprs

class AppAst(ASTNode):
    __slots__ = ('proc', 'args')
    def __init__(self, proc, args):
        self.proc = proc
        self.args = args

def clean_param_name(p):
    return p[5:] if p.startswith('rest:') else p

# ═══════════════════════════════════════════════════════════════
# 辅助函数 — 提取 to_ast 中的重复模式
# ═══════════════════════════════════════════════════════════════

def parse_param_list(cell):
    """解析 lambda 参数列表，返回 (params_list, has_rest)"""
    params = []
    cur = cell
    has_rest = False
    while isinstance(cur, Cell):
        params.append(_sn(cur.car))
        cur = cur.cdr
    if cur is not NIL:
        params.append('rest:' + _sn(cur))
        has_rest = True
    return params, has_rest

def parse_body(cell):
    """将 Cell 链的每个元素转为 AST"""
    exprs = []
    cur = cell
    while isinstance(cur, Cell):
        exprs.append(to_ast(cur.car))
        cur = cur.cdr
    return exprs

def cell_to_list(cell):
    """将 Cell 链转为 Python list"""
    result = []
    cur = cell
    while isinstance(cur, Cell):
        result.append(cur.car)
        cur = cur.cdr
    return result

# ═══════════════════════════════════════════════════════════════
# S-Expression to AST Parser
# ═══════════════════════════════════════════════════════════════

def to_ast(expr):

    if isinstance(expr, Sym):
        if expr is TRUE or expr is FALSE:
            return LiteralAst(expr)
        return VarAst(expr.name)

    if isinstance(expr, Cell):
        op = expr.car
        args = expr.cdr

        if op is SYM_QUOTE:
            return LiteralAst(args.car if isinstance(args, Cell) else NIL)

        if op is SYM_IF:
            test = to_ast(args.car)
            then_expr = to_ast(args.cdr.car)
            else_expr = LiteralAst(VOID)
            rest = args.cdr.cdr
            if rest is not NIL and isinstance(rest, Cell):
                else_expr = to_ast(rest.car)
            return IfAst(test, then_expr, else_expr)

        if op is SYM_LAMBDA:
            params, has_rest = parse_param_list(args.car)
            body_exprs = parse_body(args.cdr)
            return LambdaAst(params, body_exprs, not has_rest, original_cell=args.cdr)

        if op is SYM_BEGIN:
            return BeginAst(parse_body(args))

        if op is SYM_DEFINE:
            pat = args.car
            if isinstance(pat, Cell):
                name = _sn(pat.car)
                params, has_rest = parse_param_list(pat.cdr)
                body_exprs = parse_body(args.cdr)
                return DefineAst(name, LambdaAst(params, body_exprs, not has_rest, original_cell=args.cdr))
            else:
                val_expr = args.cdr.car if isinstance(args.cdr, Cell) else NIL
                return DefineAst(_sn(pat), to_ast(val_expr))

        if op is SYM_SETBANG:
            return SetBangAst(_sn(args.car), to_ast(args.cdr.car))

        proc_ast = to_ast(op)
        args_ast = [to_ast(a) for a in cell_to_list(args)]
        return AppAst(proc_ast, args_ast)

    return LiteralAst(expr)

# ═══════════════════════════════════════════════════════════════
# 常量折叠优化 — 复盘7：添加类型安全检查
# ═══════════════════════════════════════════════════════════════

def fold_constants(node):
    if isinstance(node, IfAst):
        test = fold_constants(node.test)
        then = fold_constants(node.then)
        els = fold_constants(node.else_)
        if isinstance(test, LiteralAst):
            if test.val is not FALSE:                
                return then
            else:
                return els
        return IfAst(test, then, els)

    if isinstance(node, BeginAst):
        return BeginAst([fold_constants(e) for e in node.exprs])

    if isinstance(node, AppAst):
        proc = fold_constants(node.proc)
        args = [fold_constants(a) for a in node.args]

        # 单参原语折叠
        if isinstance(proc, VarAst) and len(args) == 1:
            op_name = proc.name
            arg = args[0]
            if isinstance(arg, LiteralAst):
                av = arg.val
                if op_name == 'not':
                    return LiteralAst(TRUE if av is FALSE else FALSE)
                if op_name == 'null?':
                    return LiteralAst(TRUE if av is NIL else FALSE)
                if op_name == 'pair?':
                    return LiteralAst(TRUE if isinstance(av, Cell) else FALSE)
                if op_name == 'car' and isinstance(av, Cell):
                    return LiteralAst(av.car)
                if op_name == 'cdr' and isinstance(av, Cell):
                    return LiteralAst(av.cdr)

        # 双参算术/比较折叠 — 复盘7：限定数值类型 + try/except 双重保护
        if isinstance(proc, VarAst) and len(args) == 2:
            op_name = proc.name
            left, right = args[0], args[1]
            if isinstance(left, LiteralAst) and isinstance(right, LiteralAst):
                lv, rv = left.val, right.val
                if isinstance(lv, (int, float, Fraction)) and isinstance(rv, (int, float, Fraction)):
                    try:
                        if op_name == '+': return LiteralAst(lv + rv)
                        if op_name == '-': return LiteralAst(lv - rv)
                        if op_name == '*': return LiteralAst(lv * rv)
                        if op_name == '/': return LiteralAst(Fraction(lv, rv))
                        if op_name == '<': return LiteralAst(TRUE if lv < rv else FALSE)
                        if op_name == '>': return LiteralAst(TRUE if lv > rv else FALSE)
                        if op_name == '<=': return LiteralAst(TRUE if lv <= rv else FALSE)
                        if op_name == '>=': return LiteralAst(TRUE if lv >= rv else FALSE)
                        if op_name == '=': return LiteralAst(TRUE if lv == rv else FALSE)
                    except (TypeError, ValueError, ZeroDivisionError):
                        pass

        return AppAst(proc, args)

    if isinstance(node, LambdaAst):
        return LambdaAst(node.params, [fold_constants(e) for e in node.body], node.is_simple, original_cell=node.original_cell)
    if isinstance(node, DefineAst):
        return DefineAst(node.name, fold_constants(node.val))
    if isinstance(node, SetBangAst):
        return SetBangAst(node.name, fold_constants(node.val))
    return node

# ═══════════════════════════════════════════════════════════════
# set! 检测 — 用于自递归 TCO 安全性检查
# ═══════════════════════════════════════════════════════════════

def has_mutation(ast_node, var_name):
    if isinstance(ast_node, SetBangAst):
        return ast_node.name == var_name
    if isinstance(ast_node, IfAst):
        return has_mutation(ast_node.test, var_name) or \
               has_mutation(ast_node.then, var_name) or \
               has_mutation(ast_node.else_, var_name)
    if isinstance(ast_node, AppAst):
        return has_mutation(ast_node.proc, var_name) or \
               any(has_mutation(a, var_name) for a in ast_node.args)
    if isinstance(ast_node, BeginAst):
        return any(has_mutation(e, var_name) for e in ast_node.exprs)
    if isinstance(ast_node, LambdaAst):
        if var_name in ast_node.params:
            return False
        return any(has_mutation(e, var_name) for e in ast_node.body)
    return False

# ═══════════════════════════════════════════════════════════════
# 闭包检测 — 复盘6：三函数合并为两个
# ═══════════════════════════════════════════════════════════════

def has_nested_closure(ast_node, local_vars):
    """
    检查 AST 中是否存在嵌套闭包（内层 lambda 引用外层变量）。
    """
    if isinstance(ast_node, (VarAst, LiteralAst)):
        return False
    if isinstance(ast_node, LambdaAst):
        inner_params = {clean_param_name(p) for p in ast_node.params}
        # 复盘：内层 lambda 的形参对外层而言是定义者（definers），
        # 但它们同时也是闭包跟踪中的"外层变量"。
        # 递归检查 body 时，将本层形参加入 local_vars，
        # 这样更深层嵌套的 lambda 才能正确检测到对本层形参的闭包引用。
        extended_vars = local_vars | inner_params
        for body_node in ast_node.body:
            if refers_outer_var(body_node, local_vars, inner_params):
                return True
            if has_nested_closure(body_node, extended_vars):
                return True
        return False
    if isinstance(ast_node, DefineAst):
        return has_nested_closure(ast_node.val, local_vars)
    if isinstance(ast_node, SetBangAst):
        # 复盘：set! 的值表达式可能包含嵌套 lambda 闭包
        return has_nested_closure(ast_node.val, local_vars)
    if isinstance(ast_node, IfAst):
        return has_nested_closure(ast_node.test, local_vars) or \
               has_nested_closure(ast_node.then, local_vars) or \
               has_nested_closure(ast_node.else_, local_vars)
    if isinstance(ast_node, AppAst):
        if has_nested_closure(ast_node.proc, local_vars):
            return True
        return any(has_nested_closure(a, local_vars) for a in ast_node.args)
    if isinstance(ast_node, BeginAst):
        return any(has_nested_closure(e, local_vars) for e in ast_node.exprs)
    return False

def refers_outer_var(node, outer_vars, inner_params):
    """
    检查 node 中是否有变量引用 outer_vars（且未被 inner_params 遮蔽）。
    """
    if isinstance(node, VarAst):
        return node.name in outer_vars and node.name not in inner_params
    if isinstance(node, LiteralAst):
        return False
    if isinstance(node, LambdaAst):
        nested_params = {clean_param_name(p) for p in node.params}
        combined = inner_params | nested_params
        return any(refers_outer_var(b, outer_vars, combined) for b in node.body)
    if isinstance(node, DefineAst):
        return refers_outer_var(node.val, outer_vars, inner_params)
    if isinstance(node, SetBangAst):
        return (node.name in outer_vars and node.name not in inner_params) or \
               refers_outer_var(node.val, outer_vars, inner_params)
    if isinstance(node, IfAst):
        return refers_outer_var(node.test, outer_vars, inner_params) or \
               refers_outer_var(node.then, outer_vars, inner_params) or \
               refers_outer_var(node.else_, outer_vars, inner_params)
    if isinstance(node, AppAst):
        if refers_outer_var(node.proc, outer_vars, inner_params):
            return True
        return any(refers_outer_var(a, outer_vars, inner_params) for a in node.args)
    if isinstance(node, BeginAst):
        return any(refers_outer_var(e, outer_vars, inner_params) for e in node.exprs)
    return False

def collect_outer_refs(node, outer_vars, inner_params):
    """
    收集 node 中引用的外层变量集合（未被 inner_params 遮蔽）。
    """
    if isinstance(node, VarAst):
        if node.name in outer_vars and node.name not in inner_params:
            return {node.name}
        return set()
    if isinstance(node, LiteralAst):
        return set()
    if isinstance(node, LambdaAst):
        nested_params = {clean_param_name(p) for p in node.params}
        combined = inner_params | nested_params
        acc = set()
        for b in node.body:
            acc |= collect_outer_refs(b, outer_vars, combined)
        return acc
    if isinstance(node, DefineAst):
        return collect_outer_refs(node.val, outer_vars, inner_params)
    if isinstance(node, SetBangAst):
        acc = collect_outer_refs(node.val, outer_vars, inner_params)
        if node.name in outer_vars and node.name not in inner_params:
            acc.add(node.name)
        return acc
    if isinstance(node, IfAst):
        return (collect_outer_refs(node.test, outer_vars, inner_params)
                | collect_outer_refs(node.then, outer_vars, inner_params)
                | collect_outer_refs(node.else_, outer_vars, inner_params))
    if isinstance(node, AppAst):
        acc = collect_outer_refs(node.proc, outer_vars, inner_params)
        for a in node.args:
            acc |= collect_outer_refs(a, outer_vars, inner_params)
        return acc
    if isinstance(node, BeginAst):
        acc = set()
        for e in node.exprs:
            acc |= collect_outer_refs(e, outer_vars, inner_params)
        return acc
    return set()

# ═══════════════════════════════════════════════════════════════
# CompiledLambda — 复盘20：预计算 _n_regular
# ═══════════════════════════════════════════════════════════════

class CompiledLambda:
    __slots__ = ('py_func', 'params', 'env', 'is_simple', '_n_regular')
    def __init__(self, py_func, params, env, is_simple):
        self.py_func = py_func
        self.params = params
        self.env = env
        self.is_simple = is_simple
        self._n_regular = len(params) - 1 if not is_simple else len(params)

    def __call__(self, *args):
        if self.is_simple:
            return self.py_func(self.env, *args)
        from mtypes import _lst
        n = self._n_regular
        regular_args = list(args[:n])
        rest_args = _lst(args[n:])
        return self.py_func(self.env, *(regular_args + [rest_args]))

# ── 复盘18：提取统一调用路径供 __mscm_invoke__ 和 LambdaProc 复用 ──
def _invoke_compiled(cv, args_val):
    """CompiledLambda 统一调用路径"""
    if cv.is_simple:
        return cv.py_func(cv.env, *args_val)
    from mtypes import _lst
    n = cv._n_regular
    regular_args = list(args_val[:n])
    rest_args = _lst(args_val[n:])
    return cv.py_func(cv.env, *(regular_args + [rest_args]))

# ═══════════════════════════════════════════════════════════════
# 运行时支撑函数
# ═══════════════════════════════════════════════════════════════

# Unwrap a TailCall produced by JIT MakeTailCall: (proc 'v1 'v2 ...).
# Applies proc to the (already-evaluated, quoted) args directly, avoiding
# re-entry into the full interpreter. (C# JitRuntime.EvalTailCall 等价)
def __mscm_eval_tail_call__(tc):
    expr = tc.expr
    if not isinstance(expr, Cell):
        from miniscm import _eval as _eval_fn
        return _eval_fn(expr, tc.env)
    proc = expr.car
    args = []
    cur = expr.cdr
    while isinstance(cur, Cell):
        arg = cur.car
        # MakeTailCall wraps each arg in (quote v)
        if isinstance(arg, Cell) and arg.car is SYM_QUOTE:
            if isinstance(arg.cdr, Cell):
                arg = arg.cdr.car
        args.append(arg)
        cur = cur.cdr
    r = __mscm_invoke__(proc, args, tc.env)
    while isinstance(r, TailCall):
        r = __mscm_eval_tail_call__(r)
    return r

def __mscm_invoke__(proc_val, args_val, env):
    global _IS_COMPILING
    if isinstance(proc_val, LambdaProc):
        if proc_val.compiled_version is not None:
            r = _invoke_compiled(proc_val.compiled_version, args_val)
            while isinstance(r, TailCall):
                r = __mscm_eval_tail_call__(r)
            return r
        old_flag = _IS_COMPILING
        _IS_COMPILING = False
        try:
            r = proc_val(*args_val)
            while isinstance(r, TailCall):
                r = __mscm_eval_tail_call__(r)
            return r
        finally:
            _IS_COMPILING = old_flag
    if isinstance(proc_val, CompiledLambda):
        r = _invoke_compiled(proc_val, args_val)
        while isinstance(r, TailCall):
            r = __mscm_eval_tail_call__(r)
        return r
    if callable(proc_val):
        r = proc_val(*args_val)
        while isinstance(r, TailCall):
            r = __mscm_eval_tail_call__(r)
        return r
    if isinstance(proc_val, tuple) and proc_val[0] == 'lambda':
        from miniscm import eval_seq
        _, params, body, penv, is_simple = proc_val
        nenv = Env(penv)
        _bind_params(params, args_val, nenv)
        r = eval_seq(body, nenv)
        while isinstance(r, TailCall):
            from miniscm import _eval as _eval_fn
            r = _eval_fn(r.expr, r.env)
        return r
    raise TypeError(f"not callable: {proc_val}")

def __mscm_env_set_var__(env, name, val):
    cur = env
    while cur is not None:
        if name in cur.data:
            cur.data[name] = val
            return VOID
        cur = cur.parent
    env.define(name, val)
    return VOID

def __mscm_closure_set__(env, name, val):
    env.data[name] = val
    return env

def __mscm_make_tail_call__(proc, args_list, env):
    # 用 (quote v) 包裹已估值参数，避免 _eval_args_to_list 把 Cell 值当表达式求值
    arg_cells = NIL
    for a in reversed(args_list):
        arg_cells = Cell(Cell(SYM_QUOTE, Cell(a, NIL)), arg_cells)
    expr = Cell(proc, arg_cells)
    return TailCall(expr, env)

def __mscm_resolve_ic__(cache_cell, env, sym):
    val = cache_cell[0]
    if val is None:
        val = env.lookup(sym)
        cache_cell[0] = val
    return val

# ═══════════════════════════════════════════════════════════════
# 复盘15：提取 globals 构建为公共函数
# ═══════════════════════════════════════════════════════════════

def _make_jit_globals(constants):
    """构建 JIT 编译函数的全局环境字典 — 统一构建点"""
    from primitives import car, cdr, cons
    return {
        '__mscm_consts__': constants,
        'TRUE': TRUE, 'FALSE': FALSE, 'VOID': VOID,
        'Env': Env, 'Sym': Sym, 'Cell': Cell, 'NIL': NIL,
        'SchemeVector': SchemeVector, 'SchemeChar': SchemeChar,
        'SchemeString': SchemeString, 'SchemeBytevector': SchemeBytevector,
        'Fraction': Fraction, 'cells': _cells, '_cell_len': _cell_len,
        'car': car, 'cdr': cdr, 'cons': cons,
        '_vec_set_elem': vec_set_elem,
        'CompiledLambda': CompiledLambda, 'TailCall': TailCall, 'LambdaProc': LambdaProc,
        '__mscm_invoke__': __mscm_invoke__,
        '__mscm_env_set_var__': __mscm_env_set_var__,
        '__mscm_closure_set__': __mscm_closure_set__,
        '__mscm_make_tail_call__': __mscm_make_tail_call__,
        '__mscm_resolve_ic__': __mscm_resolve_ic__,
        '_lst': _lst,
    }

# ═══════════════════════════════════════════════════════════════
# AstExprCompiler — 核心编译器
# ═══════════════════════════════════════════════════════════════

class AstExprCompiler:
    def __init__(self, self_name=None, params=None, is_simple=True):
        self.constants = []
        self.self_name = self_name
        self.params = params or []
        self.is_simple = is_simple

    def register_constant(self, val):
        self.constants.append(val)
        return len(self.constants) - 1

    # ── 复盘1：消除 _compile_VarAst 中的死代码 ──
    def _compile_VarAst(self, node, lexical_vars):
        name = node.name
        if name in lexical_vars:
            return ast.Name(id=name, ctx=ast.Load())
        if name in _IMMUTABLE_PRIMITIVES:
            val = be.data.get(name)
            if val is not None:
                idx = self.register_constant(val)
                return ast.Subscript(
                    value=ast.Name(id='__mscm_consts__', ctx=ast.Load()),
                    slice=ast.Constant(value=idx),
                    ctx=ast.Load()
                )
            # 不可变原语但未注册 → env.lookup 兜底
        return ast.Call(
            func=ast.Attribute(
                value=ast.Name(id='env', ctx=ast.Load()),
                attr='lookup', ctx=ast.Load()
            ),
            args=[ast.Constant(value=name)],
            keywords=[]
        )

    def _compile_LiteralAst(self, node, lexical_vars):
        idx = self.register_constant(node.val)
        return ast.Subscript(
            value=ast.Name(id='__mscm_consts__', ctx=ast.Load()),
            slice=ast.Constant(value=idx),
            ctx=ast.Load()
        )

    def _compile_IfAst(self, node, lexical_vars):
        test_ast = self.compile_expr(node.test, lexical_vars)
        then_ast = self.compile_expr(node.then, lexical_vars)
        else_ast = self.compile_expr(node.else_, lexical_vars)
        # Scheme 真值语义：只有 #f 是假值
        cond = ast.Compare(
            left=test_ast, ops=[ast.IsNot()],
            comparators=[ast.Name(id='FALSE', ctx=ast.Load())]
        )
        return ast.IfExp(test=cond, body=then_ast, orelse=else_ast)

    # ── 复盘13：直接用 ast.Constant(value=name) 替代常量池 Sym ──
    def _compile_DefineAst(self, node, lexical_vars):
        val_ast = self.compile_expr(node.val, lexical_vars)
        return ast.Call(
            func=ast.Attribute(
                value=ast.Name(id='env', ctx=ast.Load()),
                attr='define', ctx=ast.Load()
            ),
            args=[
                ast.Constant(value=node.name),
                val_ast
            ],
            keywords=[]
        )

    def _compile_SetBangAst(self, node, lexical_vars):
        if node.name in lexical_vars:
            return ast.NamedExpr(
                target=ast.Name(id=node.name, ctx=ast.Store()),
                value=self.compile_expr(node.val, lexical_vars)
            )
        val_ast = self.compile_expr(node.val, lexical_vars)
        return ast.Call(
            func=ast.Name(id='__mscm_env_set_var__', ctx=ast.Load()),
            args=[
                ast.Name(id='env', ctx=ast.Load()),
                ast.Constant(value=node.name),
                val_ast
            ],
            keywords=[]
        )

    # ── 复盘16：单表达式快速路径 ──
    def _compile_BeginAst(self, node, lexical_vars):
        exprs = node.exprs
        if not exprs:
            return ast.Name(id='VOID', ctx=ast.Load())
        if len(exprs) == 1:
            return self.compile_expr(exprs[0], lexical_vars)
        elts = [self.compile_expr(e, lexical_vars) for e in exprs]
        return ast.Subscript(
            value=ast.List(elts=elts, ctx=ast.Load()),
            slice=ast.Constant(value=-1),
            ctx=ast.Load()
        )

    def _compile_LambdaAst(self, node, lexical_vars):
        # 闭包检测：内层 lambda 引用外层变量 → 降级为运行时 LambdaProc
        inner_params = {clean_param_name(p) for p in node.params}
        captured = collect_outer_refs(node, lexical_vars, inner_params)
        if node.original_cell is not None and captured:
            _JIT_LOG("CLOSURE_DOWNGRADE: params=", node.params, " captured=", sorted(captured))
            body_cell_idx = self.register_constant(node.original_cell)
            params_idx = self.register_constant(node.params)
            is_simple_idx = self.register_constant(node.is_simple)
            # 构建子环境并绑定捕获的外层变量
            child_env = ast.Call(
                func=ast.Name(id='Env', ctx=ast.Load()),
                args=[ast.Name(id='env', ctx=ast.Load())],
                keywords=[]
            )
            for name in sorted(captured):
                child_env = ast.Call(
                    func=ast.Attribute(
                        value=ast.Name(id='__mscm_closure_set__', ctx=ast.Load()),
                        attr='__call__', ctx=ast.Load()
                    ),
                    args=[child_env, ast.Constant(value=name), ast.Name(id=name, ctx=ast.Load())],
                    keywords=[]
                )
            return ast.Call(
                func=ast.Name(id='LambdaProc', ctx=ast.Load()),
                args=[
                    ast.Subscript(value=ast.Name(id='__mscm_consts__', ctx=ast.Load()),
                                  slice=ast.Constant(value=params_idx), ctx=ast.Load()),
                    ast.Subscript(value=ast.Name(id='__mscm_consts__', ctx=ast.Load()),
                                  slice=ast.Constant(value=body_cell_idx), ctx=ast.Load()),
                    child_env,
                    ast.Subscript(value=ast.Name(id='__mscm_consts__', ctx=ast.Load()),
                                  slice=ast.Constant(value=is_simple_idx), ctx=ast.Load()),
                    ast.Constant(value=None),
                ],
                keywords=[]
            )
        nested_compiler = AstExprCompiler()
        cleaned_params = [clean_param_name(p) for p in node.params]
        combined_vars = lexical_vars | set(cleaned_params)
        func_args = ast.arguments(
            posonlyargs=[],
            args=[ast.arg(arg='env')] + [ast.arg(arg=p) for p in cleaned_params],
            kwonlyargs=[], kw_defaults=[], defaults=[]
        )
        nested_ast_body = nested_compiler.compile_stmt_seq(
            node.body, combined_vars, is_tail=True
        )
        if not nested_ast_body:
            nested_ast_body = [ast.Return(value=ast.Name(id='VOID', ctx=ast.Load()))]
        func_def = ast.FunctionDef(
            name='nested_lambda', args=func_args,
            body=nested_ast_body, decorator_list=[]
        )
        mod = ast.Module(body=[func_def], type_ignores=[])
        ast.fix_missing_locations(mod)
        code = compile(mod, filename="<nested-lambda>", mode="exec")
        g = _make_jit_globals(nested_compiler.constants)
        exec(code, g)
        py_func = g['nested_lambda']
        py_func_idx = self.register_constant(py_func)
        params_idx = self.register_constant(node.params)
        is_simple_idx = self.register_constant(node.is_simple)
        return ast.Call(
            func=ast.Name(id='CompiledLambda', ctx=ast.Load()),
            args=[
                ast.Subscript(value=ast.Name(id='__mscm_consts__', ctx=ast.Load()),
                              slice=ast.Constant(value=py_func_idx), ctx=ast.Load()),
                ast.Subscript(value=ast.Name(id='__mscm_consts__', ctx=ast.Load()),
                              slice=ast.Constant(value=params_idx), ctx=ast.Load()),
                ast.Name(id='env', ctx=ast.Load()),
                ast.Subscript(value=ast.Name(id='__mscm_consts__', ctx=ast.Load()),
                              slice=ast.Constant(value=is_simple_idx), ctx=ast.Load())
            ],
            keywords=[]
        )

    # ── 复盘17：用模块级 _INLINE_ARITH/_INLINE_CMP 字典 ──
    def _compile_AppAst_inline(self, node, lexical_vars):
        """尝试内联 AppAst，返回 ast 节点或 None"""
        if not isinstance(node.proc, VarAst) or node.proc.name in lexical_vars:
            return None
        op_name = node.proc.name
        n_args = len(node.args)

        if n_args == 1:
            arg_ast = self.compile_expr(node.args[0], lexical_vars)
            if op_name == 'car':
                return ast.Attribute(value=arg_ast, attr='car', ctx=ast.Load())
            if op_name == 'cdr':
                return ast.Attribute(value=arg_ast, attr='cdr', ctx=ast.Load())
            if op_name == 'null?':
                return ast.IfExp(
                    test=ast.Compare(left=arg_ast, ops=[ast.Is()],
                                     comparators=[ast.Name(id='NIL', ctx=ast.Load())]),
                    body=ast.Name(id='TRUE', ctx=ast.Load()),
                    orelse=ast.Name(id='FALSE', ctx=ast.Load())
                )
            if op_name == 'pair?':
                return ast.IfExp(
                    test=ast.Compare(
                        left=ast.Attribute(value=arg_ast, attr='__class__', ctx=ast.Load()),
                        ops=[ast.Is()],
                        comparators=[ast.Name(id='Cell', ctx=ast.Load())]
                    ),
                    body=ast.Name(id='TRUE', ctx=ast.Load()),
                    orelse=ast.Name(id='FALSE', ctx=ast.Load())
                )
            if op_name == 'not':
                return ast.IfExp(
                    test=ast.Compare(left=arg_ast, ops=[ast.Is()],
                                     comparators=[ast.Name(id='FALSE', ctx=ast.Load())]),
                    body=ast.Name(id='TRUE', ctx=ast.Load()),
                    orelse=ast.Name(id='FALSE', ctx=ast.Load())
                )
            if op_name == 'zero?':
                return ast.IfExp(
                    test=ast.Compare(left=arg_ast, ops=[ast.Eq()],
                                     comparators=[ast.Constant(value=0)]),
                    body=ast.Name(id='TRUE', ctx=ast.Load()),
                    orelse=ast.Name(id='FALSE', ctx=ast.Load())
                )
            if op_name == 'positive?':
                return ast.IfExp(
                    test=ast.Compare(left=arg_ast, ops=[ast.Gt()],
                                     comparators=[ast.Constant(value=0)]),
                    body=ast.Name(id='TRUE', ctx=ast.Load()),
                    orelse=ast.Name(id='FALSE', ctx=ast.Load())
                )
            if op_name == 'negative?':
                return ast.IfExp(
                    test=ast.Compare(left=arg_ast, ops=[ast.Lt()],
                                     comparators=[ast.Constant(value=0)]),
                    body=ast.Name(id='TRUE', ctx=ast.Load()),
                    orelse=ast.Name(id='FALSE', ctx=ast.Load())
                )
            if op_name in ('even?', 'odd?'):
                parity = ast.BinOp(left=arg_ast, op=ast.Mod(),
                                   right=ast.Constant(value=2))
                if op_name == 'even?':
                    return ast.IfExp(
                        test=ast.Compare(left=parity, ops=[ast.Eq()],
                                         comparators=[ast.Constant(value=0)]),
                        body=ast.Name(id='TRUE', ctx=ast.Load()),
                        orelse=ast.Name(id='FALSE', ctx=ast.Load())
                    )
                else:
                    return ast.IfExp(
                        test=ast.Compare(left=parity, ops=[ast.NotEq()],
                                         comparators=[ast.Constant(value=0)]),
                        body=ast.Name(id='TRUE', ctx=ast.Load()),
                        orelse=ast.Name(id='FALSE', ctx=ast.Load())
                    )
            if op_name == 'string-length':
                return ast.Call(
                    func=ast.Attribute(value=arg_ast, attr='__len__', ctx=ast.Load()),
                    args=[], keywords=[]
                )
            if op_name == 'vector-length':
                return ast.Call(
                    func=ast.Attribute(value=arg_ast, attr='__len__', ctx=ast.Load()),
                    args=[], keywords=[]
                )
            return None

        if n_args >= 2 and op_name in _INLINE_ARITH:
            op_cls = _INLINE_ARITH[op_name]
            curr = self.compile_expr(node.args[0], lexical_vars)
            for arg in node.args[1:]:
                curr = ast.BinOp(left=curr, op=op_cls(),
                                 right=self.compile_expr(arg, lexical_vars))
            return curr

        if n_args == 2 and op_name in _INLINE_CMP:
            op_cls = _INLINE_CMP[op_name]
            left_ast = self.compile_expr(node.args[0], lexical_vars)
            right_ast = self.compile_expr(node.args[1], lexical_vars)
            return ast.IfExp(
                test=ast.Compare(left=left_ast, ops=[op_cls()],
                                 comparators=[right_ast]),
                body=ast.Name(id='TRUE', ctx=ast.Load()),
                orelse=ast.Name(id='FALSE', ctx=ast.Load())
            )
        return None

    def _compile_AppAst(self, node, lexical_vars):
        inline = self._compile_AppAst_inline(node, lexical_vars)
        if inline is not None:
            return inline
        proc_ast = self.compile_expr(node.proc, lexical_vars)
        args_list = ast.List(
            elts=[self.compile_expr(a, lexical_vars) for a in node.args],
            ctx=ast.Load()
        )
        return ast.Call(
            func=ast.Name(id='__mscm_invoke__', ctx=ast.Load()),
            args=[proc_ast, args_list, ast.Name(id='env', ctx=ast.Load())],
            keywords=[]
        )

    # ── 复盘2：用直接类型链替代字典+getattr ──
    def compile_expr(self, node, lexical_vars):
        t = type(node)
        if t is LiteralAst:
            return self._compile_LiteralAst(node, lexical_vars)
        if t is VarAst:
            return self._compile_VarAst(node, lexical_vars)
        if t is IfAst:
            return self._compile_IfAst(node, lexical_vars)
        if t is DefineAst:
            return self._compile_DefineAst(node, lexical_vars)
        if t is SetBangAst:
            return self._compile_SetBangAst(node, lexical_vars)
        if t is BeginAst:
            return self._compile_BeginAst(node, lexical_vars)
        if t is LambdaAst:
            return self._compile_LambdaAst(node, lexical_vars)
        if t is AppAst:
            return self._compile_AppAst(node, lexical_vars)
        return ast.Name(id='VOID', ctx=ast.Load())

    # ── compile_stmt 子方法 ──

    # ── 复盘9：修复 if 的 Scheme 真值语义（致命 BUG 修复）──
    def _stmt_IfAst(self, node, lexical_vars, is_tail):
        test_expr = self.compile_expr(node.test, lexical_vars)
        # Scheme 中只有 #f 是假值，其他一切（0, NIL, ""）都是真值
        # 必须用 "test is not FALSE" 而非 Python 的 truthy 判断
        cond = ast.Compare(
            left=test_expr, ops=[ast.IsNot()],
            comparators=[ast.Name(id='FALSE', ctx=ast.Load())]
        )
        then_stmts = self.compile_stmt(node.then, lexical_vars, is_tail)
        else_stmts = self.compile_stmt(node.else_, lexical_vars, is_tail)
        return [ast.If(test=cond, body=then_stmts, orelse=else_stmts)]

    def _stmt_BeginAst(self, node, lexical_vars, is_tail):
        stmts = []
        last_idx = len(node.exprs) - 1
        for i, expr in enumerate(node.exprs):
            is_last = (i == last_idx)
            stmts.extend(self.compile_stmt(expr, lexical_vars, is_tail if is_last else False))
        return stmts

    # ── 复盘14：临时变量用 __mscm_t_ 前缀避免冲突 ──
    def _stmt_self_tail_call(self, node, lexical_vars):
        n_args = len(node.args)
        n_params = len(self.params)
        
        # 处理 rest 参数的 self-tail-call
        if not self.is_simple:
            rest_idx = None
            for i, p in enumerate(self.params):
                if p.startswith('rest:'):
                    rest_idx = i
                    break
            if rest_idx is not None:
                n_fixed = rest_idx
                temp_names = [f"__mscm_t_{i}" for i in range(n_args)]
                assign_temps = []
                for temp_name, arg in zip(temp_names, node.args):
                    assign_temps.append(
                        ast.Assign(
                            targets=[ast.Name(id=temp_name, ctx=ast.Store())],
                            value=self.compile_expr(arg, lexical_vars)
                        )
                    )
                cleaned_params = [clean_param_name(p) for p in self.params]
                # 固定参数：取前 n_fixed 个 temp
                fixed_elts = [ast.Name(id=t, ctx=ast.Load()) for t in temp_names[:n_fixed]]
                rest_elts = [ast.Name(id=t, ctx=ast.Load()) for t in temp_names[n_fixed:]]
                fixed_targets = [ast.Name(id=p, ctx=ast.Store()) for p in cleaned_params[:n_fixed]]
                rest_target = ast.Name(id=cleaned_params[rest_idx], ctx=ast.Store())
                stmts = list(assign_temps)
                if fixed_targets:
                    if len(fixed_targets) == 1:
                        stmts.append(ast.Assign(
                            targets=[fixed_targets[0]],
                            value=fixed_elts[0]
                        ))
                    else:
                        stmts.append(ast.Assign(
                            targets=[ast.Tuple(elts=fixed_targets, ctx=ast.Store())],
                            value=ast.Tuple(elts=fixed_elts, ctx=ast.Load())
                        ))
                stmts.append(ast.Assign(
                    targets=[rest_target],
                    value=ast.Call(
                        func=ast.Name(id='_lst', ctx=ast.Load()),
                        args=[ast.List(elts=rest_elts, ctx=ast.Load())],
                        keywords=[]
                    )
                ))
                stmts.append(ast.Continue())
                return stmts
        
        if n_params == 1 and n_args == 1:
            p_name = clean_param_name(self.params[0])
            if not has_mutation(node.args[0], p_name):
                arg_expr = self.compile_expr(node.args[0], lexical_vars)
                return [
                    ast.Assign(
                        targets=[ast.Name(id=p_name, ctx=ast.Store())],
                        value=arg_expr
                    ),
                    ast.Continue()
                ]
        temp_names = [f"__mscm_t_{i}" for i in range(n_args)]
        assign_temps = []
        for temp_name, arg in zip(temp_names, node.args):
            assign_temps.append(
                ast.Assign(
                    targets=[ast.Name(id=temp_name, ctx=ast.Store())],
                    value=self.compile_expr(arg, lexical_vars)
                )
            )
        cleaned_params = [clean_param_name(p) for p in self.params]
        reassign_params = ast.Assign(
            targets=[ast.Tuple(
                elts=[ast.Name(id=p, ctx=ast.Store()) for p in cleaned_params],
                ctx=ast.Store()
            )],
            value=ast.Tuple(
                elts=[ast.Name(id=t, ctx=ast.Load()) for t in temp_names],
                ctx=ast.Load()
            )
        )
        return assign_temps + [reassign_params, ast.Continue()]

    def _stmt_cross_tail_call(self, node, lexical_vars):
        proc_ast = self.compile_expr(node.proc, lexical_vars)
        args_list = ast.List(
            elts=[self.compile_expr(a, lexical_vars) for a in node.args],
            ctx=ast.Load()
        )
        target = be.data.get(node.proc.name)
        if target is not None and callable(target) and not isinstance(target, LambdaProc):
            return [ast.Return(value=ast.Call(
                func=ast.Name(id='__mscm_invoke__', ctx=ast.Load()),
                args=[proc_ast, args_list, ast.Name(id='env', ctx=ast.Load())],
                keywords=[]
            ))]
        return [ast.Return(value=ast.Call(
            func=ast.Name(id='__mscm_make_tail_call__', ctx=ast.Load()),
            args=[proc_ast, args_list, ast.Name(id='env', ctx=ast.Load())],
            keywords=[]
        ))]

    def _is_cross_tail_target(self, node, lexical_vars):
        """检查是否为合法的交叉尾调用目标"""
        return (
            self.self_name is not None
            and isinstance(node.proc, VarAst)
            and node.proc.name != self.self_name
            and node.proc.name not in _INLINE_OPS
            and node.proc.name not in _IMMUTABLE_PRIMITIVES
            and node.proc.name not in lexical_vars
        )

    def _stmt_SetBangAst(self, node, lexical_vars, is_tail):
        val_ast = self.compile_expr(node.val, lexical_vars)
        if node.name in lexical_vars:
            stmts = [ast.Assign(
                targets=[ast.Name(id=node.name, ctx=ast.Store())],
                value=val_ast
            )]
        else:
            stmts = [ast.Expr(value=ast.Call(
                func=ast.Name(id='__mscm_env_set_var__', ctx=ast.Load()),
                args=[
                    ast.Name(id='env', ctx=ast.Load()),
                    ast.Constant(value=node.name),
                    val_ast
                ],
                keywords=[]
            ))]
        if is_tail:
            stmts.append(ast.Return(value=ast.Name(id='VOID', ctx=ast.Load())))
        return stmts

    def compile_stmt(self, node, lexical_vars, is_tail=False):
        t = type(node)
        if t is IfAst:
            return self._stmt_IfAst(node, lexical_vars, is_tail)
        if t is BeginAst:
            return self._stmt_BeginAst(node, lexical_vars, is_tail)
        if t is SetBangAst:
            return self._stmt_SetBangAst(node, lexical_vars, is_tail)
        if is_tail and t is AppAst:
            if isinstance(node.proc, VarAst):
                if node.proc.name == self.self_name:
                    return self._stmt_self_tail_call(node, lexical_vars)
                if self._is_cross_tail_target(node, lexical_vars):
                    return self._stmt_cross_tail_call(node, lexical_vars)
        expr = self.compile_expr(node, lexical_vars)
        if is_tail:
            return [ast.Return(value=expr)]
        return [ast.Expr(value=expr)]

    def compile_stmt_seq(self, nodes, lexical_vars, is_tail=False):
        if not nodes:
            return []
        stmts = []
        last_idx = len(nodes) - 1
        for i, expr in enumerate(nodes):
            stmts.extend(self.compile_stmt(expr, lexical_vars, is_tail if i == last_idx else False))
        return stmts

# ═══════════════════════════════════════════════════════════════
# compile_lambda_proc — 主编译入口
# 复盘4：用 contextmanager 保证 _IS_COMPILING 安全
# 复盘11：MSCM_JIT_DEBUG 下打印异常 traceback
# ═══════════════════════════════════════════════════════════════

# 宏展开委托给 Scheme 端 my-macro-expand (与 minischeme Compiler.ExpandViaScheme 一致)
# expr 是代码, 需 quote 防止被 Eval 求值
def expand_via_scheme(expr, env):
    quoted = Cell(SYM_QUOTE, Cell(expr, NIL))
    call = Cell(Sym('my-macro-expand'), Cell(quoted, Cell(env, NIL)))
    from miniscm import _eval
    return _eval(call, env)

# quasiquote 依赖运行时环境 (unquote), 不能在 JIT 编译期预展开。
# 若 lambda 体包含 quasiquote, 跳过 JIT 让解释器展开。
def has_quasiquote(expr):
    while isinstance(expr, SyntaxObject):
        expr = expr.expr
    if isinstance(expr, Cell):
        c = expr
        if isinstance(c.car, Sym) and c.car.name == 'quasiquote':
            return True
        return has_quasiquote(c.car) or has_quasiquote(c.cdr)
    return False

# 递归展开 quote/quasiquote 之外的所有子形式。
# quote/quasiquote 保持原样 (quasiquote 需运行时环境, JIT 已跳过)。
def fully_expand(expr):
    while isinstance(expr, SyntaxObject):
        expr = expr.expr
    if not isinstance(expr, Cell):
        return expr
    c = expr
    if isinstance(c.car, Sym) and c.car.name in ('quote', 'quasiquote'):
        return expr
    new_car = fully_expand(c.car)
    new_cdr = fully_expand(c.cdr)
    if new_car is c.car and new_cdr is c.cdr:
        return c
    return Cell(new_car, new_cdr)

def compile_lambda_proc(lambda_proc):
    _JIT_LOG("compile_lambda_proc ENTER: ", lambda_proc.name, " params=", lambda_proc.params)
    if not should_jit(lambda_proc):
        _JIT_LOG("SKIP_JIT: ", lambda_proc.name)
        return None
    if _IS_COMPILING:
        _JIT_LOG("compile_lambda_proc REENTRY GUARDED: ", lambda_proc.name)
        return None

    # 编译失败标记: 与 JSON body 缓存同目录的 .fail 文件。
    # 结构性失败(闭包/自递归/quasiquote)跨进程结果一致, 记录后避免每次重复编译尝试。
    _fail_file = None
    if lambda_proc.name is not None:
        _fail_file = os.path.join(CACHE_DIR,
            safe_file_name(lambda_proc.name) + "_" +
            body_hash_src(_pr(lambda_proc.body)) + ".fail")
        if os.path.exists(_fail_file):
            return None

    def _mark_failed():
        if _fail_file is not None:
            try:
                os.makedirs(CACHE_DIR, exist_ok=True)
                with open(_fail_file, "w") as f:
                    f.write("1")
                # json 与 fail 互斥：标记失败时清除可能残留的 json 缓存
                _json_path = os.path.splitext(_fail_file)[0] + ".json"
                if os.path.exists(_json_path):
                    os.remove(_json_path)
            except Exception:
                pass

    try:
        with _compiling_guard():
            # Step 1: 宏展开 body (带缓存, 与 C# CompileLambdaProc 一致)
            # 缓存文件: .mscm_cache/{SafeFileName(Name)}_{BodyHash(bodySrc)}.json
            # 缓存内容: CacheEntry { Version, Hash, Params, Body } — 宏展开后的 body 表单
            body_forms = []
            cur = lambda_proc.body
            cache_file = None
            body_src = None
            if lambda_proc.name is not None:
                cache_dir = CACHE_DIR
                body_src = _pr(lambda_proc.body)
                cache_file = os.path.join(cache_dir,
                    safe_file_name(lambda_proc.name) + "_" + body_hash_src(body_src) + ".json")
                if os.path.exists(cache_file):
                    try:
                        with open(cache_file) as f:
                            entry = json.load(f)
                        if (entry.get("version") == CACHE_VERSION
                                and entry.get("hash") == body_src
                                and entry.get("params") == lambda_proc.params
                                and entry.get("body") is not None):
                            from reader import read
                            for s in entry["body"]:
                                body_forms.append(read(s))
                            if body_forms:
                                cur = None  # 跳过展开
                            else:
                                body_forms = []
                    except Exception:
                        body_forms = []
                if cur is not None:
                    while isinstance(cur, Cell):
                        expanded = expand_via_scheme(cur.car, lambda_proc.env)
                        if has_quasiquote(expanded):
                            _mark_failed()
                            return None  # 需要运行时展开; 跳过 JIT
                        body_forms.append(fully_expand(expanded))
                        cur = cur.cdr
            else:
                while isinstance(cur, Cell):
                    expanded = expand_via_scheme(cur.car, lambda_proc.env)
                    if has_quasiquote(expanded):
                        return None
                    body_forms.append(fully_expand(expanded))
                    cur = cur.cdr

            # Step 2: Scheme → AST
            body_asts = [to_ast(f) for f in body_forms]
            cleaned_params = [clean_param_name(p) for p in lambda_proc.params]
            lexical_vars = set(cleaned_params)

            # Step 4: 常量折叠
            optimized_body_asts = [fold_constants(e) for e in body_asts]

            compiler_inst = AstExprCompiler(
                self_name=lambda_proc.name,
                params=lambda_proc.params,
                is_simple=lambda_proc.is_simple
            )

            # Step 5: 编译 AST → Python AST
            nested_ast_body = compiler_inst.compile_stmt_seq(
                optimized_body_asts, lexical_vars, is_tail=True
            )
            if not nested_ast_body:
                nested_ast_body = [ast.Return(value=ast.Name(id='VOID', ctx=ast.Load()))]

            loop_def = ast.While(
                test=ast.Constant(value=True),
                body=nested_ast_body,
                orelse=[]
            )
            func_args = ast.arguments(
                posonlyargs=[],
                args=[ast.arg(arg='env')] + [ast.arg(arg=p) for p in cleaned_params],
                kwonlyargs=[], kw_defaults=[], defaults=[]
            )
            func_def = ast.FunctionDef(
                name='compiled_body', args=func_args,
                body=[loop_def], decorator_list=[]
            )
            mod = ast.Module(body=[func_def], type_ignores=[])
            ast.fix_missing_locations(mod)
            code = compile(mod, filename="<mscm-jit-lambda>", mode="exec")

            # 步骤 5: exec → py_func（复盘15：用 _make_jit_globals）
            g = _make_jit_globals(compiler_inst.constants)
            exec(code, g)
            py_func = g['compiled_body']

            # 成功编译后才写 json 缓存，与 .fail 互斥（失败只写 .fail，绝不并存）
            if cache_file is not None:
                try:
                    os.makedirs(CACHE_DIR, exist_ok=True)
                    entry = {
                        "version": CACHE_VERSION,
                        "hash": body_src,
                        "params": lambda_proc.params,
                        "body": [_pr(f) for f in body_forms]
                    }
                    with open(cache_file, "w") as f:
                        json.dump(entry, f, ensure_ascii=False)
                    # json 与 fail 互斥：成功时清除可能残留的 fail 标记
                    _fail_path = os.path.splitext(cache_file)[0] + ".fail"
                    if os.path.exists(_fail_path):
                        os.remove(_fail_path)
                except Exception:
                    pass

            return CompiledLambda(
                py_func, lambda_proc.params, lambda_proc.env, lambda_proc.is_simple
            )

    # 复盘11：MSCM_JIT_DEBUG 下打印异常 traceback
    except Exception:
        _mark_failed()
        if os.environ.get('MSCM_JIT_DEBUG'):
            import traceback
            traceback.print_exc()
        return None

# ═══════════════════════════════════════════════════════════════
# 字节码缓存 — 复盘8：用 type() is 精确匹配
# ═══════════════════════════════════════════════════════════════

# 与 C# Compiler.SafeFileName 一致 — 将非法文件名字符转义
def safe_file_name(name):
    sb = []
    for ch in name:
        if ch in '?!<>=*|:"/\\':
            sb.append(f"_{ord(ch):x}")
        else:
            sb.append(ch)
    return "".join(sb)

# 与 C# Compiler.BodyHash 一致 — 用 body 源码 hash 命名缓存文件 (大写 hex, 与 C# ToHexString 一致)
def body_hash_src(body_src):
    return hashlib.sha256(body_src.encode('utf-8')).hexdigest()[:16].upper()

class LambdaProc:
    __slots__ = ('name', 'params', 'body', 'env', 'is_simple',
                 'call_count', 'compiled_version', '_eval_fn_cache', '_jit_failed')
    
    def __init__(self, params, body, env, is_simple, name=None):
        self.name = name
        self.params = params
        self.body = body
        self.env = env
        self.is_simple = is_simple
        self.call_count = 0
        self.compiled_version = None
        self._eval_fn_cache = None
        self._jit_failed = False

    def _get_eval_fn(self):
        """延迟导入 _eval，缓存避免每次调用重复导入"""
        if self._eval_fn_cache is None:
            from miniscm import _eval as _eval_fn
            self._eval_fn_cache = _eval_fn
        return self._eval_fn_cache

    def __call__(self, *args):
        if _JIT_ALLOWED() and not _IS_COMPILING and not self._jit_failed and should_jit(self):
            self.call_count += 1
            if not self.compiled_version:
                _JIT_LOG("JIT: compile trigger name=", self.name, " calls=", self.call_count)
                try:
                    cv = compile_lambda_proc(self)
                    if cv is not None:
                        self.compiled_version = cv
                    else:
                        # 编译失败(如嵌套自递归/闭包/quasiquote), 标记避免重复尝试
                        self._jit_failed = True
                except Exception:
                    if os.environ.get('MSCM_JIT_DEBUG'):
                        import traceback
                        traceback.print_exc()
                    self._jit_failed = True

            if self.compiled_version is not None:
                try:
                    r = _invoke_compiled(self.compiled_version, args)
                    while isinstance(r, TailCall):
                        r = __mscm_eval_tail_call__(r)
                    return r
                except Exception:
                    if os.environ.get('MSCM_JIT_DEBUG'):
                        import traceback
                        traceback.print_exc()
                    # 降级到解释路径

        # 复盘10：fallback 路径也解包 TailCall
        from miniscm import eval_seq
        nenv = Env(self.env)
        _bind_params(self.params, args, nenv)
        r = eval_seq(self.body, nenv)
        eval_fn = self._get_eval_fn()
        while isinstance(r, TailCall):
            r = eval_fn(r.expr, r.env)
        return r