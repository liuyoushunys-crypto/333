# miniscm — Agent Guide

**Zero-dependency Scheme interpreter.** Two aligned implementations:
- **Python**: `miniscm/` — pure Python 3, AST-based JIT compiler.
- **C#**: `minischeme/` — .NET (net10.0), Expression-tree JIT compiler.

C# 是基准实现，Python 侧执行逻辑与其严格对齐（trampoline / 尾调用 / 宏系统 / JIT 语义逐分支对应）。

## Entrypoints

### Python
- **Interpreter**: `python3 miniscm.py [file.scm ...]` — batch evaluates file(s). No args starts REPL (`mscm>`).
- **Python API**: `from miniscm import load_file; load_file("path.scm")` — load Scheme into the global env.
- **Dual mode (`pyb`)**: controlled by env var `MSCM_PYB`（默认 `"1"`=True）. `pyb=True`（Python builtins from `primitives_ext.py` + 3 boot libs）/ `pyb=False`（再加 10 个 `scm/*.scm` 扩展库）. **JIT 恒开启（`compiler.USE_JIT = True`），pyb 不影响 JIT**。
- 必须从 `miniscm/` 目录运行：`cd miniscm && python3 miniscm.py test/xxx.scm` — `load` 的相对路径按进程 CWD 解析（`miniscm/scm/...`），从仓库根跑会找不到 `scm/lang/*.scm` 等文件。

### C#
- 构建+运行: `cd minischeme && dotnet run -- test/xxx.scm`（`load` 按 CWD 解析，需在 `minischeme/` 下；`test`/`test1`/`tools` 是 `minischeme/` 下的符号链接，直接用 `test/`/`test1/` 相对路径，不要用 `../test`；`dotnet run` 用 Debug 配置，`dotnet run -c Release` 用 Release）。
- `MSCM_PYB == "1"` 时 `pyb=True`（只加载前 3 个 boot 库 + `PrimitiveRegistry.InitExt()`）；默认 False（加载全部 scm 库）。
- `MSCM_JIT_DEBUG` 环境变量（py/C# 通用）开启 JIT 编译日志与异常 traceback。

## Architecture

```
miniscm/
  mtypes.py           (766 行) — Sym, Cell, Env, NIL, VOID, EOF, SyntaxObject, TailCall, Box, 基本类型 + 辅助
  reader.py           (494 行) — tokenizer + 递归下降解析器 → Cell/Sym/vector/inf/nan 字面量
  miniscm.py          (666 行) — _eval 主循环 (trampoline)、特殊形式、宏分发 (expand_macro)、桥接原语、REPL、pyb 引导
  compiler.py         (1594 行) — JIT AST 编译器、AstExprCompiler、CompiledLambda、LambdaProc、
                                  __mscm_invoke__/__mscm_try_unpack_tail_call__/__mscm_make_tail_call__、缓存
  primitives.py       (920 行) — 内建过程 (~300)，initenv() 注册进全局 Env `be`
  primitives_first.py (499 行) — expand_macro（"macro" 元组展开，_expand_macro_compiled 走 __mscm_invoke__）
  primitives_ext.py   (2314 行) — pyb=True 模式的扩展 builtins
  minref.py           (531 行) — REPL ,expand 的宏展开显示（quasiquote walk 等）
  native_syntax.py    (292 行) — 原生 syntax-rules 编译（单一事实源，minref 复用）
  scm/                — Scheme 引导库 (21 文件): boot-min2/boot-core/boot-sugar/srfi-*/lang-* 等
  .mscm_cache/        — JIT 字节码缓存目录 (CACHE_VERSION=6)
minischeme/           — C# 对照实现: Program.cs / Evaluator.cs / Compiler.cs / JitRuntime.cs /
                        NativeSyntax.cs / MinRef.cs / PrimitiveRegistry*.cs / Ext.*.cs / scm/ (21 文件)
test/                 — 31 个回归测试 (.scm, 内联断言)
test1/                — 220 个补充测试 (syntax-rules 等)
tools/regression.py   — 回归测试 runner (统计 [PASS]/[FAIL]/error 行)
```

