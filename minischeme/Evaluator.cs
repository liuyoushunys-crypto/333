using Miniscm.Types;
using Miniscm.Reader;
using Miniscm.Compiler;
using Void = Miniscm.Types.Void;

namespace Miniscm.Eval;

public static class Evaluator
{
    public static readonly Env GlobalEnv = new();
    public static readonly Dictionary<Sym, Func<object?, Env, object?>> Specials = [];
    private static readonly object? UnboundSentinel = new();

    // JIT compilation guard (prevents reentrant compilation)
    internal static bool IsCompiling = false;

    // JIT-compile a named LambdaProc if not already compiled. Called from both
    // EvalCore and JitRuntime.Invoke so a lambda is JITted no matter which
    // path calls it; otherwise a JIT function mutually tail-calling an
    // uncompiled one recurses through EvalCore<->Invoke (+2 frames/round).
    internal static void EnsureCompiled(LambdaProc lp)
    {
        if (IsCompiling || lp.CompiledVersion is not null || lp.Name is null) return;
        IsCompiling = true;
        try
        {
            var compiled = Miniscm.Compiler.Compiler.CompileLambdaProc(lp);
            if (compiled is not null)
                lp.CompiledVersion = compiled;
        }
        catch
        {
        }
        finally
        {
            IsCompiling = false;
        }
    }

    // Current macro's definition environment, used by the Scheme sx-expand
    // hygiene resolution (free template identifiers resolve at definition time).
    internal static Env? CurrentMacroDefEnv;

    // Env at the macro call site, used by boot-min.scm sx-eval-tmpl to eval
    // templates so pattern-substituted local symbols resolve correctly.
    internal static Env? CurrentExpandEnv;

    private static void Put(Sym s, Func<object?, Env, object?> f) => Specials[s] = f;

    public static void InitSpecials()
    {
        Put(Sym.QUOTE, HQuote);
        Put(Sym.IF, HIf);
        Put(Sym.LAMBDA, HLambda);
        Put(Sym.BEGIN, HBegin);
        Put(Sym.DEFINE, HDefine);
        Put(Sym.SETBANG, HSet);
        Put(Sym.SETF, HSetf);
        // 注: quasiquote 等已迁移到 Scheme (boot-min.scm)。
        //     the-environment 是 C# 暴露给 Scheme 的桥接接口:
        //     Scheme 端用 (eval expr (the-environment)) 在调用方词法环境求值
        //     unquote, 使 quasiquote/define-syntax 等可在 Scheme 自举实现。
        Put(Sym.THE_ENVIRONMENT, HTheEnvironment);
        // Put(Sym.UNQUOTE, HUnquote);
        // Put(Sym.UNSPLICE, HUnquote);
        // Put(Sym.USYNTAX, HUnquote);
        // 微解释器: and/or/cond/let/let*/letrec 特殊形式已移除,
        // 由 boot-core.scm 的 Scheme 宏 (define-syntax) 接管。
    }

    // ── Special Forms ──

    // the-environment: 返回当前词法环境对象 (供 (eval expr env) 使用)
    private static object? HTheEnvironment(object? args, Env env) => env;

    private static object? HQuote(object? args, Env env)
    {
        var v = args is Cell c ? c.Car : Const.NIL;
        return StripSyntax(v);
    }

    private static object? HUnquote(object? args, Env env) =>
        throw new Exception("unquote outside quasiquote");

    private static object? HIf(object? args, Env env)
    {
        if (args is not Cell a) return Const.VOID;
        var test = Eval(a.Car, env);
        if (test is Sym t && t == Const.FALSE)
        {
            if (a.Cdr is Cell thenElse && thenElse.Cdr is Cell elseClause)
                return new TailCall(elseClause.Car, env);
            return Const.VOID;
        }
        return a.Cdr is Cell thenClause ? new TailCall(thenClause.Car, env) : Const.VOID;
    }

