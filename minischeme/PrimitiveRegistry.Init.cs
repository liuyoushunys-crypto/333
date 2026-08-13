using System.Numerics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Miniscm.Types;
using Miniscm.Eval;
using Miniscm.Compiler;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    private static object IntegerBits(long n)
    {
        if (n == 0) return new Cell(0L, Nil.Instance);
        var bits = new List<object?>();
        var value = Math.Abs(n);
        while (value != 0) { bits.Add(value & 1); value >>= 1; }
        return bits.ToCell()!;
    }

    private static object? ClosePort(object?[] args)
    {
        if (args[0] is ITuple it && it.Length > 2 && it[0] is "port" && it[2] is IDisposable d) d.Dispose();
        return Const.VOID;
    }

    private static object? RationalExpt(object?[] args)
    {
        var value = Math.Pow(NumericHelper.ToDouble(args[0]), NumericHelper.ToDouble(args[1]));
        return value == Math.Truncate(value) ? (object?)(long)value : value;
    }


    private static object? PPairFold(object?[] args)
    {
        object? acc = args[1];
        var cur = args[2];
        while (cur is Cell c) { acc = App(args[0], cur, acc); cur = c.Cdr; }
        return acc;
    }

    private static object? PPairFoldRight(object?[] args)
    {
        var pairs = new List<object?>();
        var cur = args[2];
        while (cur is Cell c) { pairs.Add(cur); cur = c.Cdr; }
        object? acc = args[1];
        for (int i = pairs.Count - 1; i >= 0; i--) acc = App(args[0], pairs[i], acc);
        return acc;
    }

    private static object? PSplitAt(object?[] args)
    {
        var first = new List<object?>();
        var cur = args[0];
        int n = NumericHelper.ToInt(args[1]);
        while (cur is Cell c && n-- > 0) { first.Add(c.Car); cur = c.Cdr; }
        return new Cell(first.ToCell(), new Cell(cur, Const.NIL));
    }

    private static object? PInitConditionMessage(object?[] args)
    {
        if (args[0] is ErrorObject eo) return eo.Message is Sym em ? em.Name : eo.Message;
        if (args[0] is SchemeException se) return se.Val?.ToString() ?? "";
        return ToStr(args[0]);
    }

    private static object? PConditionReportString(object?[] args)
    {
        if (args[0] is ErrorObject eo3) return new SchemeString(eo3.Message is Sym em3 ? em3.Name : ToStr(eo3.Message));
        return new SchemeString("unknown condition");
    }

    private static object? PUtf8String(object?[] args)
    {
        byte[] data = args[0] is SchemeBytevector bv ? bv.Data : Encoding.UTF8.GetBytes(ToStr(args[0]));
        int start = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
        int end = args.Length > 2 ? NumericHelper.ToInt(args[2]) : data.Length;
        if (start < 0) start = 0;
        if (end > data.Length) end = data.Length;
        return new SchemeString(Encoding.UTF8.GetString(data, start, end - start));
    }

    private static object? PStringForEach(object?[] args)
    {
        var fn = args[0];
        var s = ToStr(args[1]);
        foreach (var rune in s.EnumerateRunes()) App(fn, new SchemeChar(rune.Value));
        return Const.VOID;
    }

    private static object? PStringMap(object?[] args)
    {
        var fn = args[0];
        var s = ToStr(args[1]);
        var sb = new StringBuilder();
        foreach (var rune in s.EnumerateRunes()) sb.Append(char.ConvertFromUtf32(AsChar(App(fn, new SchemeChar(rune.Value)))));
        return new SchemeString(sb.ToString());
    }

    private static object? PBytevectorCopyBang(object?[] args)
    {
        var target = AsBytevector(args[0]); var at = NumericHelper.ToInt(args[1]);
        var source = AsBytevector(args[2]); var start = args.Length > 3 ? NumericHelper.ToInt(args[3]) : 0;
        var end = args.Length > 4 ? NumericHelper.ToInt(args[4]) : source.Length;
        for (var i = start; i < end; i++) target[at++] = source[i];
        return Const.VOID;
    }

    private static object? PWithInputFromString(object?[] args)
    {
        var oldIn = Console.In;
        using var sr = new StringReader(ToStr(args[0]));
        Console.SetIn(sr);
        try { return App(args[1]); } finally { Console.SetIn(oldIn); }
    }

    private static object? PCallWithInputString(object?[] args)
    {
        var port = MakePort("input", new StringPort(ToStr(args[0])));
        return App(args[1], port);
    }

    private static object? PCallWithPort(object?[] args)
    {
        try { return App(args[1], args[0]); }
        finally { if (args[0] is ITuple it && it.Length > 2 && it[2] is IDisposable d) d.Dispose(); }
    }

    private static object? PHashTableAlist(object?[] args)
    {
        var items = new List<object?>();
        foreach (var kv in (Dictionary<object, object?>)args[0]!) items.Add(new Cell(kv.Key, kv.Value));
        return items.ToCell();
    }

    private static object? PAlistHashTable(object?[] args)
    {
        var ht = new Dictionary<object, object?>();
        var cur = args[0];
        while (cur is Cell c) { if (c.Car is Cell pair) ht[pair.Car!] = pair.Cdr; cur = c.Cdr; }
        return ht;
    }

    private static object? PHashTableMap(object?[] args)
    {
        var fn = args[0];
        var items = new List<object?>();
        foreach (var kv in (Dictionary<object, object?>)args[1]!) items.Add(JitRuntime.Invoke(fn, [kv.Key, kv.Value], Evaluator.GlobalEnv));
        return items.ToCell();
    }

    private static object? PHashTableFold(object?[] args)
    {
        var fn = args[0];
        object? acc = args[1];
        foreach (var kv in (Dictionary<object, object?>)args[2]!) acc = JitRuntime.Invoke(fn, [acc, kv.Key, kv.Value], Evaluator.GlobalEnv);
        return acc;
    }

    private static object? PBooleansInteger(object?[] args)
    {
        long r = 0;
        for (int i = 0; i < args.Length; i++) if (ReferenceEquals(args[i], Const.TRUE)) r |= 1L << i;
        return r;
    }

    private static object? PCxr(object?[] args, Func<object?, object?>[] chain)
    {
        object? x = args[0];
        for (int i = chain.Length - 1; i >= 0; i--) x = chain[i](x);
        return x;
    }
}
