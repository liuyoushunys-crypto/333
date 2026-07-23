using Miniscm.Eval;
using Miniscm.Types;
using Void = Miniscm.Types.Void;

namespace Miniscm.Compiler;

public static class JitRuntime
{
    public static readonly HashSet<string> ImmutablePrimitives = new()
    {
        "car", "cdr", "cons", "map", "apply", "list", "append", "reverse", "length",
        "boolean?", "procedure?", "symbol?", "number?", "string?", "vector?",
        "char?", "bytevector?", "eof-object?", "assq", "assoc", "memq", "member",
        "caar", "cadr", "cdar", "cddr", "caaar", "caadr", "cadar", "caddr",
        "cdaar", "cdadr", "cddar", "cdddr", "string-append", "vector-ref",
        "vector-set!", "string-ref", "string-set!", "set-car!", "set-cdr!",
        "display", "write", "newline"
    };

    public static object? Invoke(object? procVal, object?[] argsVal, Env env)
    {
        if (procVal is LambdaProc lp)
        {
            if (lp.CompiledVersion is CompiledLambda cl)
                return cl.Invoke(lp.ClosureEnv, argsVal);
            var nenv = new Env(lp.ClosureEnv);
            Evaluator.BindParams(lp.Params, [.. argsVal], nenv);
            var r = Evaluator.SeqTailCall(lp.Body, nenv);
            while (r is TailCall tcr) r = Evaluator.EvalCore(tcr.Expr, tcr.Env);
            return r;
        }
        if (procVal is CompiledLambda cv)
            return cv.Invoke(cv.Env, argsVal);
        if (procVal is Func<object?[], object?> fn)
            return fn(argsVal);
        if (procVal is Delegate d)
            return d.DynamicInvoke(argsVal);
        if (procVal is System.Runtime.CompilerServices.ITuple it && it.Length >= 2 && it[0] is string t0)
        {
            if (t0 == "lambda" && it.Length >= 5 && it[1] is List<string> lamParams && it[3] is Env le)
            {
                var nenv = new Env(le);
                Evaluator.BindParams(lamParams, [.. argsVal], nenv);
                var r = Evaluator.SeqTailCall(it[2], nenv);
                while (r is TailCall tcr) r = Evaluator.EvalCore(tcr.Expr, tcr.Env);
                return r;
            }
        }
        throw new Exception($"not callable: {Printer.Format(procVal)}");
    }

    public static object? EnvSetVar(Env env, string name, object? val)
    {
        var cur = env;
        while (cur is not null)
        {
            if (cur.Data.ContainsKey(name)) { cur.Data[name] = val; return Const.VOID; }
            cur = cur.Parent;
        }
        env.Data[name] = val;
        return Const.VOID;
    }

    public static TailCall MakeTailCall(object? proc, object?[] argsList, Env env)
    {
        object? argCells = Const.NIL;
        for (int i = argsList.Length - 1; i >= 0; i--)
            argCells = new Cell(new Cell(Sym.QUOTE, new Cell(argsList[i], Const.NIL)), argCells);
        var expr = new Cell(proc, argCells);
        return new TailCall(expr, env);
    }

    public static object? ResolveIC(object?[] cacheCell, Env env, string sym)
    {
        var val = cacheCell[0];
        if (val is null)
        {
            val = env.Lookup(sym);
            cacheCell[0] = val;
        }
        return val;
    }

    public static object? BoolToScheme(bool b) => b ? Const.TRUE : Const.FALSE;

    public static bool IsFalsy(object? v) => v is Sym s && s == Const.FALSE;

    public static object? CarOf(object? x) => x is Cell c ? c.Car : throw new Exception("car: not a pair");
    public static object? CdrOf(object? x) => x is Cell c ? c.Cdr : throw new Exception("cdr: not a pair");

    public static LambdaProc MakeLambda(List<string> @params, bool isSimple, Env env, object? body)
    {
        return new LambdaProc(@params, body, env, isSimple);
    }
}
