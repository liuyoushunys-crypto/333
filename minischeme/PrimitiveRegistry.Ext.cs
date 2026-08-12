using System.Numerics;
using System.Runtime.CompilerServices;
using Miniscm.Types;
using Miniscm.Eval;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    // ── initenv_ext.py 对齐补齐 ──
    // miniscm/initenv_ext.py（注册自 primitives_ext.py）中，
    // minischeme 运行时（含 scm 库）仍未定义的 builtin。
    // 由 Program.cs 在 scm 库加载后调用 InitExt() 注册。
    public static void InitExt()
    {
        RegisterExtComparators();
        RegisterExtDivision();
        RegisterExtFixnums();
        RegisterExtFlonums();
        RegisterExtBitwise();
        RegisterExtBitvectors();
        RegisterExtNumberTheory();
        RegisterExtLists();
        RegisterExtStrings();
        RegisterExtChars();
        RegisterExtVectors();
        RegisterExtMisc();

        // SRFI-35/36: error conditions
        _b("make-error-condition", args =>
        {
            var t = args.Length > 0 ? args[0] : Const.NIL;
            var m = args.Length > 1 ? args[1] : Const.NIL;
            return ("condition", t, m);
        });
        _b("condition-message", args =>
        {
            if (args[0] is ITuple ct && ct.Length > 2)
                return ct[2];
            if (args[0] is ErrorObject eo)
                return eo.Message is Sym em ? em.Name : eo.Message;
            if (args[0] is SchemeException se) return se.Val?.ToString() ?? "";
            return ToStr(args[0]);
        });

        // describe: 打印对象到 stdout
        _b("describe", args =>
        {
            Console.WriteLine(Printer.Format(args[0]));
            return Const.VOID;
        });

        // SRFI-144: flonum / fixnum conversions
        _b("fixnum->flonum", args => NumericHelper.ToDouble(args[0]));
        _b("flonum->fixnum", args => NumericHelper.ToLong(args[0]));
        _b("float", args => NumericHelper.ToDouble(args[0]));
        _b("flexp2", args => Math.Pow(2.0, NumericHelper.ToDouble(args[0])));
        _b("flfinite?", args => args[0] is double d && double.IsFinite(d) ? Const.TRUE : Const.FALSE);
        _b("flinfinite?", args => args[0] is double d && double.IsInfinity(d) ? Const.TRUE : Const.FALSE);
        _b("flnan?", args => args[0] is double d && double.IsNaN(d) ? Const.TRUE : Const.FALSE);

        // SRFI-141: floor division remainder
        _b("floor-rem", args => NumericHelper.Modulo(args[0], args[1]));

        // SRFI-143: fixnum bitwise / arithmetic
        _b("fxbit-count", args => PopCount(NumericHelper.ToLong(args[0])));
        _b("fxbit-set?", args => (NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]) & 1) != 0 ? Const.TRUE : Const.FALSE);
        _b("fxcopy-bit", args =>
        {
            long x = NumericHelper.ToLong(args[0]);
            int i = NumericHelper.ToInt(args[1]);
            bool b = args.Length > 2 && Truthy(args[2]);
            return b ? x : (x | (1L << i));
        });
        _b("fxdiv0", args => FloorDiv(args[0], args[1]));
        _b("fxfirst-set-bit", args =>
        {
            long x = NumericHelper.ToLong(args[0]);
            return x == 0 ? -1L : (long)(BitOperations.TrailingZeroCount((ulong)x));
        });
        _b("fxgcd", PGcd);
        _b("fxif", args => (NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[1])) | (~NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[2])));
        _b("fxlength", args => BitLength(NumericHelper.ToLong(args[0])));
        _b("fxmod0", args => NumericHelper.Modulo(args[0], args[1]));

        // SRFI-189: maybe values
        _b("maybe->values", args =>
        {
            if (args[0] is Cell mc) return new Cell(mc.Car, Const.TRUE);
            return new Cell(Const.FALSE, Const.FALSE);
        });

        // random seed
        _b("random-seed", args =>
        {
            var seed = NumericHelper.ToInt(args[0]);
            _extRandomState = seed;
            return Const.VOID;
        });
    }

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

    private static long FloorDiv(object? a, object? b)
    {
        var ia = NumericHelper.ToBigInt(a);
        var ib = NumericHelper.ToBigInt(b);
        var r = ia / ib;
        if (ia % ib != 0 && (ia < 0) != (ib < 0)) r -= 1;
        return (long)r;
    }
}
