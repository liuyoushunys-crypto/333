# miniscm.py
#!/usr/bin/env python3
"""
模块：miniscm.py
职能：引导求值器的核心大循环分发、蹦床机制实现、内建过程初始注入以及 REPL 启动。
"""
# ═══════════════════════════════════════════════════════════════
# 模块入口：miniscm.py 是整个 Scheme 解释器的心脏。它定义了 _eval 主循环
# （基于 while True + TailCall 的蹦床/尾调用消除机制）、所有特殊形式处理器
# （h_quote / h_if / h_lambda / h_define 等）、宏展开递归、quasiquote 展开、
# REPL 交互式循环，以及引导启动时的内置环境初始化。
#
# 核心设计哲学：用 Python 的 while 循环模拟 Scheme 的尾调用优化（TCO）。
# 通过返回 TailCall(expr, env) 帧，外层 _eval 的 while True 循环直接提取
# 新的 expr 和 env 继续分发，完全不增长 Python 调用栈。这对于深度递归的
# Scheme 程序（如 10 万层尾递归）至关重要。
# ═══════════════════════════════════════════════════════════════

import sys, os
sys.setrecursionlimit(3000)


from mtypes import (
    SYM_SETF, SYM_THE_ENVIRONMENT, SYM_UNQUOTE, SYM_UNSPLICE, SYM_USYNTAX,
    Sym, Cell, SyntaxObject, TailCall, NIL, VOID, EOF, TRUE, FALSE,
    SYM_BEGIN, SYM_DEFINE, SYM_IF, SYM_LAMBDA, SYM_QUOTE, SYM_SETBANG,SYM_IMPORT,
    Env, be, _UNBOUND,
    _pr, _sn, _bind_params
)
# mtypes 提供了所有基础类型和辅助函数：
#   Sym      — Scheme 符号，interned，比较用 is 而非 ==
#   Cell     — 序对（cons cell），car + cdr
#   SyntaxObject — 包装了语法上下文的表达式，需要解包 .expr
#   TailCall — 尾调用帧，包含 expr 和 env，引导蹦床
#   NIL / VOID / EOF / TRUE / FALSE — 常量单例
#   Env      — 词法环境，链式 parent 结构
#   be       — 全局内置环境（在模块加载时创建，由 primitives.py 填充）
#   _sn(x)   — 提取符号名字符串：Sym.name
#   _bind_params — 绑定 lambda 参数到新环境

from reader import read_all
from compiler import LambdaProc

# ═══════════════════════════════════════════════════════════════
# 1. 特殊核心形式处理器（Special Forms Dispatcher）
# ═══════════════════════════════════════════════════════════════
# 特殊形式（special form）是 Scheme 语言的核心语法结构，如 quote、if、
# lambda、define、set! 等。它们不由函数调用的方式处理——参数不预先求值，
# 而是由处理器自行决定哪些子表达式需要求值。
#
# 注册方式：@put(SYM_XXX) 装饰器将处理器函数存入 SPECIALS 字典。
# 在 _eval 主循环中，当 op 是特殊形式符号时，直接调用对应的处理器。
#
# 关键约定：每个处理器接收 (args, env)，其中 args 是 cdr 部分（即操作数
# 列表）。返回值可以是：
#   - 普通值：直接返回给 _eval，_eval 再返回给调用者
#   - TailCall(expr, env)：引导 _eval 的 while 循环继续求值 expr，
#     实现尾调用消除
#   - SyntaxObject：如果处理器返回了未解包的 SyntaxObject，_eval 会在
#     检查 TailCall 之后自动解包（见 _eval 中 `return r` 前的逻辑）

def seq_tail_call(seq, env):
    """
    按顺序评估块中前 N-1 个表达式，并在最后一项返回 TailCall 帧，
    引导外层解释主大循环进行 Trampoline 无栈调用。
    """
    # 这是实现 TCO（尾调用优化）的核心辅助函数。
    # 它的工作机制：
    #   - 对于 begin 块中的表达式序列 (e1 e2 ... en)，依次求值 e1 到 e_{n-1}，
    #     这些求值是对 _eval 的直接调用（栈会增长，但返回后收缩）。
    #   - 对于最后一个表达式 en，不直接求值，而是返回 TailCall(en, env)。
    #   - 外层 _eval 的 while 循环检测到 TailCall 后，提取 en 和 env，
    #     继续循环反复，完全避免最后一递归层。
    #
    # 为什么必须用 TailCall？
    #   如果直接调用 _eval(en, env)，语义正确但 Python 栈会多一层。
    #   对于深度尾递归（如 10 万次），这层会撑爆 Python 栈。
    #   TailCall 帧让 _eval 的 while 循环接手，实现物理上的尾递归消除。
    #
    # 边界情况：seq 为空（NIL）时返回 VOID。
    if seq is NIL: return VOID
    cur = seq
    while isinstance(cur, Cell) and isinstance(cur.cdr, Cell):
        _eval(cur.car, env)
        cur = cur.cdr
    if isinstance(cur, Cell):
        return TailCall(cur.car, env)
    return _eval(cur, env)

