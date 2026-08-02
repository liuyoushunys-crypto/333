# miniscm — Agent Guide

**Zero-dependency Scheme interpreter in pure Python 3.** No Makefile, no package manager, no test framework.

## Entrypoints

- **Interpreter**: `python3 miniscm.py [file.scm ...]` — batch evaluates file(s). No args starts REPL (`mscm>`).
- **Python API**: `from miniscm import load_file; load_file("path.scm")` — load Scheme into the global env.
- **Dual mode**: `pyb = True` (Python builtins from `primitives_ext.py`) / `pyb = False` (pure Scheme from `scm/*.scm`). Same test suite passes both.

## Architecture

```
mtypes.py      (753 lines) — Sym, Cell, Env, NIL, VOID, EOF, SyntaxObject, TailCall, primitive types + helpers
reader.py      (494 lines) — tokenizer + recursive-descent parser → Cell/Sym/vector/inf/nan literals
miniscm.py     (748 lines) — eval loop (trampoline), special forms, macro dispatch (ExpandMacro), bridge primitives
compiler.py    (1322 lines) — JIT AST compiler, LambdaProc wrapper, bytecode cache (serialize/deserialize)
primitives.py  (1163 lines) — all built-in procedures (~300), registered via `initenv()` into global Env `be`
primitives_ext.py — extension builtins for pyb=True mode
scm/           — Scheme bootstrap library (15 files): boot-core, boot-sugar, srfi-*, generators, fill-gaps, etc.
.mscm_cache/   — JIT bytecode cache directory
```