    private static object? HLambda(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad lambda form");
        var @params = new List<string>();
        var cur = a.Car;
        bool hasRest = false;
        while (cur is Cell pc) { @params.Add(pc.Car.AsString()); cur = pc.Cdr; }
        if (cur is not Nil) { @params.Add("rest:" + (cur as Sym)?.Name ?? cur?.ToString() ?? ""); hasRest = true; }
        return new LambdaProc(@params, a.Cdr, env, !hasRest);
    }

    private static object? HBegin(object? args, Env env) => SeqTailCall(args, env);

    private static object? HDefine(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad define form");
        if (a.Car is Cell pat)
        {
            var name = pat.Car.AsString();
            var @params = new List<string>();
            var cur = pat.Cdr;
            bool hasRest = false;
            while (cur is Cell pc) { @params.Add(pc.Car.AsString()); cur = pc.Cdr; }
            if (cur is not Nil) { @params.Add("rest:" + (cur as Sym)?.Name ?? cur?.ToString() ?? ""); hasRest = true; }
            env.Data[name] = new LambdaProc(@params, a.Cdr, env, !hasRest, name);
            return Sym.Intern(name);
        }
        var name2 = a.Car.AsString();
        if (a.Cdr is Cell valCell)
        {
            var val = Eval(valCell.Car, env);
            if (val is LambdaProc lp && lp.Name is null) lp.Name = name2;
            env.Data[name2] = val;
        }
        return Sym.Intern(name2);
    }

    private static object? HSet(object? args, Env env)
    {
        if (args is not Cell a) return Const.VOID;
        if (a.Car is Sym s)
        {
            var v = Eval(a.Cdr is Cell c ? c.Car : Const.NIL, env);
            var e = env;
            while (e is not null)
            {
                if (e.Data.ContainsKey(s.Name)) { e.Data[s.Name] = v; return Const.VOID; }
                e = e.Parent;
            }
            env.Data[s.Name] = v;
            return Const.VOID;
        }
        return new TailCall(new Cell(Sym.SETF, new Cell(a.Car, new Cell(a.Cdr is Cell c2 ? c2.Car : Const.NIL, Const.NIL))), env);
    }

    private static object? HSetf(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad set!-form");
        var place = a.Car;
        var val = a.Cdr is Cell c ? c.Car : Const.NIL;
        if (place is Cell pc && pc.Car is Sym ps)
        {
            if (ps.Name is "car" or "cdr")
            {
                var setter = Sym.Intern($"set-{ps.Name}!");
                return new TailCall(new Cell(setter, new Cell(pc.Cdr is Cell cc ? cc.Car : Const.NIL, new Cell(val, Const.NIL))), env);
            }
        }
        throw new Exception($"set!: invalid place: {place}");
    }

    // ── Helpers ──

    private static object? StripSyntax(object? v)
    {
        while (v is SyntaxObject so) v = so.Expr;
        if (v is Cell c) return new Cell(StripSyntax(c.Car), StripSyntax(c.Cdr));
        return v;
    }

    public static object? SeqTailCall(object? seq, Env env)
    {
        if (seq is Nil) return Const.VOID;
        var cur = seq;
        while (cur is Cell c && c.Cdr is Cell)
        {
            Eval(c.Car, env);
            cur = c.Cdr;
        }
        if (cur is Cell last) return new TailCall(last.Car, env);
        return Eval(cur, env);
    }

    public static object? Eval(object? expr, Env env) => EvalCore(expr, env);