SPECIALS = {}
def put(sym):
    def deco(f): SPECIALS[sym] = f; return f
    return deco

def strip_syntax(v):
    """递归还原所有 SyntaxObject 嵌套包装"""
    # 用于 quote 处理器：quote 返回的表达式必须是“干净”的语法对象，
    # 即去掉所有 SyntaxObject 包装。因为 quote 是字面量引用，不需要
    # 保留语法上下文。
    #
    # 递归策略：
    #   1. 如果 v 是 SyntaxObject，剥去最外层包装后继续检查
    #      （SyntaxObject 可以嵌套多层）
    #   2. 如果 v 是 Cell（列表/序对），递归剥除 car 和 cdr 中的
    #      SyntaxObject
    #   3. 原子值直接返回
    #
    # 性能注意：while 循环而非递归处理 SyntaxObject 的嵌套链，
    # 避免了 Python 递归深度问题。
    while isinstance(v, SyntaxObject):
        v = v.expr
    if isinstance(v, Cell):
        return Cell(strip_syntax(v.car), strip_syntax(v.cdr))
    return v

@put(SYM_QUOTE)
def h_quote(args,env): 
    # quote: 返回字面量本身，不求值。使用 strip_syntax 去掉所有
    # 可能包裹的 SyntaxObject，因为 quote 的结果是纯粹的语法字面量。
    # 例如 (quote a) → 返回符号 a，而不是 SyntaxObject(a)。
    # args.car 是 (quote <expr>) 中的 <expr>。
    return strip_syntax(args.car)

@put(SYM_THE_ENVIRONMENT)
def h_the_environment(args, env):
    # the-environment: 返回当前词法环境对象 (供 (eval expr env) 使用)。
    # 与 C# Evaluator.HTheEnvironment 一致 — 特殊形式, 直接返回词法 env。
    return env

@put(SYM_IF)
def h_if(args,env):
    # if: (if test then-expr else-expr)
    # 求值 test，若为 FALSE 则走 else 分支（如果存在），否则走 then 分支。
    # 两个分支都返回 TailCall 以实现尾上下文。
    # 如果没有 else 分支且 test 为假，返回 VOID。
    t=_eval(args.car,env)
    if t is FALSE:
        # 注意检查 else 分支的存在性：
        #   args.cdr 是 (then-expr) 时，cdr.cdr 是 NIL → 无 else → 返回 VOID
        #   args.cdr 是 (then-expr else-expr) 时，cdr.cdr.car 是 else-expr
        return VOID if args.cdr is NIL or args.cdr.cdr is NIL else TailCall(args.cdr.cdr.car,env)
    return TailCall(args.cdr.car,env)

@put(SYM_LAMBDA)
def h_lambda(args,env):
    """
    构造优化包装的 Lambda 过程（即 LambdaProc 实例）。
    LambdaProc 代理了该过程，负责管理 JIT 计数、自动字节码编译及解释 fallback。
    """
    # 解析参数列表：形如 (lambda (x y) body) 或 (lambda x body) 或
    # (lambda (x . rest) body)。
    # params 列表存储参数名，可变参数以 'rest:name' 格式存储。
    # has_rest 标记是否有 rest 参数。
    # not has_rest → is_simple 表示是否简单参数（无 rest）。
    # LambdaProc 的构造参数：(params, body, env, is_simple, name=None)
    params = []
    cur = args.car
    has_rest = False
    while isinstance(cur, Cell): 
        params.append(_sn(cur.car))
        cur = cur.cdr
    if cur is not NIL: 
        params.append('rest:' + _sn(cur))
        has_rest = True
    return LambdaProc(params, args.cdr, env, not has_rest)

@put(SYM_BEGIN)
def h_begin(args,env):
    # begin: (begin e1 e2 ... en) 依次求值每个表达式，返回最后一个的值。
    # 委托给 seq_tail_call，确保最后一个表达式的求值在尾上下文中。
    return seq_tail_call(args, env)

