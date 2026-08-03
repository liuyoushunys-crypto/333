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
        {
            var r = cv.Invoke(cv.Env, argsVal);
            while (r is TailCall tcr) r = EvalTailCall(tcr);
            return r;
        }
        if (procVal is LambdaProc lp)
        {
            if (lp.CompiledVersion is CompiledLambda cl)
            {
                var r = cl.Invoke(lp.ClosureEnv, argsVal);
                while (r is TailCall tcr) r = EvalTailCall(tcr);
                return r;
            }
            var nenv = new Env(lp.ClosureEnv, lp.Params.Count);
            Evaluator.BindParams(lp.Params, argsVal, nenv);
            var r2 = Evaluator.SeqTailCall(lp.Body, nenv);
            while (r2 is TailCall tcr2) r2 = Evaluator.EvalCore(tcr2.Expr, tcr2.Env);
            return r2;
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

    // Unwrap a TailCall produced by JIT MakeTailCall: (proc 'v1 'v2 ...).
    // Applies proc to the (already-evaluated, quoted) args directly, avoiding
    // re-entry into the full interpreter.
    internal static object? EvalTailCall(TailCall tc)
    {
        var expr = tc.Expr;
        if (expr is not Cell ec) return Evaluator.EvalCore(expr, tc.Env);
        var proc = ec.Car;
        var args = new List<object?>();
        var cur = ec.Cdr;
        while (cur is Cell ac)
        {
            var arg = ac.Car;
            // MakeTailCall wraps each arg in (quote v)
            if (arg is Cell qc && qc.Car is Sym qs && qs.Name == "quote")
                arg = qc.Cdr is Cell qarg ? qarg.Car : arg;
            args.Add(arg);
            cur = ac.Cdr;
        }
        var r = Invoke(proc, args.ToArray(), tc.Env);
        while (r is TailCall tcr) r = EvalTailCall(tcr);
        return r;
    }

    public static TailCall MakeTailCall(object? proc, object?[] argsList, Env env)
    {
        object? argCells = Const.NIL;
        for (int i = argsList.Length - 1; i >= 0; i--)
            argCells = new Cell(new Cell(Sym.QUOTE, new Cell(argsList[i], Const.NIL)), argCells);
        var expr = new Cell(proc, argCells);
        return new TailCall(expr, env);
    }

    public static object? CarOf(object? x) => x is Cell c ? c.Car : throw new Exception("car: not a pair");
    public static object? CdrOf(object? x) => x is Cell c ? c.Cdr : throw new Exception("cdr: not a pair");

    public static LambdaProc MakeLambda(List<string> @params, bool isSimple, Env env, object? body)
    {
        return new LambdaProc(@params, body, env, isSimple);
    }

    public static Env MakeClosure(Env env, string name, object? val)
    {
        env.Data[name] = val;
        return env;
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

    // 数值标量相等 (Eqv/Equal2 共享)
    public static bool ScalarEquals(object? a, object? b)
    {
        if (a is long la && b is long lb) return la == lb;
        if (a is long la2 && b is int ib2) return la2 == ib2;
        if (a is int ia3 && b is long lb3) return ia3 == lb3;
        if (a is int ia4 && b is int ib4) return ia4 == ib4;
        if (a is BigInteger ba && b is BigInteger bb) return ba == bb;
        if (a is BigInteger ba2 && b is long lb5) return ba2 == lb5;
        if (a is long la6 && b is BigInteger bb6) return la6 == bb6;
        if (a is BigInteger ba7 && b is int ib7) return ba7 == ib7;
        if (a is int ia8 && b is BigInteger bb8) return ia8 == bb8;
        if (a is SchemeFraction fa && b is SchemeFraction fb) return fa.Equals(fb);
        if (a is SchemeFraction fa2 && b is long lf) return fa2.Equals(new SchemeFraction(lf, 1));
        if (a is long lf2 && b is SchemeFraction fb2) return fb2.Equals(new SchemeFraction(lf2, 1));
        if (a is double da && b is double db) return da == db;
        if (a is double da2 && b is long dl) return da2 == dl;
        if (a is long dl2 && b is double db2) return dl2 == db2;
        if (a is Complex cxa && b is Complex cxb) return cxa == cxb;
        return false;
    }

    public static object? Eqv(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return Const.TRUE;
        if (a is null || b is null) return Const.FALSE;
        if (ScalarEquals(a, b)) return Const.TRUE;
        if (a is string s && b is string sb) return s == sb ? Const.TRUE : Const.FALSE;
        if (a is SchemeChar sca && b is SchemeChar scb) return sca.Codepoint == scb.Codepoint ? Const.TRUE : Const.FALSE;
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
        if (a is SchemeString ssa2 && b is string sb2) return ssa2.ToString() == sb2 ? Const.TRUE : Const.FALSE;
        if (a is string sa2 && b is SchemeString ssb2) return sa2 == ssb2.ToString() ? Const.TRUE : Const.FALSE;
        if (a is SchemeString ssa3 && b is SchemeString ssb3) return ssa3.ToString() == ssb3.ToString() ? Const.TRUE : Const.FALSE;
        if (a is string sa3 && b is string sb3) return sa3 == sb3 ? Const.TRUE : Const.FALSE;
        if (ScalarEquals(a, b)) return Const.TRUE;
        if (a is SchemeChar sca && b is SchemeChar scb) return sca.Codepoint == scb.Codepoint ? Const.TRUE : Const.FALSE;
        if (a is Sym syma && b is Sym symb) return syma.Name == symb.Name ? Const.TRUE : Const.FALSE;
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

    public static object? Map1(object? f, object? lst)
    {
        var items = new List<object?>();
        var cur = lst;
        while (cur is Cell c)
        {
            items.Add(Invoke(f, [c.Car], Evaluator.GlobalEnv));
            cur = c.Cdr;
        }
        return items.ToCell();
    }

    public static object? Filter1(object? pred, object? lst)
    {
        var items = new List<object?>();
        var cur = lst;
        while (cur is Cell c)
        {
            if (Invoke(pred, [c.Car], Evaluator.GlobalEnv) == Const.TRUE)
                items.Add(c.Car);
            cur = c.Cdr;
        }
        return items.ToCell();
    }

    public static object? ForEach1(object? f, object? lst)
    {
        var cur = lst;
        while (cur is Cell c)
        {
            Invoke(f, [c.Car], Evaluator.GlobalEnv);
            cur = c.Cdr;
        }
        return Const.VOID;
    }

    public static object? ApplyList(object? f, object? lst)
    {
        var args = new List<object?>();
        var cur = lst;
        while (cur is Cell c) { args.Add(c.Car); cur = c.Cdr; }
        return Invoke(f, args.ToArray(), Evaluator.GlobalEnv);
    }
}
