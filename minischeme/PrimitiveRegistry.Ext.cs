using System.Numerics;
using System.Runtime.CompilerServices;
using Miniscm.Types;
using Miniscm.Eval;
using Miniscm.Compiler;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    // ── initenv_ext.py 对齐补齐 ──
    // miniscm/initenv_ext.py（注册自 primitives_ext.py）中，
    // minischeme 运行时（含 scm 库）仍未定义的 builtin。
    // 由 Program.cs 在 scm 库加载后调用 InitExt() 注册。

    private static long _extRandomState = Environment.TickCount;

    private static bool Truthy(object? v)
    {
        if (v is Sym s) return !ReferenceEquals(s, Const.FALSE);
        if (v is Nil) return false;
        return true;
    }

    private static long PopCount(long x)
    {
        return BitOperations.PopCount((ulong)x);
    }

    private static long BitLength(long x)
    {
        if (x == 0) return 0;
        var ux = x < 0 ? (ulong)(~x) : (ulong)x;
        return (long)(64 - BitOperations.LeadingZeroCount(ux));
    }

    private static object? PMakeErrorCondition(object?[] args) => ("condition", args.Length > 0 ? args[0] : Const.NIL, args.Length > 1 ? args[1] : Const.NIL);
    private static object? PMakeConditionType(object?[] args) => ("condition-type", args.Length > 0 ? args[0] : Const.FALSE, args.Length > 1 ? args[1] : Const.FALSE);
    private static object? PMakeCondition(object?[] args)
    {
        var type = args.Length > 0 ? args[0] : Const.FALSE;
        var fields = args.Length > 1 ? args[1..] : [];
        return ("condition", type, fields.ToList().ToCell());
    }
    private static object? PConditionRef(object?[] args)
    {
        if (args[0] is ITuple t && t.Length > 2 && t[0] is "condition")
        {
            var fields = ((object?)t[2]).Cells();
            for (var i = 0; i + 1 < fields.Count; i += 2)
                if (JitRuntime.Equal2(fields[i], args[1]) == Const.TRUE) return fields[i + 1];
        }
        return Const.FALSE;
    }
    private static object? PConditionMessage(object?[] args)
    {
        if (args[0] is ITuple ct && ct.Length > 2) return ct[2];
        if (args[0] is ErrorObject eo) return eo.Message is Sym em ? em.Name : eo.Message;
        if (args[0] is SchemeException se) return se.Val?.ToString() ?? "";
        return ToStr(args[0]);
    }
    private static object? PDescribe(object?[] args) { Console.WriteLine(Printer.Format(args[0])); return Const.VOID; }
    private static object? PFxCopyBit(object?[] args)
    {
        long x = NumericHelper.ToLong(args[0]);
        int i = NumericHelper.ToInt(args[1]);
        bool b = args.Length > 2 && Truthy(args[2]);
        return b ? x : (x | (1L << i));
    }
    private static object? PFxFirstSetBit(object?[] args)
    {
        long x = NumericHelper.ToLong(args[0]);
        return x == 0 ? -1L : (long)BitOperations.TrailingZeroCount((ulong)x);
    }
    private static object? PMaybeValues(object?[] args) => args[0] is Cell mc ? new Cell(mc.Car, Const.TRUE) : new Cell(Const.FALSE, Const.FALSE);
    private static object? PRandomSeed(object?[] args) { _extRandomState = NumericHelper.ToInt(args[0]); return Const.VOID; }

}