@put(SYM_DEFINE)
def h_define(args,env):
    """在词法环境定义变量，或者构造命名 lambda。传递定义名以便 JIT 的 self-TCO 循环化匹配。"""
    # define 有两种形式：
    #   1. (define name value) — 简单变量定义
    #   2. (define (fn-name params ...) body ...) — 命名 lambda（语法糖）
    #      等效于 (define fn-name (lambda (params ...) body ...))
    # 对于命名 lambda，将 name 传给 LambdaProc 以便 JIT 编译器能识别
    # 自递归（self-recursion）并生成 while 循环优化代码。
    pat = args.car
    if isinstance(pat, Cell):
        name = _sn(pat.car)
        params = []
        cur = pat.cdr
        has_rest = False
        while isinstance(cur, Cell): 
            params.append(_sn(cur.car))
            cur = cur.cdr
        if cur is not NIL: 
            params.append('rest:' + _sn(cur))
            has_rest = True
        # 传递定义变量名给 LambdaProc
        env.data[name] = LambdaProc(params, args.cdr, env, not has_rest, name=name)
    else:
        name = str(pat)
        val = _eval(args.cdr.car, env)
        # (define name (lambda ...)) 形式同样传递名字, 使宏展开生成的
        # 函数(define-syntax/syntax-rules 展开产物)也能 JIT 编译缓存
        if isinstance(val, LambdaProc) and val.name is None:
            val.name = name
        env.define(name, val)
    # 注意：define 的返回值在 R5RS 中通常是未指定的；这里返回被定义符号
    return Sym(str(pat))

@put(SYM_SETBANG)
def h_set(args,env):
    # set!: (set! var expr) — 修改变量值。
    # 与 define 不同，set! 只在已有绑定中查找，不会创建新绑定。
    # 查找策略：从当前环境向上遍历 parent 链，找到匹配的绑定就修改。
    # 如果直到顶层都找不到，就在当前环境创建新绑定（R5RS 允许）。
    #
    # 特殊路径：如果 set! 的目标不是符号（如 (set! (car x) v)），
    # 则转化为 SYM_SETF 调用，由 h_setf 处理通用位置设置。
    if isinstance(args.car, Sym):
        n=args.car.name; v=_eval(args.cdr.car,env)
        e=env
        while e:
            if n in e.data:
                e.data[n]=v; return VOID
            e=e.parent
        env.define(n,v); return VOID
    return TailCall(Cell(SYM_SETF,Cell(args.car,Cell(args.cdr.car,NIL))),env)

@put(SYM_SETF)
def h_setf(args,env):
    # set!-form: 通用位置设置 (与 C# Evaluator.HSetf 一致)。
    # (set! (car x) v) → (set-car! x v); (set! (cdr x) v) → (set-cdr! x v)。
    a = args
    place = a.car
    val = a.cdr.car if isinstance(a.cdr, Cell) else NIL
    if isinstance(place, Cell) and isinstance(place.car, Sym):
        ps = place.car.name
        if ps in ('car', 'cdr'):
            setter = Sym('set-' + ps + '!')
            target = place.cdr.car if isinstance(place.cdr, Cell) else NIL
            return TailCall(Cell(setter, Cell(target, Cell(val, NIL))), env)
    raise Exception(f"set!: invalid place: {place}")

# @put(SYM_UNQUOTE)
# @put(SYM_UNSPLICE)
# @put(SYM_USYNTAX)
def h_unquote(args, env):
    # unquote/unsyntax 在 quasiquote 外使用时报错 (与 C# HUnquote 一致)
    raise Exception("unquote outside quasiquote")

def eval_seq(seq, env):
    # eval_seq: 对一系列表达式逐个求值，返回最后一个的值。
    # 与 seq_tail_call 不同，这里直接调用 _eval 而非返回 TailCall。
    # 用于非尾上下文的表达式序列求值（如宏展开的 body）。
    # 注意：前 N-1 个表达式的返回值被丢弃（赋值给 r 但下一轮被覆盖）。
    r = VOID
    cur = seq
    while isinstance(cur, Cell): 
        r = _eval(cur.car, env)
        cur = cur.cdr
    return r