    internal static object? EvalCore(object? expr, Env env)
    {
        while (true)
        {
            // Symbol
            if (expr is Sym sym)
            {
                if (sym == Const.TRUE || sym == Const.FALSE) return expr;
                return env.Lookup(sym);
            }

            // Non-list
            if (expr is not Cell cell)
            {
                if (expr is Nil or Void or Eof) return expr;
                if (expr is SyntaxObject so) return so.Expr;
                return expr;
            }

            // Special forms
            var op = cell.Car;
            var args = cell.Cdr;

            var handler = op is Sym osp ? Specials.GetValueOrDefault(osp) : null;
            if (handler is not null)
            {
                var r = handler(args, env);
                if (r is TailCall tc) { expr = tc.Expr; env = tc.Env; continue; }
                return r;
            }

            // Macro expansion
            object? proc;
            if (op is Sym ops)
            {
                proc = env.LookupSilent(ops.Name, UnboundSentinel);
                if (!ReferenceEquals(proc, UnboundSentinel))
                {
                    var newExpr = ExpandMacro(proc, args, env);
                    if (newExpr is not null) { expr = newExpr; continue; }
                }
                else
                    proc = EvalCore(op, env);
            }
            else
                proc = EvalCore(op, env);

                var curArgs = args;

                // LambdaProc
                if (proc is LambdaProc lp)
            {
                EnsureCompiled(lp);

                if (lp.CompiledVersion is CompiledLambda cv)
                {
                    var argsArr = EvalArgsToArray(curArgs, env);
                    // JitRuntime.Invoke 是迭代 trampoline：JIT 尾调用返回的 TailCall
                    // 在循环内解包，不会把 MakeTailCall 的 (proc (quote v) ...) 表达式
                    // 重新喂给解释器（否则对深列表值做 HQuote/StripSyntax 爆栈）。
                    return Miniscm.Compiler.JitRuntime.Invoke(cv, argsArr, cv.Env);
                }

                var nenv = new Env(lp.ClosureEnv, lp.Params.Count);
                BindParams(lp.Params, EvalArgsToArray(curArgs, env), nenv);
                var r3 = SeqTailCall(lp.Body, nenv);
                if (r3 is TailCall tc3) { expr = tc3.Expr; env = tc3.Env; continue; }
                return r3;
            }

            // CompiledLambda 直接调用（闭包 box 捕获的值可能是已编译 lambda）。
            // JitRuntime.Invoke 是迭代 trampoline：JIT 尾调用返回的 TailCall 在
            // 循环内解包，不会递归 EvalCore（否则深尾递归逐层 +1 栈帧爆栈）。
            if (proc is CompiledLambda cl)
            {
                var argsArr = EvalArgsToArray(curArgs, env);
                return Miniscm.Compiler.JitRuntime.Invoke(cl, argsArr, cl.Env);
            }

            var evaledArgs = EvalArgsToArray(curArgs, env);

            // Primitive functions (Func<object?[], object?>)
            if (proc is Func<object?[], object?> fn)
            {
                var r3 = fn(evaledArgs);
                if (r3 is TailCall tc3) { expr = tc3.Expr; env = tc3.Env; continue; }
                return r3;
            }

            // Other delegate
            if (proc is Delegate d)
            {
                var r3 = d.DynamicInvoke(evaledArgs);
                if (r3 is TailCall tc3) { expr = tc3.Expr; env = tc3.Env; continue; }
                return r3;
            }

            // Tuple proc (macro, lambda)
            if (proc is System.Runtime.CompilerServices.ITuple it && it.Length >= 2 && it[0] is string t0)
            {
                if (t0 == "lambda")
                {
                    if (it.Length >= 5 && it[1] is List<string> lamParams && it[3] is Env le)
                    {
                        var nenv2 = new Env(le);
                        BindParams(lamParams, evaledArgs, nenv2);
                        var r4 = SeqTailCall(it[2], nenv2);
                        if (r4 is TailCall tc4) { expr = tc4.Expr; env = tc4.Env; continue; }
                        return r4;
                    }
                }
            }

            throw new Exception($"not callable: {Printer.Format(proc)}");
        }
    }

    // 展开 "macro" 元组: 绑定模式变量 (rest 符号 args) 后求值宏体。
    // 宏体为 (sx-macro-expand ...), 真正的模式解构与宏体求值在 Scheme 端完成。
    // 第一优先级: 原生 syntax-rules 编译器 (NativeSyntax) — 展开时零解释器。
    // 失败自动回退 Scheme 引擎。
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<object, object?> MacroCompileCache = new();

    static object? UnwrapSyntax(object? e)
    {
        while (e is SyntaxObject so) e = so.Expr;
        return e;
    }

