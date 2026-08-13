using System.Numerics;
using Miniscm.Types;
using Miniscm.Eval;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    private const long FX_GREATEST = long.MaxValue;
    private const long FX_LEAST = long.MinValue;

    private static object MakeComparator(object eq, object lt, object hash)
        => new Cell(Sym.Intern("comparator"), new Cell(eq, new Cell(lt, new Cell(hash, Const.NIL))));

    private static object? CallComparator(object? comparator, object? a, object? b, int fallback)
    {
        if (comparator is Cell c && c.Cdr is Cell fields)
        {
            var proc = fallback == 0 ? fields.Car : fields.Cdr is Cell rest ? rest.Car : null;
            if (proc is not null) return App(proc, a, b);
        }
        return fallback == 0
            ? (NumericHelper.Compare(a, b) == 0 ? Const.TRUE : Const.FALSE)
            : (NumericHelper.Compare(a, b) < 0 ? Const.TRUE : Const.FALSE);
    }

    // SRFI-128 Comparators
    private static object? RegisterExtComparators()
    {
        _b("make-comparator", args =>
        {
            var eq = args.Length > 0 ? args[0] : Const.NIL;
            var lt = args.Length > 1 ? args[1] : Const.NIL;
            var hf = args.Length > 2 ? args[2] : Const.NIL;
            var nm = args.Length > 3 ? args[3] : Sym.Intern("custom");
            return new Cell(Sym.Intern("comparator"), new Cell(eq, new Cell(lt, new Cell(hf, new Cell(nm, Const.NIL)))));
        });
        _b("comparator?", args => args[0] is Cell c && c.Car is Sym s && s.Name == "comparator" ? Const.TRUE : Const.FALSE);
        _b("comparator-order?", args => args[0] is Cell c && c.Car is Sym s && s.Name == "comparator" ? Const.TRUE : Const.FALSE);
        _b("comparator-hashable?", args => args[0] is Cell c && c.Car is Sym s && s.Name == "comparator" ? Const.TRUE : Const.FALSE);
        _b("integer-comparator", _ => MakeComparator(
            (Func<object?[], object?>)(_ => Const.TRUE),
            (Func<object?[], object?>)(a => NumericHelper.Compare(a[0], a[1]) == 0 ? Const.TRUE : Const.FALSE),
            (Func<object?[], object?>)(a => NumericHelper.Compare(a[0], a[1]) < 0 ? Const.TRUE : Const.FALSE)));
        _b("=?", args => CallComparator(args[0], args[1], args[2], 0));
        _b("<?", args => CallComparator(args[0], args[1], args[2], -1));
        _b("comparator-test-type", args => (Func<object?[], object?>)(_ => Const.TRUE));
        _b("make-default-comparator", args => new Cell(Sym.Intern("comparator"),
            new Cell((Func<object?[], object?>)(a => (object?)(Const.TRUE)), Const.NIL)));
        _b("make-eq-comparator", args => new Cell(Sym.Intern("comparator"), new Cell((Func<object?[], object?>)(a => (object?)(Const.TRUE)), Const.NIL)));
        _b("make-eqv-comparator", args => new Cell(Sym.Intern("comparator"), new Cell((Func<object?[], object?>)(a => (object?)(Const.TRUE)), Const.NIL)));
        _b("make-equal-comparator", args => new Cell(Sym.Intern("comparator"), new Cell((Func<object?[], object?>)(a => (object?)(Const.TRUE)), Const.NIL)));
        return Const.VOID;
    }

    // SRFI-141 Division
    private static object? RegisterExtDivision()
    {
        _b("floor-div", args => FloorDiv(args[0], args[1]));
        _b("floor-mod", args => NumericHelper.Modulo(args[0], args[1]));
        _b("floor-rem", args => NumericHelper.Modulo(args[0], args[1]));
        _b("floor-quotient", args => FloorDiv(args[0], args[1]));
        _b("floor-remainder", args => NumericHelper.Modulo(args[0], args[1]));
        _b("floor/", args => new Cell(FloorDiv(args[0], args[1]), NumericHelper.Modulo(args[0], args[1])));

        _b("truncate-div", args => NumericHelper.Quotient(args[0], args[1]));
        _b("truncate-rem", args => NumericHelper.Remainder(args[0], args[1]));
        _b("truncate-quotient", args => NumericHelper.Quotient(args[0], args[1]));
        _b("truncate-remainder", args => NumericHelper.Remainder(args[0], args[1]));
        _b("truncate/", args => new Cell(NumericHelper.Quotient(args[0], args[1]), NumericHelper.Remainder(args[0], args[1])));

        _b("ceiling-div", args => CeilDiv(args[0], args[1]));
        _b("ceiling-rem", args => CeilRem(args[0], args[1]));
        _b("ceiling-quotient", args => CeilDiv(args[0], args[1]));
        _b("ceiling-remainder", args => CeilRem(args[0], args[1]));
        _b("ceiling/", args => new Cell(CeilDiv(args[0], args[1]), CeilRem(args[0], args[1])));

        _b("round-div", args => RoundDiv(args[0], args[1]));
        _b("round-quotient", args => RoundDiv(args[0], args[1]));
        _b("round-rem", args => NumericHelper.Sub(args[0], NumericHelper.Mul(RoundDiv(args[0], args[1]), args[1])));
        _b("round-remainder", args => NumericHelper.Sub(args[0], NumericHelper.Mul(RoundDiv(args[0], args[1]), args[1])));
        _b("round/", args => new Cell(RoundDiv(args[0], args[1]), NumericHelper.Sub(args[0], NumericHelper.Mul(RoundDiv(args[0], args[1]), args[1]))));

        _b("euclidean-div", args => EuclideanDiv(args[0], args[1]));
        _b("euclidean-rem", args => EuclideanRem(args[0], args[1]));
        _b("euclidean-quotient", args => EuclideanDiv(args[0], args[1]));
        _b("euclidean-remainder", args => EuclideanRem(args[0], args[1]));
        _b("euclidean/", args => new Cell(EuclideanDiv(args[0], args[1]), EuclideanRem(args[0], args[1])));

        // exact/inexact floor/round/etc conversions
        _b("floor->exact", args => args[0] is double df ? (object?)(long)Math.Floor(df) : args[0] is SchemeFraction fr1 ? (object?)(long)Math.Floor((double)fr1.Num / (double)fr1.Den) : args[0]);
        _b("ceiling->exact", args => args[0] is double dc ? (object?)(long)Math.Ceiling(dc) : args[0] is SchemeFraction fr2 ? (object?)(long)Math.Ceiling((double)fr2.Num / (double)fr2.Den) : args[0]);
        _b("round->exact", args => args[0] is double dr ? (object?)(long)Math.Round(dr) : args[0] is SchemeFraction fr3 ? (object?)(long)Math.Round((double)fr3.Num / (double)fr3.Den) : args[0]);
        _b("truncate->exact", args => args[0] is double dt ? (object?)(long)dt : args[0] is SchemeFraction fr4 ? (object?)(long)(fr4.Num / fr4.Den) : args[0]);
        _b("exact", args => args[0] is double de && de == Math.Floor(de) ? (object?)(long)de : args[0]);
        _b("inexact", args => NumericHelper.ToDouble(args[0]));
        return Const.VOID;
    }

    // SRFI-143 Fixnums
    private static object? RegisterExtFixnums()
    {
        _b("fx-width", args => 64L);
        _b("fx-greatest", args => FX_GREATEST);
        _b("fx-least", args => FX_LEAST);
        _b("fx+", args =>
        {
            long r = 0;
            foreach (var a in args) r = checked((long)(r + NumericHelper.ToLong(a)));
            return r;
        });
        _b("fx-", args =>
        {
            if (args.Length == 0) return Const.FALSE;
            if (args.Length == 1) return checked((long)-NumericHelper.ToLong(args[0]));
            long r = NumericHelper.ToLong(args[0]);
            for (int i = 1; i < args.Length; i++) r = checked((long)(r - NumericHelper.ToLong(args[i])));
            return r;
        });
        _b("fx*", args =>
        {
            long r = 1;
            foreach (var a in args) r = checked((long)(r * NumericHelper.ToLong(a)));
            return r;
        });
        _b("fxdiv", args => NumericHelper.Quotient(args[0], args[1]));
        _b("fxmod", args => NumericHelper.Remainder(args[0], args[1]));
        _b("fxdiv0", args => FloorDiv(args[0], args[1]));
        _b("fxmod0", args => NumericHelper.Modulo(args[0], args[1]));
        _b("fxzero?", args => NumericHelper.ToLong(args[0]) == 0 ? Const.TRUE : Const.FALSE);
        _b("fxpositive?", args => NumericHelper.ToLong(args[0]) > 0 ? Const.TRUE : Const.FALSE);
        _b("fxnegative?", args => NumericHelper.ToLong(args[0]) < 0 ? Const.TRUE : Const.FALSE);
        _b("fxodd?", args => (NumericHelper.ToLong(args[0]) & 1) != 0 ? Const.TRUE : Const.FALSE);
        _b("fxeven?", args => (NumericHelper.ToLong(args[0]) & 1) == 0 ? Const.TRUE : Const.FALSE);
        _b("fxmax", args => args.Max(a => NumericHelper.ToLong(a)));
        _b("fxmin", args => args.Min(a => NumericHelper.ToLong(a)));
        _b("fxand", args =>
        {
            long r = FX_GREATEST;
            foreach (var a in args) r &= NumericHelper.ToLong(a);
            return r;
        });
        _b("fxior", args =>
        {
            long r = 0;
            foreach (var a in args) r |= NumericHelper.ToLong(a);
            return r;
        });
        _b("fxxor", args =>
        {
            long r = 0;
            foreach (var a in args) r ^= NumericHelper.ToLong(a);
            return r;
        });
        _b("fxnot", args => NumericHelper.ToLong(args[0]) ^ FX_GREATEST);
        _b("fxlsh", args => (long)(NumericHelper.ToLong(args[0]) << NumericHelper.ToInt(args[1])));
        _b("fxrshl", args => NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]));
        _b("fxrsha", args => NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]));
        _b("fx=?",
            args => ChainCmp(args, (a, b) => a == b));
        _b("fx<?",
            args => ChainCmp(args, (a, b) => a < b));
        _b("fx>?",
            args => ChainCmp(args, (a, b) => a > b));
        _b("fx<=?",
            args => ChainCmp(args, (a, b) => a <= b));
        _b("fx>=?",
            args => ChainCmp(args, (a, b) => a >= b));
        _b("fxbit-count", args => PopCount(NumericHelper.ToLong(args[0])));
        _b("fxbit-set?", args => (NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]) & 1) != 0 ? Const.TRUE : Const.FALSE);
        _b("fxcopy-bit", args =>
        {
            long x = NumericHelper.ToLong(args[0]);
            int i = NumericHelper.ToInt(args[1]);
            bool b = args.Length > 2 && Truthy(args[2]);
            return b ? (x | (1L << i)) : (x & ~(1L << i));
        });
        _b("fxfirst-set-bit", args =>
        {
            long x = NumericHelper.ToLong(args[0]);
            return x == 0 ? -1L : (long)BitOperations.TrailingZeroCount((ulong)x);
        });
        _b("fxlength", args => BitLength(NumericHelper.ToLong(args[0])));
        _b("fxif", args => (NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[1])) | (~NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[2])));
        _b("fxgcd", PGcd);
        return Const.VOID;
    }

    // SRFI-144 Flonums
    private static object? RegisterExtFlonums()
    {
        _b("flonum?", args => args[0] is double or float ? Const.TRUE : Const.FALSE);
        _b("fl+", args => args.Aggregate(0.0, (a, b) => a + NumericHelper.ToDouble(b)));
        _b("fl-", args =>
        {
            if (args.Length == 0) return Const.FALSE;
            if (args.Length == 1) return -NumericHelper.ToDouble(args[0]);
            double r = NumericHelper.ToDouble(args[0]);
            for (int i = 1; i < args.Length; i++) r -= NumericHelper.ToDouble(args[i]);
            return r;
        });
        _b("fl*", args => args.Aggregate(1.0, (a, b) => a * NumericHelper.ToDouble(b)));
        _b("fl/", args =>
        {
            if (args.Length == 0) return Const.FALSE;
            if (args.Length == 1) return 1.0 / NumericHelper.ToDouble(args[0]);
            double r = NumericHelper.ToDouble(args[0]);
            for (int i = 1; i < args.Length; i++) r /= NumericHelper.ToDouble(args[i]);
            return r;
        });
        _b("flzero?", args => NumericHelper.ToDouble(args[0]) == 0.0 ? Const.TRUE : Const.FALSE);
        _b("flpositive?", args => NumericHelper.ToDouble(args[0]) > 0.0 ? Const.TRUE : Const.FALSE);
        _b("flnegative?", args => NumericHelper.ToDouble(args[0]) < 0.0 ? Const.TRUE : Const.FALSE);
        _b("flodd?", args => ((long)NumericHelper.ToDouble(args[0]) % 2) != 0 ? Const.TRUE : Const.FALSE);
        _b("fleven?", args => ((long)NumericHelper.ToDouble(args[0]) % 2) == 0 ? Const.TRUE : Const.FALSE);
        _b("flfinite?", args => args[0] is double d && double.IsFinite(d) ? Const.TRUE : Const.FALSE);
        _b("flinfinite?", args => args[0] is double d && double.IsInfinity(d) ? Const.TRUE : Const.FALSE);
        _b("flnan?", args => args[0] is double d && double.IsNaN(d) ? Const.TRUE : Const.FALSE);
        _b("flmax", args => args.Max(a => NumericHelper.ToDouble(a)));
        _b("flmin", args => args.Min(a => NumericHelper.ToDouble(a)));
        _b("flfloor", args => (double)Math.Floor(NumericHelper.ToDouble(args[0])));
        _b("flceiling", args => (double)Math.Ceiling(NumericHelper.ToDouble(args[0])));
        _b("flround", args => (double)Math.Round(NumericHelper.ToDouble(args[0])));
        _b("fltruncate", args => (double)Math.Truncate(NumericHelper.ToDouble(args[0])));
        _b("flsqrt", args => Math.Sqrt(NumericHelper.ToDouble(args[0])));
        _b("flexp", args => Math.Exp(NumericHelper.ToDouble(args[0])));
        _b("flexpt", args => Math.Pow(NumericHelper.ToDouble(args[0]), NumericHelper.ToDouble(args[1])));
        _b("fllog", args => Math.Log(NumericHelper.ToDouble(args[0])));
        _b("flsin", args => Math.Sin(NumericHelper.ToDouble(args[0])));
        _b("flcos", args => Math.Cos(NumericHelper.ToDouble(args[0])));
        _b("fltan", args => Math.Tan(NumericHelper.ToDouble(args[0])));
        _b("flasin", args => Math.Asin(NumericHelper.ToDouble(args[0])));
        _b("flacos", args => Math.Acos(NumericHelper.ToDouble(args[0])));
        _b("flatan", args => Math.Atan(NumericHelper.ToDouble(args[0])));
        _b("fl=?",
            args => ChainCmp(args, (a, b) => a == b));
        _b("fl<?",
            args => ChainCmp(args, (a, b) => a < b));
        _b("fl>?",
            args => ChainCmp(args, (a, b) => a > b));
        _b("fl<=?",
            args => ChainCmp(args, (a, b) => a <= b));
        _b("fl>=?",
            args => ChainCmp(args, (a, b) => a >= b));
        _b("flonum->fixnum", args => NumericHelper.ToLong(args[0]));
        _b("fixnum->flonum", args => NumericHelper.ToDouble(args[0]));
        return Const.VOID;
    }

    // SRFI-151 Bitwise (re-register as native for pyb)
    private static object? RegisterExtBitwise()
    {
        _b("integer->booleans", args =>
        {
            long n = NumericHelper.ToLong(args[0]);
            var bits = new List<object?>();
            while (n != 0) { bits.Add((n & 1) != 0 ? Const.TRUE : Const.FALSE); n >>= 1; }
            if (bits.Count == 0) bits.Add(Const.FALSE);
            return bits.ToCell();
        });
        return Const.VOID;
    }

    // Bitvectors
    private static object? RegisterExtBitvectors()
    {
        _b("bitvector?", args => args[0] is SchemeVector ? Const.TRUE : Const.FALSE);
        _b("make-bitvector", args =>
        {
            int n = NumericHelper.ToInt(args[0]);
            object? fill = args.Length > 1 ? args[1] : Const.FALSE;
            var data = new List<object?>();
            for (int i = 0; i < n; i++) data.Add(fill);
            return new SchemeVector(data);
        });
        _b("bitvector-copy", args => new SchemeVector(((SchemeVector)args[0]!).Data.ToList()));
        _b("bitvector-append", args =>
        {
            var all = new List<object?>();
            foreach (var bv in args) all.AddRange(((SchemeVector)bv!).Data);
            return new SchemeVector(all);
        });
        _b("bitvector-length", args => ((SchemeVector)args[0]!).Length);
        _b("bitvector-ref", args => ((SchemeVector)args[0]!)[NumericHelper.ToInt(args[1])] is Sym s && !ReferenceEquals(s, Const.FALSE) ? Const.TRUE : Const.FALSE);
        _b("bitvector-set!", args => { ((SchemeVector)args[0]!)[NumericHelper.ToInt(args[1])] = args[2]; return Const.VOID; });
        _b("list->bitvector", args =>
        {
            var data = new List<object?>();
            foreach (var x in args[0].Cells()) data.Add(ReferenceEquals(x, Const.TRUE) ? Const.TRUE : Const.FALSE);
            return new SchemeVector(data);
        });
        _b("bitvector->list", args =>
        {
            var data = new List<object?>();
            foreach (var x in ((SchemeVector)args[0]!).Data) data.Add(ReferenceEquals(x, Const.FALSE) ? Const.FALSE : Const.TRUE);
            return data.ToCell();
        });
        return Const.VOID;
    }

    // Number theory & math
    private static object? RegisterExtNumberTheory()
    {
        _b("scheme-gcd", args =>
        {
            if (args.Length == 0) return 0L;
            bool anyFrac = args.Any(a => a is SchemeFraction);
            if (anyFrac) return SchemeGcdFrac(args);
            long r = NumericHelper.ToLong(args[0]);
            for (int i = 1; i < args.Length; i++) r = Gcd(r, NumericHelper.ToLong(args[i]));
            return r;
        });
        _b("factorial", args =>
        {
            long n = NumericHelper.ToLong(args[0]);
            long r = 1;
            for (long i = 2; i <= n; i++) r *= i;
            return r;
        });
        _b("fibonacci", args =>
        {
            long n = NumericHelper.ToLong(args[0]);
            if (n < 0) return 0L;
            long a = 0, b = 1;
            for (long i = 0; i < n; i++) { var t = a + b; a = b; b = t; }
            return a;
        });
        _b("fib-pair", args => FibPair(NumericHelper.ToLong(args[0])));
        _b("prime?", args => IsPrime(NumericHelper.ToLong(args[0])) ? Const.TRUE : Const.FALSE);
        _b("factor", args => Factor(NumericHelper.ToLong(args[0])).ToCell());
        _b("binomial", args => Binomial(NumericHelper.ToLong(args[0]), NumericHelper.ToLong(args[1])));
        _b("permutations", args => args.Length == 1 && args[0] is Cell ? ListPermutations(args[0].Cells()).ToCell() : Permutations(NumericHelper.ToLong(args[0]), NumericHelper.ToLong(args[1])).ToCell());
        _b("combinations", args => args[0] is Cell ? ListCombinations(args[0].Cells(), NumericHelper.ToLong(args[1])).ToCell() : Combinations(NumericHelper.ToLong(args[0]), NumericHelper.ToLong(args[1])).ToCell());
        _b("quick-expt", args => QuickExpt(NumericHelper.ToLong(args[0]), NumericHelper.ToLong(args[1])));
        _b("expt-mod", args => ModPow(NumericHelper.ToLong(args[0]), NumericHelper.ToLong(args[1]), NumericHelper.ToLong(args[2])));
        _b("log-base", args => Math.Log(NumericHelper.ToDouble(args[0]), NumericHelper.ToDouble(args[1])));
        _b("log2", args => Math.Log2(NumericHelper.ToDouble(args[0])));
        _b("log10", args => Math.Log10(NumericHelper.ToDouble(args[0])));
        _b("degrees->radians", args => NumericHelper.ToDouble(args[0]) * Math.PI / 180.0);
        _b("radians->degrees", args => NumericHelper.ToDouble(args[0]) * 180.0 / Math.PI);
        _b("square", args => NumericHelper.Mul(args[0], args[0]));
        _b("sinh", args => Math.Sinh(NumericHelper.ToDouble(args[0])));
        _b("cosh", args => Math.Cosh(NumericHelper.ToDouble(args[0])));
        _b("tanh", args => Math.Tanh(NumericHelper.ToDouble(args[0])));
        _b("sech", args => 1.0 / Math.Cosh(NumericHelper.ToDouble(args[0])));
        _b("csch", args => 1.0 / Math.Sinh(NumericHelper.ToDouble(args[0])));
        _b("coth", args => 1.0 / Math.Tanh(NumericHelper.ToDouble(args[0])));
        return Const.VOID;
    }

    private static object? ChainCmp(object?[] args, Func<long, long, bool> cmp)
    {
        for (int i = 1; i < args.Length; i++)
            if (!cmp(NumericHelper.ToLong(args[i - 1]), NumericHelper.ToLong(args[i]))) return Const.FALSE;
        return Const.TRUE;
    }

    private static object? SchemeGcdFrac(object?[] args)
    {
        BigInteger num = 0, den = 1;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is SchemeFraction fr) { num = fr.Num; den = fr.Den; break; }
            if (args[i] is long or int or BigInteger) { num = NumericHelper.ToBigInt(args[i]); den = 1; break; }
        }
        for (int i = 0; i < args.Length; i++)
        {
            BigInteger n2, d2;
            if (args[i] is SchemeFraction fr2) { n2 = fr2.Num; d2 = fr2.Den; }
            else { n2 = NumericHelper.ToBigInt(args[i]); d2 = 1; }
            var g1 = BigInteger.GreatestCommonDivisor(num, n2);
            var g2 = BigInteger.GreatestCommonDivisor(den, d2);
            num = g1;
            den = BigInteger.Abs(den * d2 / g2);
            if (den != 0)
            {
                var g = BigInteger.GreatestCommonDivisor(num, den);
                num /= g; den /= g;
            }
        }
        if (den == 1) return (object?)num;
        if (den < 0) { num = -num; den = -den; }
        return new SchemeFraction(num, den);
    }

    private static long Gcd(long a, long b)
    {
        a = Math.Abs(a); b = Math.Abs(b);
        while (b != 0) { var t = a % b; a = b; b = t; }
        return a;
    }

    private static object? FibPair(long n)
    {
        if (n <= 0) return new Cell(0L, 1L);
        var pair = FibPair(n / 2);
        long a = NumericHelper.ToLong(((Cell)pair).Car);
        long b = NumericHelper.ToLong(((Cell)pair).Cdr);
        long c = a * (b * 2 - a);
        long d = a * a + b * b;
        if (n % 2 == 0) return new Cell(c, d);
        return new Cell(d, c + d);
    }

    private static bool IsPrime(long n)
    {
        if (n < 2) return false;
        if (n == 2) return true;
        if (n % 2 == 0) return false;
        for (long d = 3; d * d <= n; d += 2)
            if (n % d == 0) return false;
        return true;
    }

    private static List<object?> Factor(long n)
    {
        var factors = new List<object?>();
        long d = 2;
        while (d * d <= n)
        {
            while (n % d == 0) { factors.Add(d); n /= d; }
            d += d == 2 ? 1 : 2;
        }
        if (n > 1) factors.Add(n);
        return factors;
    }

    private static long Binomial(long n, long k)
    {
        if (k < 0 || k > n) return 0;
        k = Math.Min(k, n - k);
        long r = 1;
        for (long i = 0; i < k; i++) r = r * (n - i) / (i + 1);
        return r;
    }

    private static List<object?> Permutations(long n, long k)
    {
        var res = new List<object?>();
        for (long i = 0; i < k; i++) res.Add(n - i);
        return res;
    }

    private static List<object?> Combinations(long n, long k)
    {
        var res = new List<object?>();
        long b = Binomial(n, k);
        for (long i = 0; i < b; i++) res.Add(i);
        return res;
    }

    private static IEnumerable<object?> ListPermutations(IReadOnlyList<object?> items)
    {
        if (items.Count == 0) { yield return Const.NIL; yield break; }
        for (var i = 0; i < items.Count; i++)
            foreach (var tail in ListPermutations(items.Where((_, n) => n != i).ToList()))
                yield return new Cell(items[i], tail);
    }

    private static IEnumerable<object?> ListCombinations(IReadOnlyList<object?> items, long k)
    {
        if (k == 0) { yield return Const.NIL; yield break; }
        if (k < 0 || k > items.Count) yield break;
        for (var i = 0; i <= items.Count - k; i++)
            foreach (var tail in ListCombinations(items.Skip(i + 1).ToList(), k - 1))
                yield return new Cell(items[i], tail);
    }

    private static object? QuickExpt(long b, long e)
    {
        long r = 1;
        while (e > 0)
        {
            if ((e & 1) == 1) r *= b;
            b *= b;
            e >>= 1;
        }
        return r;
    }

    private static object? ModPow(long b, long e, long m)
    {
        if (e < 0) throw new SchemeException("expt-mod: negative exponent");
        long r = 1;
        b %= m;
        while (e > 0)
        {
            if ((e & 1) == 1) r = r * b % m;
            b = b * b % m;
            e >>= 1;
        }
        return r;
    }

    private static long CeilDiv(object? a, object? b)
    {
        var fa = NumericHelper.ToFraction(a); var fb = NumericHelper.ToFraction(b);
        var den = fa.Den * fb.Num;
        if (den == 0) throw new DivideByZeroException();
        var num = fa.Num * fb.Den;
        var q = num / den;
        if (num % den != 0 && (num < 0) == (den < 0)) q++;
        return (long)q;
    }

    private static object? FloorDiv(object? a, object? b)
    {
        var fa = NumericHelper.ToFraction(a); var fb = NumericHelper.ToFraction(b);
        var den = fa.Den * fb.Num;
        if (den == 0) throw new DivideByZeroException();
        var num = fa.Num * fb.Den;
        var q = num / den;
        if (num % den != 0 && (num < 0) != (den < 0)) q--;
        return q <= long.MaxValue && q >= long.MinValue ? (object?)(long)q : q;
    }

    private static BigInteger FloorDivBig(BigInteger ia, BigInteger ib)
    {
        var r = ia / ib;
        if (ia % ib != 0 && (ia < 0) != (ib < 0)) r -= 1;
        return r;
    }

    private static object? CeilRem(object? a, object? b)
    {
        return NumericHelper.Sub(a, NumericHelper.Mul(CeilDiv(a, b), b));
    }

    private static long RoundDiv(object? a, object? b)
    {
        var fa = NumericHelper.ToFraction(a); var fb = NumericHelper.ToFraction(b);
        var ia = fa.Num * fb.Den;
        var ib = fa.Den * fb.Num;
        var q = ia * 2 / ib;
        var r = ia % ib;
        var rounded = ia / ib;
        if (r * 2 >= ib || r * 2 <= -ib)
            rounded += (ia < 0) == (ib < 0) ? 1 : -1;
        return (long)rounded;
    }

    private static object? EuclideanDiv(object? a, object? b)
    {
        if (a is SchemeFraction || b is SchemeFraction)
        {
            var divisor = NumericHelper.ToFraction(b);
            return divisor.Num.Sign >= 0 ? FloorDiv(a, b) : NumericHelper.Negate(FloorDiv(NumericHelper.Negate(a), NumericHelper.Negate(b)));
        }
        var ia = NumericHelper.ToBigInt(a);
        var ib = NumericHelper.ToBigInt(b);
        var ibAbs = BigInteger.Abs(ib);
        var r = ((ia % ibAbs) + ibAbs) % ibAbs;
        return (long)((ia - r) / ib);
    }

    private static object? EuclideanRem(object? a, object? b)
    {
        if (a is SchemeFraction || b is SchemeFraction)
            return NumericHelper.Sub(a, NumericHelper.Mul(EuclideanDiv(a, b), b));
        var ia = NumericHelper.ToBigInt(a);
        var ib = NumericHelper.ToBigInt(b);
        var ibAbs = BigInteger.Abs(ib);
        var r = ((ia % ibAbs) + ibAbs) % ibAbs;
        return (long)r;
    }
}