def eval_args_to_array(_cur, env):
    if not isinstance(_cur, Cell):
        return []
    c = _cur
    if c.cdr is NIL:
        return [_eval(c.car, env)]
    if isinstance(c.cdr, Cell) and c.cdr.cdr is NIL:
        return [_eval(c.car, env), _eval(c.cdr.car, env)]
    if isinstance(c.cdr, Cell) and isinstance(c.cdr.cdr, Cell) and c.cdr.cdr.cdr is NIL:
        return [_eval(c.car, env), _eval(c.cdr.car, env), _eval(c.cdr.cdr.car, env)]
    evaled = []
    while isinstance(_cur, Cell):
        evaled.append(_eval(_cur.car, env))
        _cur = _cur.cdr
    return evaled

def _ensure_jit_compiled(proc_lp):
    from compiler import USE_JIT, _IS_COMPILING, should_jit, compile_lambda_proc
    if not (USE_JIT and not _IS_COMPILING and not proc_lp._jit_failed and should_jit(proc_lp)):
        return
    if proc_lp.compiled_version:
        return
    try:
        cv = compile_lambda_proc(proc_lp)
        if cv:
            proc_lp.compiled_version = cv
        else:
            # 编译失败(如嵌套自递归/闭包/quasiquote), 标记避免重复尝试
            proc_lp._jit_failed = True
    except Exception:
        proc_lp._jit_failed = True

def _eval_compiled_lambda(cv, _cur, env):
    """执行已编译的 LambdaProc。"""
    evaled_args = eval_args_to_array(_cur, env)
    if cv.is_simple:
        return cv.py_func(Env(cv.env), *evaled_args)
    from mtypes import _lst
    n_reg = len(cv.params) - 1
    return cv.py_func(Env(cv.env), *evaled_args[:n_reg], _lst(evaled_args[n_reg:]))

def _eval_tuple_lambda(proc_val, _cur, env):
    """执行老的 tuple lambda 格式。"""
    _, params, body, penv, is_simple = proc_val
    nenv = Env(penv)
    if is_simple:
        p_len = len(params)
        evaled_args = eval_args_to_array(_cur, env)
        if p_len == 0 and not evaled_args:
            pass
        elif p_len == len(evaled_args):
            for i, p in enumerate(params):
                nenv.data[p] = evaled_args[i]
        else:
            _bind_params(params, evaled_args, nenv)
    else:
        _bind_params(params, eval_args_to_array(_cur, env), nenv)
    return seq_tail_call(body, nenv)


def _eval(expr, env):
    _unbound_sentinel = _UNBOUND
    from primitives_first import expand_macro
    while True:
        # B0: 符号
        if isinstance(expr, Sym):
            if expr is TRUE or expr is FALSE:
                return expr
            return env.lookup(expr)

        # B1: 列表
        if not isinstance(expr, Cell):
            if expr is TRUE or expr is FALSE or expr is NIL or expr is VOID or expr is EOF:
                return expr
            if isinstance(expr, SyntaxObject):
                return expr.expr
            return expr

        op = expr.car
        args = expr.cdr

        # B1a: 特殊形式
        handler = SPECIALS.get(op)
        if handler is not None:
            r = handler(args, env)
            if isinstance(r, TailCall):
                expr, env = r.expr, r.env
                continue
            return r

        # B1b: 宏展开 (与 C# Evaluator.EvalCore 一致)
        #   op 为符号时查绑定: 若已绑定, 尝试 ExpandMacro (内部判断是否
        #   "macro" 元组, 否则返回 None); 展开成功 → 继续主循环。
        #   未绑定 → 求值 op 本身作为过程值。
        proc = _unbound_sentinel
        if isinstance(op, Sym):
            proc = env.lookup_silent(op, _unbound_sentinel)
            if proc is not _unbound_sentinel:
                new_expr = expand_macro(proc, args, env)
                if new_expr is not None:
                    expr = new_expr
                    continue
            else:
                proc = _eval(op, env)
        else:
            proc = _eval(op, env)

        _cur = args

        # B1c: LambdaProc (与 C# LambdaProc 分支一致)
        if isinstance(proc, LambdaProc):
            if proc.compiled_version is None:
                _ensure_jit_compiled(proc)
            if proc.compiled_version is not None:
                r = _eval_compiled_lambda(proc.compiled_version, _cur, env)
                if isinstance(r, TailCall):
                    expr, env = r.expr, r.env
                    continue
                if r is True: r = TRUE
                elif r is False: r = FALSE
                return r
            nenv = Env(proc.env)
            _bind_params(proc.params, eval_args_to_array(_cur, env), nenv)
            r = seq_tail_call(proc.body, nenv)
            if isinstance(r, TailCall):
                expr, env = r.expr, r.env
                continue
            if r is True: r = TRUE
            elif r is False: r = FALSE
            return r

        evaled_args = eval_args_to_array(_cur, env)

        # B1d: 普通 callable (与 C# Func/Delegate 分支一致)
        if callable(proc):
            r = proc(*evaled_args)
            if isinstance(r, TailCall):
                expr, env = r.expr, r.env
                continue
            if r is True: r = TRUE
            elif r is False: r = FALSE
            return r

        # B1f: Tuple Lambda (与 C# ITuple "lambda" 分支一致)
        if isinstance(proc, tuple) and proc[0] == 'lambda':
            r = _eval_tuple_lambda(proc, _cur, env)
            if isinstance(r, TailCall):
                expr, env = r.expr, r.env
                continue
            if isinstance(r, SyntaxObject):
                return r.expr
            return r

        raise TypeError(f"not callable: {proc}")