    // 从宏元组提取 (lits, rules)。结构必须是 sx-make-macro-binding 生成的
    // ((sx-macro-expand 'args '((sx-dispatch args 'lits 'rules))) args (sx-expand-env))。
    static (object? Lits, object? Rules)? ExtractSyntaxRules(object? proc)
    {
        try
        {
            if (proc is not System.Runtime.CompilerServices.ITuple it || it.Length < 5) return null;
            var mbody = UnwrapSyntax(it[2]);
            if (mbody is not Cell mb || UnwrapSyntax(mb.Car) is not Cell form) return null;
            if (UnwrapSyntax(form.Car) is not Sym formSym || formSym.Name != "sx-macro-expand") return null;
            if (form.Cdr is not Cell f1 || f1.Cdr is not Cell f2) return null;
            if (f2.Car is not Cell bodyListCell) return null;
            var bodyList = UnwrapSyntax(bodyListCell.Cdr);
            if (bodyList is not Cell bl || UnwrapSyntax(bl.Car) is not Cell blc || UnwrapSyntax(blc.Car) is not Cell dispatch) return null;
            if (UnwrapSyntax(dispatch.Car) is not Sym ds || ds.Name != "sx-dispatch") return null;
            if (dispatch.Cdr is not Cell d1 || d1.Cdr is not Cell d2) return null;
            // dispatch = (sx-dispatch args (quote lits) (quote rules))
            // d1 = (args (quote lits) (quote rules)); d1.Cdr = ((quote lits) (quote rules))
            // d2 = ((quote lits) (quote rules)); lits = d2.Car.Cdr.Car
            var lits = d2.Car is Cell lc && UnwrapSyntax(lc.Car) is Sym lq && lq.Name == "quote" && lc.Cdr is Cell ld2 ? ld2.Car : null;
            if (d2.Cdr is not Cell d3) return null;
            // d3 = ((quote rules)); rules = d3.Car.Cdr.Car
            var rules = d3.Car is Cell rc3 && UnwrapSyntax(rc3.Car) is Sym rq && rq.Name == "quote" && rc3.Cdr is Cell rd3 ? rd3.Car : null;
            if (lits is not null && rules is not null)
                return (lits, rules);
            return null;
        }
        catch
        {
            return null;
        }
    }

    static Func<object?, object?>? CompileMacroNative(object? proc)
    {
        try
        {
            if (proc is not System.Runtime.CompilerServices.ITuple it || it.Length < 5) return null;
            if (it[3] is not Env defEnv) return null;
            var sr = ExtractSyntaxRules(proc);
            if (sr is null) return null;
            return Miniscm.Compiler.NativeSyntax.CompileSyntaxRules(sr.Value.Lits, sr.Value.Rules, defEnv);
        }
        catch
        {
            return null;
        }
    }

    internal static object? ExpandMacro(object? proc, object? args, Env env)
    {
        if (proc is not System.Runtime.CompilerServices.ITuple it
            || it.Length < 5 || it[0] is not string p0 || p0 != "macro")
            return null;
        if (it[3] is not Env defEnv)
            return null;

        // 原生 syntax-rules 编译器缓存路径 (与 miniscm expand_macro 的 __native_syntax__ 等价)
        try
        {
            if (!MacroCompileCache.TryGetValue(proc, out var nativeCached))
            {
                nativeCached = CompileMacroNative(proc);
                MacroCompileCache[proc] = nativeCached;
            }
            if (nativeCached is Func<object?, object?> nativeFn)
            {
                var nativeResult = nativeFn(args ?? Const.NIL);
                return ResolveHygieneMarkers(nativeResult, defEnv);
            }
        }
        catch
        {
            // 原生编译/展开失败 → 回退 Scheme 引擎
        }

        var mbody = it[2];
        var nenv = new Env(env);
        // 微解释器所有宏元组模式均为 rest 符号 (my-definemacro 注册的 'args),
        // 仅需绑定 args = 全部实参。真正的模式解构在 Scheme 端 sx-macro-expand。
        if (it[1] is Sym patSym)
            nenv.Data[patSym.Name] = args ?? Const.NIL;

