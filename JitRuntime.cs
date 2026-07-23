using System.Linq;
using System.Numerics;
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
        if (procVal is Func<object?[], object?> fn)
            return fn(argsVal);
        if (procVal is CompiledLambda cv)
            return cv.Invoke(cv.Env, argsVal);
        if (procVal is LambdaProc lp)
        {
            if (lp.CompiledVersion is CompiledLambda cl)
                return cl.Invoke(lp.ClosureEnv, argsVal);
            var nenv = new Env(lp.ClosureEnv, lp.Params.Count);
            Evaluator.BindParams(lp.Params, argsVal, nenv);
            var r = Evaluator.SeqTailCall(lp.Body, nenv);
            while (r is TailCall tcr) r = Evaluator.EvalCore(tcr.Expr, tcr.Env);
            return r;
        }
        if (procVal is Delegate d)
            return d.DynamicInvoke(argsVal);
        if (procVal is System.Runtime.CompilerServices.ITuple it && it.Length >= 2 && it[0] is string t0)
        {
            if (t0 == "lambda" && it.Length >= 5 && it[1] is List<string> lamParams && it[3] is Env le)
            {
                var nenv = new Env(le, lamParams.Count);
                Evaluator.BindParams(lamParams, argsVal, nenv);
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

    public static object? Append2(object? a, object? b)
    {
        if (a is Nil) return b;
        if (a is not Cell ca) return b;
        var head = new Cell(ca.Car, Const.NIL);
        var tail = head;
        var cur = ca.Cdr;
        while (cur is Cell c)
        {
            var n = new Cell(c.Car, Const.NIL);
            tail.Cdr = n;
            tail = n;
            cur = c.Cdr;
        }
        tail.Cdr = b;
        return head;
    }

    public static object? Reverse1(object? lst)
    {
        object? r = Const.NIL;
        var cur = lst;
        while (cur is Cell c) { r = new Cell(c.Car, r); cur = c.Cdr; }
        return r;
    }

    public static object? StringLength(object? s)
    {
        if (s is SchemeString ss) return (long)ss.Length;
        string str = s is string str2 ? str2 : Printer.Format(s);
        int count = 0;
        foreach (var _ in str.EnumerateRunes()) count++;
        return (long)count;
    }

    public static object? VectorLength(object? v)
    {
        if (v is SchemeVector sv) return (long)sv.Data.Count;
        throw new Exception("vector-length: not a vector");
    }

    public static object? StringAppend2(object? a, object? b)
    {
        var sa = a is SchemeString ssa ? ssa.ToString() : a?.ToString() ?? "";
        var sb = b is SchemeString ssb ? ssb.ToString() : b?.ToString() ?? "";
        return new SchemeString(sa + sb);
    }

    public static object? StringRef(object? s, object? idx)
    {
        int i = NumericHelper.ToInt(idx);
        if (s is SchemeString ss) return new SchemeChar(ss.Data[i]);
        string str = s is string str2 ? str2 : Printer.Format(s);
        int count = 0;
        foreach (var rune in str.EnumerateRunes())
        {
            if (count == i) return new SchemeChar(rune.Value);
            count++;
        }
        throw new Exception("string-ref: out of bounds");
    }

    public static object? VectorRef(object? v, object? idx)
    {
        if (v is SchemeVector sv) return sv.Data[NumericHelper.ToInt(idx)];
        throw new Exception("vector-ref: not a vector");
    }

    public static object? ListLength(object? lst)
    {
        int n = 0;
        var cur = lst;
        while (cur is Cell) { n++; cur = ((Cell)cur).Cdr; }
        return (long)n;
    }

    public static object? ListTail(object? lst, object? n)
    {
        int k = NumericHelper.ToInt(n);
        var cur = lst;
        for (int i = 0; i < k; i++)
        {
            if (cur is Cell c) cur = c.Cdr;
            else throw new Exception("list-tail: out of bounds");
        }
        return cur;
    }

    public static object? ListRef(object? lst, object? n)
    {
        int k = NumericHelper.ToInt(n);
        var cur = lst;
        for (int i = 0; i < k; i++)
        {
            if (cur is Cell c) cur = c.Cdr;
            else throw new Exception("list-ref: out of bounds");
        }
        return cur is Cell c2 ? c2.Car : throw new Exception("list-ref: out of bounds");
    }

    public static object? Memq(object? x, object? lst)
    {
        var cur = lst;
        while (cur is Cell c)
        {
            if (ReferenceEquals(x, c.Car)) return cur;
            cur = c.Cdr;
        }
        return Const.FALSE;
    }

    public static object? Assq(object? x, object? lst)
    {
        var cur = lst;
        while (cur is Cell c)
        {
            if (c.Car is Cell pair && ReferenceEquals(x, pair.Car)) return c.Car;
            cur = c.Cdr;
        }
        return Const.FALSE;
    }

    public static object? Eqv(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return Const.TRUE;
        if (a is null || b is null) return Const.FALSE;
        if (a.GetType() == b.GetType())
        {
            if (a is int or long or BigInteger or SchemeFraction or double or Complex)
                return a.Equals(b) ? Const.TRUE : Const.FALSE;
            if (a is string s) return s == (string)b ? Const.TRUE : Const.FALSE;
            if (a is SchemeChar sc) return sc.Codepoint == ((SchemeChar)b).Codepoint ? Const.TRUE : Const.FALSE;
        }
        return Const.FALSE;
    }

    public static object? Equal2(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return Const.TRUE;
        if (a is Cell ca && b is Cell cb)
        {
            if (Equal2(ca.Car, cb.Car) != Const.TRUE) return Const.FALSE;
            return Equal2(ca.Cdr, cb.Cdr);
        }
        if (a is null || b is null) return Const.FALSE;
        if (a is SchemeVector va && b is SchemeVector vb)
            return va.Data.SequenceEqual(vb.Data, EqualityComparer<object?>.Create((x, y) => Equal2(x, y) == Const.TRUE)) ? Const.TRUE : Const.FALSE;
        if (a.GetType() == b.GetType())
        {
            if (a is int or long or BigInteger or SchemeFraction or double or Complex)
                return a.Equals(b) ? Const.TRUE : Const.FALSE;
            if (a is string s) return s == (string)b ? Const.TRUE : Const.FALSE;
            if (a is SchemeChar sc) return sc.Codepoint == ((SchemeChar)b).Codepoint ? Const.TRUE : Const.FALSE;
            if (a is SchemeString ssa && b is SchemeString ssb) return ssa.ToString() == ssb.ToString() ? Const.TRUE : Const.FALSE;
        }
        return Const.FALSE;
    }

    public static object? Member(object? x, object? lst)
    {
        var cur = lst;
        while (cur is Cell c)
        {
            if (Equal2(x, c.Car) == Const.TRUE) return cur;
            cur = c.Cdr;
        }
        return Const.FALSE;
    }

    public static object? Assoc(object? key, object? alist)
    {
        var cur = alist;
        while (cur is Cell c)
        {
            if (c.Car is Cell entry && Equal2(entry.Car, key) == Const.TRUE) return entry;
            cur = c.Cdr;
        }
        return Const.FALSE;
    }
}