# ═══════════════════════════════════════════════════════════════
# 4. 引导启动与内置环境装载
# ═══════════════════════════════════════════════════════════════


# 注册 Scheme 宏系统自举所需的桥接原语。
# 移除 Python 端 define-macro/define-syntax 等特殊形式后,
# boot-min2/my-definemacro2 的 Scheme 宏引擎依赖这些桥接。
# 各桥接行为与 C# minischeme (PrimitiveRegistry.Init.cs / Evaluator.cs) 一致:
#   eval            — (eval expr [env]) 求值于指定环境
#   the-environment — 特殊形式, 返回当前词法环境 (h_the_environment)
#   sx-def-env      — 当前宏定义环境 (展开期间) 或全局
#   sx-expand-env   — 当前宏调用点环境 (展开期间) 或全局
#   sx-defined?     — 名称在环境中是否有绑定
#   sx-defmacro     — 注册 ("macro", pattern, body, env, true) 元组
#   sx-expand-call  — 单次宏展开; (car expr) 为 "macro" 元组则展开, 否则 FALSE

def load_file(path):
    # 加载并求值 Scheme 文件中的所有顶层表达式。
    # 使用 be（全局内置环境）作为求值环境。
    #
    # 重要警告：except: pass 静默捕获所有异常。
    # 这意味着文件中的语法错误、未定义变量、类型错误等
    # 都只会跳过出错表达式，不会中断加载过程。
    # 这是有意为之（引导库可能包含条件加载的表达式），
    # 但也意味着错误很难调试——打印了 n 但不会告诉你哪个表达式失败了。
    #
    # 返回值是被成功求值的表达式数量。
    with open(path,encoding="utf-8") as f: src=f.read()
    exprs=read_all(src); n=0
    for expr in exprs:
        if expr is EOF: continue
        try: _eval(expr,be); n+=1
        except: pass
    return n

_BASE = os.path.dirname(os.path.abspath(__file__))

def _repl_line(prompt):
    # 读取单行输入。处理 EOFError（Ctrl+D）和 KeyboardInterrupt（Ctrl+C），
    # 返回 None 表示退出。
    try:
        return input(prompt)
    except (EOFError, KeyboardInterrupt):
        return None

def _repl_multiline():
    # 多行输入处理器。支持跨多行的表达式输入。
    # 通过计算括号深度（( 计数减 ) 计数）判断表达式是否完整。
    # depth <= 0 时认为表达式完整，返回所有行拼接的字符串。
    #
    # 注意：这只统计圆括号，不支持方括号或花括号。
    # 字符串字面量中的括号也会被计数（可能导致出错）。
    #
    # 两种输入模式：
    #   - 交互式（isatty）：使用 input()，显示 mscm> 和 .> 提示符
    #   - 管道模式（非 isatty）：从 sys.stdin.readline 读取，
    #     将提示符写入 stderr（避免污染 stdout 的输出）
    lines = []
    depth = 0
    interactive = sys.stdin.isatty()
    while True:
        if not lines:
            p = 'mscm> '
        else:
            p = '.> '
        if interactive:
            line = _repl_line(p)
            if line is None:
                if lines: return '\n'.join(lines)
                return None
            if not line.strip() and not lines:
                continue
        else:
            sys.stderr.write(p)
            sys.stderr.flush()
            try:
                line = sys.stdin.readline()
            except:
                if lines: return '\n'.join(lines)
                return None
            if not line:
                if lines: return '\n'.join(lines)
                return None
            line = line.rstrip('\n')
            if not line.strip() and not lines:
                continue
        lines.append(line.rstrip('\n'))
        depth += line.count('(') - line.count(')')
        if depth <= 0:
            return '\n'.join(lines)