        var savedDefEnv = CurrentMacroDefEnv;
        CurrentMacroDefEnv = defEnv;
        CurrentExpandEnv = env;
        object? r;
        try
        {
            r = EvalSeq(mbody, nenv);
            while (r is TailCall tcr) r = EvalCore(tcr.Expr, tcr.Env);
        }
        finally
        {
            // CurrentExpandEnv 不在此恢复: 宏展开结果 (如 my-definemacro 调用)
            // 在展开后求值, 需通过 (sx-expand-env) 读到宏定义点词法环境。
            CurrentMacroDefEnv = savedDefEnv;
        }

        var result = (r as SyntaxObject)?.Expr ?? r;
        // Hygiene: resolve (sx-hygiene name) markers emitted by sx-expand
        // for free template identifiers. Only these marked identifiers are
        // resolved in the macro's definition env.
        result = ResolveHygieneMarkers(result, defEnv);
        return result;
    }

    public static object? EvalSeq(object? seq, Env env)
    {
        object? r = Const.VOID;
        var cur = seq;
        while (cur is Cell c) { r = EvalCore(c.Car, env); cur = c.Cdr; }
        return r;
    }

    // Resolve (sx-hygiene name) markers in a macro expansion. The marker names a
    // free template identifier that must resolve in the macro's definition env.
    // Data values are inlined as quoted literals; procedures/macros are left as
    // callable names. Non-marked sub-expressions are returned unchanged.
    internal static object? ResolveHygieneMarkers(object? expr, Env defEnv)
    {
        while (expr is SyntaxObject so) expr = so.Expr;
        if (expr is Cell c)
        {
            if (c.Car is Sym s && s.Name == "sx-hygiene")
            {
                var name = c.Cdr is Cell arg && arg.Cdr is Nil && arg.Car is Sym nameSym
                    ? nameSym.Name
                    : null;
                if (name is not null && defEnv.Data.TryGetValue(name, out var v))
                {
                    // Inline data values; leave procedures/macros as names.
                    if (v is System.Runtime.CompilerServices.ITuple it && it.Length >= 2 && it[0] is string t0 && t0 == "macro")
                        return c.Cdr is Cell cc ? cc.Car : c;
                    if (v is Delegate or LambdaProc or CompiledLambda or Func<object?[], object?>)
                        return c.Cdr is Cell cc2 ? cc2.Car : c;
                    return new Cell(Sym.QUOTE, new Cell(v, Const.NIL));
                }
                return c.Cdr is Cell ccc ? ccc.Car : c;
            }
            var newCar = ResolveHygieneMarkers(c.Car, defEnv);
            var newCdr = ResolveHygieneMarkers(c.Cdr, defEnv);
            if (ReferenceEquals(newCar, c.Car) && ReferenceEquals(newCdr, c.Cdr))
                return c;
            return new Cell(newCar, newCdr);
        }
        return expr;
    }

    public static object?[] EvalArgsToArray(object? args, Env env)
    {
        int cnt = 0;
        var cur = args;
        while (cur is Cell) { cnt++; cur = ((Cell)cur).Cdr; }
        var arr = new object?[cnt];
        cur = args;
        for (int i = 0; i < cnt; i++)
        {
            if (cur is not Cell c) break;
            arr[i] = EvalCore(c.Car, env);
            cur = c.Cdr;
        }
        return arr;
    }

    public static void BindParams(List<string> @params, object?[] evaledArgs, Env nenv)
    {
        int pi = 0;
        for (int i = 0; i < @params.Count; i++)
        {
            var p = @params[i];
            if (p.StartsWith("rest:"))
            {
                int len = evaledArgs.Length - pi;
                object? rest = Const.NIL;
                for (int j = evaledArgs.Length - 1; j >= pi; j--)
                    rest = new Cell(evaledArgs[j], rest);
                nenv.Data[p[5..]] = rest;
                break;
            }
            if (pi < evaledArgs.Length) nenv.Data[p] = evaledArgs[pi];
            pi++;
        }
    }
}