宏系统与 minischeme (C#) 完全对齐: Python 端无宏引擎, define-macro/define-syntax/quasiquote/syntax-rules 全部由 Scheme 端 boot-min2.scm 自举实现。Python 仅保留桥接原语 (sx-defmacro/sx-expand-call/sx-def-env/sx-expand-env/the-environment)，宏展开通过 `_expand_macro`（等价 C# `Evaluator.ExpandMacro`）处理 `("macro", pattern, body, env, true)` 元组；原生 syntax-rules 编译器 (native_syntax.py / NativeSyntax.cs) 优先，失败回退 Scheme 引擎。

## Running Tests

### Python
```sh
cd miniscm
python3 miniscm.py test/test-vectors.scm          # 单文件（test 是指向 ../test 的链接）
MSCM_PYB=0 python3 miniscm.py test/test-vectors.scm   # pyb=False 模式
```
- 从仓库根跑回归（runner 内部以 `cwd=miniscm/` 运行）:
```sh
python3 tools/regression.py              # 默认核心测试集 (~28 文件)
python3 tools/regression.py --all        # 全部 test/ 文件
python3 tools/regression.py test/test-strings.scm ...   # 指定文件
```
- 统计口径: `[PASS]`/`[CHECK PASS]` vs `[FAIL]`/`[CHECK FAIL]`，并抓 `error:`/`RecursionError`/`NameError`/`Traceback` 行。
- 注意：不要把 `_fail` 函数名、`FAIL: 0` 汇总行、`[PASS] assert fail raises` 用例名误当失败（grep `FAIL` 会误报）。

### C#
```sh
cd minischeme
dotnet run -- test/test-compiler.scm    # 单文件（MSCM_PYB 控制 pyb；dotnet run -c Release 用 Release）
```

### 已知基线
- `test/test-compiler.scm`（C# 回归主文件）: 233 PASS 0 FAIL，含 acc-tail 80000、互递归 even-tail?/odd-tail? 100000、side-effects-in-order。
- 全部 `test/test-*.scm` + `test1/*.scm` 在两种 pyb 模式下 0 error（`cd miniscm && python3 miniscm.py test/xxx.scm` 直接传文件参数）。
- 深尾递归: acc-tail 80000 / 互递归 100000 通过（此前 `maximum recursion depth exceeded`）。

## Key Conventions

- **Python 3.11+ required** — used for AST-based JIT compilation.
- **`@_b` decorator** in primitives.py registers a Python function as a Scheme builtin. `_b(name, fn)` registers with a custom Scheme name.
- **Special forms** registered via `@put(Sym)` decorator in `miniscm.py` (`SPECIALS` dict).
- **`Cell(a, d)`** is the cons cell — car/cdr. **`NIL`** is end-of-list (not falsy).
- **`Sym(s)`** is an interned symbol. **`_sn(x)`** extracts name string, **`_so(x)`** unwraps SyntaxObject.
- **`TRUE`/`FALSE`** are singleton Syms `#t`/`#f` — use `x is TRUE` not `x is True`.
- **`VOID`** = unspecified return value (not printed). **`EOF`** = end-of-file sentinel.
- **SchemeString** vs Python `str` — Scheme string operations return `SchemeString` objects.
- **Fractions** use Python's `Fraction` from the standard library.
- **`+inf.0`, `-inf.0`, `+nan.0`** — R7RS standard numeric syntax supported by reader.
- **`NIL` / `SchemeString` / `SchemeVector` / `SchemeBytevector`** — all have `__bool__` returning `True` (Scheme semantics: only `#f` is falsy).

## Tail Call Optimization (TCO)

三层 trampoline 机制，C#/py 逐分支对齐，完全避免 Python/.NET 栈增长：

### Layer 0: `_eval` 主循环消化解释器尾调用
`miniscm.py:_eval`（~303 行起，`while True`）遇 `seq_tail_call`/`HIf`/`HCond` 返回的 `TailCall(expr, env)` 时提取并 `continue`；`LambdaProc` 分支 BindParams 后 `seq_tail_call`（`TailCall` 帧或真值）。等价 C# `Evaluator.EvalCore`（`while (true)` + `SeqTailCall`）。

### Layer 1: `__mscm_invoke__` 迭代 trampoline（JIT 调用统一入口）
`compiler.py:__mscm_invoke__`（= C# `JitRuntime.Invoke`）：
- 分支顺序: `LambdaProc`（先 `_ensure_jit_compiled`，已编译走 `_invoke_compiled`，否则 BindParams+`eval_seq` 解释执行）→ `CompiledLambda`（`_invoke_compiled`）→ callable → tuple-lambda。
- 各分支若返回 `TailCall`，先 `__mscm_try_unpack_tail_call__`（= C# `TryUnpackTailCall`）：**仅解包 JIT `MakeTailCall` 产生的 `(proc (quote v1) (quote v2) ...)`**（proc 是运行时函数对象、参数已求值），解包成功则 `(proc, args, env) = u; continue`（同一帧循环，不涨栈）。
- 解释器 AST 尾调用（proc 为 Sym/Cell/字面量）`try_unpack` 返回 None → 交回 `_eval` 主循环求值（`_eval` 内部循环消化，不重入 `__mscm_invoke__`）。
- 关键：**不能**把 JIT TailCall 的 `(proc (quote 深值)...)` 重新喂给解释器求值 — 对深列表值做 `HQuote`/`strip_syntax` 会逐层 +1 栈帧爆栈（这是原递归 `__mscm_eval_tail_call__`/`EvalTailCall` 的根因，均已删除）。

### Layer 2: 编译体自带 trampoline 循环
编译出的函数体（py `ast.While` / C# `Expression.Loop`，`while True`）内：
- **自递归**（proc 名 == 当前函数名）: 实参求值到临时变量后重绑参数 + `continue`（O(1) 栈）。
- **其它尾位置调用**（交叉调用、词法局部函数、内联 lambda 应用、不可变原语）: 一律生成 `__mscm_make_tail_call__(proc, args, env)` 值返回（= C# `MakeTailCall` + break），由外层 `__mscm_invoke__`/`_eval` 的 trampoline 循环解包。**嵌套 lambda（无名字，无法走自递归优化）必须靠此兜底**，否则尾调用生成 `__mscm_invoke__(...)` 递归帧每轮 +3 栈帧爆栈。
- 不可变原语（`_IMMUTABLE_PRIMITIVES` / C# `ImmutablePrimitives`）已内联或直调，不进 TailCall 路径。

`LambdaProc.__call__`（`compiler.py:1536`）供解释器外调用者（`map`/`_call` 等原语）使用：编译路径 `__mscm_invoke__(self, args, self.env)`；fallback `eval_seq` + `while isinstance(r, TailCall): r = eval_fn(...)` 解包，TailCall 不泄漏给 Scheme 代码。

## JIT Compiler

- **`LambdaProc`**（`compiler.py:1536`）包裹每个用户 lambda：`name/params/body/env/is_simple/call_count/compiled_version/_jit_failed`。
- **`CompiledLambda`**（`compiler.py:539`）编译产物：`py_func` + 预计算 `_n_regular`（rest 参数打包）。
- **`compile_lambda_proc`**（`compiler.py:1325`）：宏展开 body（`.mscm_cache/*.json` 缓存，CACHE_VERSION=6）→ `to_ast` → `fold_constants` → `AstExprCompiler.compile_stmt_seq` → Python AST（`while True` 包裹）→ `compile()`/`exec()`。
- **`_ensure_jit_compiled`**（`miniscm.py:276`）= C# `Evaluator.EnsureCompiled`：编译守卫 `_IS_COMPILING`（重入防护）、`.fail` 文件标记结构性编译失败（闭包/自递归/quasiquote）、`should_jit` 跳过匿名/辅助函数（`flip/complement/const/identity/check/test/t-eq`）。**编译失败静默回退解释器**，`MSCM_JIT_DEBUG=1` 看原因。
- **嵌套 lambda**（`_compile_LambdaAst`）：boxed 捕获的编译为 `LambdaProc`（解释器对象）；非闭包的内层 lambda 递归编译为 `CompiledLambda`（`nested_lambda` 函数 + `ast.While` trampoline，对齐 C# `innerLoop`）。
- **`_make_jit_globals`**（`compiler.py:702`）：编译函数 globals 统一构建点 — `__mscm_consts__`/`TRUE/FALSE/VOID`/`Env/Sym/Cell/NIL`/Scheme 类型/`_cells`/`car/cdr/cons`/`CompiledLambda/TailCall/LambdaProc`/`__mscm_invoke__`/`__mscm_make_tail_call__`/`__mscm_resolve_ic__`/`Box` 等。**缺失注册项会在编译期报 `name 'xxx' is not defined`**。
- **JIT 优化**：`_IMMUTABLE_PRIMITIVES` 冻结进 `__mscm_consts__`（消除 IC 间接）；`+ - *` → `ast.BinOp`、`< > = eq?` → `ast.Compare`、`car/cdr` → 属性访问（`/` 排除，必须返回 Fraction）；常量折叠；Selective Morphic IC（仅不可变原语缓存，set!-able 变量恒走 `env.lookup`）。

## REPL

```sh
cd miniscm && python3 miniscm.py
mscm> (+ 1 2)
3
mscm> ,quit   # or (exit)
```
- `,expand <expr>` 显示宏展开（minref.py）。`,dis`/`,json` 已删除。
- 自动加载 3 个核心库: `boot-min2.scm`/`boot-core.scm`/`boot-sugar.scm`（pyb=True）；pyb=False 追加加载 10 个扩展库。

## Common Pitfalls

- `TRUE`/`FALSE` are not Python bools — use `x is TRUE` not `x is True`.
- `NIL` is not falsy in Python — check with `x is NIL`.
- **JIT 恒开启（`USE_JIT = True`），不能设为 False** — pyb=False 时也不关（`__mscm_make_tail_call__` 依赖 JIT 编译体 trampoline；解释器深尾递归路径没有等价保护）。
- **测试必须从 `miniscm/` 目录跑**（`python3 miniscm.py test/xxx.scm`）— `load` 相对路径按 CWD 解析；从仓库根跑会 `No such file: 'scm/lang/lang-hs.scm'` 等连锁失败。`test`/`test1`/`tools` 是 `miniscm/` 下的符号链接。
- `load_file` silently catches errors (bare `except: pass` in `miniscm.py`).
- **`/` division with ints returns `Fraction`** — `(/ 1 3 2)` → `1/6`, not `0.166...`.
- **`the-environment` is a special form** (`h_the_environment`), returns the lexical env — must stay a special form, not a regular builtin, for nested quasiquote hygiene.
- **Macro expansion** goes through `_expand_macro`（`primitives_first.py`，C# `ExpandMacro` 等价）. Only `("macro", pattern, body, env, true)` tuples are expanded; `sx-expand-call` bridge exposes one-step expansion to Scheme.
- **`sx-def-env`/`sx-expand-env`** return dynamic macro-definition/call-site envs during expansion (module-level `_CURRENT_MACRO_DEF_ENV`/`_CURRENT_EXPAND_ENV`), falling back to global `be`.
- **`string-copy` returns `SchemeString`** (mutable), not `str`. `string-set!` requires a `SchemeString` with `.data` attribute.
- No lint/typecheck tooling exists for this project.
- **DSL test files** (`test-lang-*.scm`) define their own macros. Macro self-reference (e.g. `define-macro` expanding to the same macro name) causes infinite expansion. Fixed via forwarding functions like `lang-map`.
- **`_eval` overflow limits**: tail recursion works at 100k+ via trampolines. Lists/strings/vectors handle 50k+ elements.
- **Stale cache after rename**: renaming internal globals (e.g. `_consts` → `__mscm_consts__`) invalidates all existing `.msc`/`.json` caches. Clear `.mscm_cache/` and re-run.
- **`compile_lambda_proc` silently returns `None` on failure** — fallback interpreter path is used. Enable `MSCM_JIT_DEBUG=1` to see compilation errors. Common cause: missing JIT globals like `_cells`, `_cell_len`, `_vec_set_elem`.
- **C# 侧已提交基线**: `JitRuntime.Invoke`（迭代 trampoline + `TryUnpackTailCall`）、`Evaluator.EnsureCompiled`、`Compiler.cs` 编译体 `Expression.Loop` — 修改 C# 后需同步 py 对应分支，反之亦然（两实现须逐分支对齐）。