宏系统与 minischeme (C#) 完全对齐: Python 端无宏引擎 (macro.py 已删除),
define-macro/define-syntax/quasiquote/syntax-rules 全部由 Scheme 端 (my-definemacro2.scm +
boot-min2.scm) 自举实现。Python 仅保留桥接原语 (sx-defmacro/sx-expand-call/sx-def-env/
sx-expand-env/the-environment), 宏展开通过 `_expand_macro` (等价 C# Evaluator.ExpandMacro)
处理 ("macro", pattern, body, env, true) 元组。

`primitives.py` is imported at eval-time (not module level). The global env `be` from `mtypes.py` is shared across all modules.

## Running Tests

No test runner. All tests are `.scm` files with inline assertions:

```sh
python3 miniscm.py test/test2.scm          # single file
python3 miniscm.py test/test-install-core.scm
```

回归测试 (pyb=True 模式, 覆盖核心功能并汇总 PASS/FAIL):

```sh
python3 tools/regression.py              # 默认核心测试集 (11 个文件)
python3 tools/regression.py --all        # 全部 test/ 文件
python3 tools/regression.py test/test-strings.scm ...   # 指定文件
```

脚本统计 `[PASS]`/`[CHECK PASS]` 与 `[FAIL]`/`[CHECK FAIL]`, 退出码 0=通过,
非 0=有超出已知失败数的回归。已知失败 (`tools/regression.py` 中 `KNOWN_FAILS`):
test-arithmetic 3 个 (fixnum 宽度), test-strings 1 个 (digit-value), test-language 16 个 (DSL typeof 差异)。

```sh
python3 miniscm.py test/test2.scm          # single file
python3 miniscm.py test/test-install-core.scm
```

Tests print `[PASS]`/`[FAIL]` lines. There are **222 test files** in `test/`. All pass with **0 FAIL** in both `pyb=True` and `pyb=False` modes.

Key test files:
- `test/test-ext-accuracy.scm` — 632 tests, comprehensive accuracy, same file passes both modes
- `test/test-full.scm` — comprehensive stress test
- `test/test-install-core.scm` (~476 tests) — core library coverage
- `test/test3.scm` — advanced feature tests (guard, values, string ops, etc.)
- `test/test2.scm` — macro system + edge cases
- `test/test-macros.scm` — exhaustive macro branch coverage
- `test/test_all_builtins.scm` — builtin primitive coverage
- `test/test-scm-all.scm` — core language equivalence
- `test/srfi-tests.scm` — SRFI-1/13/14 coverage
- `test/test-lang-*.scm` — DSL language demos (C, Rust, Python, JS, etc.)
- `test/test-edge-cases.scm` — edge/boundary tests
- `test/test-tail-recursion.scm` — 34 deep tail recursion tests (all pass instant)
- `test/test-morphic-ic.scm` — Selective Morphic IC correctness tests

## Key Conventions

- **Python 3.11+ required** — used for AST-based JIT compilation.
- **`@_b` decorator** in primitives.py registers a Python function as a Scheme builtin.
- **`_b(name, fn)`** alternative form to register with a custom Scheme name.
- **Special forms** registered via `@_put(Sym)` decorator in `miniscm.py`.
- **`Cell(a, d)`** is the cons cell — car/cdr. **`NIL`** is end-of-list (not falsy).
- **`Sym(s)`** is an interned symbol. **`_sn(x)`** extracts name string, **`_so(x)`** unwraps SyntaxObject.
- **`TRUE`/`FALSE`** are singleton Syms `#t`/`#f` — do not use Python `True`/`False` for Scheme values.
- **`VOID`** = unspecified return value (not printed). **`EOF`** = end-of-file sentinel.
- **SchemeString** vs Python `str` — Scheme string operations return `SchemeString` objects.
- **Fractions** use Python's `Fraction` from the standard library.
- **`+inf.0`, `-inf.0`, `+nan.0`** — R7RS standard numeric syntax supported by reader.
- **`NIL` / `SchemeString` / `SchemeVector` / `SchemeBytevector`** — all have `__bool__` returning `True` (Scheme semantics: only `#f` is falsy).

## Tail Call Optimization (TCO)

TCO is implemented via a two-layer mechanism that avoids Python stack growth:

### Layer 1: `_eval` inlines `LambdaProc` body
In `miniscm.py:_eval` (lines ~1040-1066), when `proc_val` is a `LambdaProc`, the eval loop directly binds arguments and evaluates the body via `_seq_tail_call`, keeping the result inside `eval`'s `while True` loop. This entirely avoids Python recursion through `LambdaProc.__call__`. If `compiled_version` exists, calls `cv.py_func(...)` directly, bypassing `LambdaProc.__call__` overhead.

### Layer 2: compiled code returns `TailCall`
The JIT compiler (`compile_lambda`) generates Python functions that:
- For self-recursion: use `continue` (loop back within the compiled function, O(1) stack)
- For cross-function calls (mutual recursion): use `return __mscm_make_tail_call__(...)` which creates a `TailCall(expr, env)` frame; only for user-defined LambdaProc in `be`, never for builtins or lexical variables.

When `_eval` receives a `TailCall`, it extracts `expr` and `env` and `continue`s the main loop, re-entering the dispatch without growing the Python stack.

### `LambdaProc.__call__` resolves TailCall internally
For callers outside `_eval` (like `map_`, `_call`, `_cvw` in `primitives.py`), `LambdaProc.__call__` uses `_eval_seq` and unwraps any `TailCall` itself via `while isinstance(r, TailCall): r = _eval_fn(r.expr, r.env)`, so TailCall never leaks to Scheme code.

## JIT Compiler

- **`LambdaProc`** (`compiler.py:1096`) wraps each user-defined lambda. Tracks `call_count` and stores optional `compiled_version`.
- **Every named user function is compiled on first call.**
- **`compile_lambda`** (`compiler.py:882`): macro-expands body, builds Python AST with `while True` loop for self-recursion TCO, generates `__mscm_make_tail_call__(...)` for cross-function tail calls.
- **Cache**: compiled bytecode is serialized via `pickle` + `marshal` to `.mscm_cache/*.msc` files. `save_cache` / `load_cache` handle serialization with `serialize_val` / `deserialize_val`.
- **`_should_jit`** skips anonymous/hygiene-generated lambdas and helper functions like `flip`, `complement`, `const`.
- **`_has_nested_closure`** detects closures through intermediate lambda scopes, preventing incorrect JIT compilation of named-let patterns.

### JIT Optimizations

- **`_IMMUTABLE_PRIMITIVES`**: ~40 standard primitives (`car`, `cdr`, `null?`, `pair?`, `cons`, `+`, `*`, etc.) are frozen into `__mscm_consts__` at compile time, eliminating IC indirection per call.
- **Compiled LambdaProc direct dispatch**: `_eval`'s main path and tight loop detect `compiled_version` and call `py_func` directly, bypassing `LambdaProc.__call__` overhead.
- **AST-level inlining**: `+`, `-`, `*` compile to `ast.BinOp`; `<`, `>`, `=`, `eq?` compile to `ast.Compare`; `car`/`cdr` compile to Python attribute access; `null?`/`pair?`/`not` compile to `ast.Is` comparisons. `/` is excluded (must return `Fraction` for exact arithmetic).
- **Constant folding**: compile-time evaluation of `not`, `null?`, `pair?`, `car`/`cdr` on literal args; arithmetic op folding for `+`, `-`, `*`, `/`, `<`, `>`, `=`, `<=`, `>=`.
- **Self-recursion TCO**: compiled `while True` loop with `continue` for self-tail-calls; `__mscm_make_tail_call__(...)` for cross-function mutual recursion.
- **Selective Morphic IC**: only `_IMMUTABLE_PRIMITIVES` are cached; user variables (`set!`-able) always use `env.lookup` for correctness.
- **Builtin guard**: cross-function tail calls only apply to user-defined `LambdaProc` targets; builtins (Python functions) use `__mscm_invoke__` instead of `__mscm_make_tail_call__`.
- **Rest-param support**: non-simple (rest-param) compiled lambdas go through `CompiledLambda.__call__` which handles rest arg wrapping, not inline `py_func` dispatch.

## REPL

```sh
python3 miniscm.py
mscm> (+ 1 2)
3
mscm> ,quit   # or (exit)
```

Auto-loads the 4 core library files on startup, matching minischeme (`Program.cs`):
`my-definemacro2.scm`, `boot-min2.scm`, `boot-core.scm`, `boot-sugar.scm`.
The macro system (define-macro/define-syntax/syntax-rules/quasiquote) is fully
self-hosted in Scheme — Python has no macro engine.

## Common Pitfalls

- `TRUE`/`FALSE` are not Python bools — use `x is TRUE` not `x is True`.
- `NIL` is not falsy in Python — check with `x is NIL`.
- `primitives.py` uses `_b` which calls `be.define()` — the global env must already exist.
- `load_file` silently catches errors (bare `except: pass` in `miniscm.py`).
- **`/` division with ints returns `Fraction`** — `(/ 1 3 2)` → `1/6`, not `0.166...`.
- **`the-environment` is a special form** (`h_the_environment`), returns the lexical env — must stay a special form, not a regular builtin, for nested quasiquote hygiene.
- **Macro expansion** goes through `_expand_macro` (C# `ExpandMacro` equivalent). Only `("macro", pattern, body, env, true)` tuples are expanded; `sx-expand-call` bridge exposes one-step expansion to Scheme.
- **`sx-def-env`/`sx-expand-env`** return dynamic macro-definition/call-site envs during expansion (module-level `_CURRENT_MACRO_DEF_ENV`/`_CURRENT_EXPAND_ENV`), falling back to global `be`.
- **`string-copy` returns `SchemeString`** (mutable), not `str`. `string-set!` requires a `SchemeString` with `.data` attribute.
- **No lint/typecheck tooling** exists for this project.
- **DSL test files** (`test-lang-*.scm`) define their own macros. Macro self-reference (e.g. `define-macro` expanding to the same macro name) causes infinite expansion. Fixed via forwarding functions like `lang-map`.
- **`_eval` overflow limits**: Tail recursion works at 100k+ via `_eval` inline + `TailCall` trampoline. Named `let`/`do`/`guard` loops are deeply trampolined. Lists/strings/vectors handle 50k+ elements.
- **Stale cache after rename**: renaming internal globals (e.g. `_consts` → `__mscm_consts__`) invalidates all existing `.msc` files. Clear `.mscm_cache/` and re-run.
- **`compile_lambda` silently returns `None` on failure** — if a user-defined function fails to compile, the fallback interpreter path is used. Enable `MSCM_JIT_DEBUG=1` to see compilation errors. Common cause: missing JIT globals like `_cells`, `_cell_len`, `_vec_set_elem`. JIT is currently disabled (`compiler.USE_JIT = False`).