def repl():
    # REPL 主循环：Read-Eval-Print-Loop。
    # 1. 显示欢迎信息
    # 2. 尝试加载 readline 支持（历史记录、行编辑）
    # 3. 循环：读取多行输入 → 解析为表达式 → 求值 → 打印结果
    # 4. 只打印非 VOID 的结果
    # 5. 支持 ,quit 和 (exit) 退出
    # 6. 退出时保存历史记录（如果有 readline）
    print("miniscm v1.0 — 零依赖 Scheme 自举引导器")
    hist = None
    try:
        import readline
        hist = os.path.expanduser('~/.miniscm_history')
        try: readline.read_history_file(hist)
        except: pass
        readline.set_history_length(500)
    except ImportError:
        pass
    while True:
        try:
            source = _repl_multiline()
            if source is None:
                print(); break
            if not source.strip(): continue
            if source.strip() in (',quit','(exit)'): break
            try:
                for expr in read_all(source):
                    if expr is EOF: continue
                    r=_eval(expr,be)
                    if r is not VOID: print(_pr(r))
            except Exception as e:
                print(f"error: {e}")
        except KeyboardInterrupt:
            print(); continue
    if hist:
        try:
            import readline
            readline.write_history_file(hist)
        except: pass


if __name__=='__main__':
    # ═══════════════════════════════════════════════════════════
    # 程序入口
    # ═══════════════════════════════════════════════════════════
    # 引导流程：
    #   1. 调用 initenv() 注册所有内置过程到 be（全局环境）
    #   2. 逐个加载 scm/ 目录下的 Scheme 引导库
    #      （boot-core、boot-sugar、SRFI 库等）
    #   3. 如果命令行有参数，逐个加载并求值指定的 .scm 文件
    #   4. 如果没有参数，启动 REPL
    #
    # 库加载失败（如文件不存在）被静默处理（except: pass），
    # 确保解释器即使在库不全的情况下也能启动。
    from initenv_first import initenv_first
    initenv_first()
    from initenv import initenv
    initenv()
    # ── Scheme 宏系统桥接原语 (boot-min2 自举所需) ──
    import compiler
    compiler.PYB_MODE = 'scm'
    compiler.USE_JIT = True
    # 全功能引导: 核心宏引擎 + 扩展库。
    # define-macro/define-syntax/quasiquote/syntax-rules 由 Scheme 端实现,
    # Python 侧仅保留桥接原语 (sx-defmacro/sx-expand-call 等)。
    # _libs = ['my-definemacro2.scm','boot-min2.scm','boot-core.scm','boot-sugar.scm']
    _libs = ['boot-min2.scm','boot-core.scm','boot-sugar.scm']
    for f in _libs:
        try:
            n=load_file(_BASE+'/scm/'+f)
            sys.stderr.write(f"loaded {n} from {f}\n")
        except: pass


    pyb = False
    import compiler
    compiler.PYB_MODE = 'pyb' if pyb else 'scm'
    from initenv_ext import initenv_ext
    initenv_ext()
    if pyb:
        pass
    else:
        _libs = ['char-boolean.scm','numeric.scm',
                'srfi-1-list.scm','srfi-13-string.scm','hof-vector.scm',
                'number-theory.scm','gensym-stream.scm',
                'data-structures-ext.scm','srfi-14-char-set.scm',
                'generators.scm','misc.scm','fill-gaps.scm']
        for f in _libs:
            try:
                n=load_file(_BASE+'/scm/'+f)
                sys.stderr.write(f"loaded {n} from {f}\n")
            except: pass


        # scm 库 parameterize 版 with-output-to-string 需 display 支持端口重定向，
        # Python 的 display 写 sys.stdout，故用原生版（sys.stdout 切换）
        from primitives import call as _call2
        import io as _io2
        from mtypes import SchemeString as _SS2
        def _wots2(thunk):
            buf = _io2.StringIO()
            old = sys.stdout
            sys.stdout = buf
            try:
                _call2(thunk, [])
                return _SS2(buf.getvalue())
            finally:
                sys.stdout = old
        be.define('with-output-to-string', _wots2)

    # from initenv_py import initenv_py
    # initenv_py()

    if len(sys.argv)>1:
        for p in sys.argv[1:]: n=load_file(p); print(f"loaded {n} forms from {p}")
    else: repl()
