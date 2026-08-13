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

    // SRFI-141 Division

    // SRFI-143 Fixnums

    // SRFI-144 Flonums

    // SRFI-151 Bitwise (re-register as native for pyb)

    // Bitvectors

    // Number theory & math

    private static object? MakeComparatorPrimitive(object?[] args)
    {
        var eq = args.Length > 0 ? args[0] : Const.NIL;
        var lt = args.Length > 1 ? args[1] : Const.NIL;
        var hf = args.Length > 2 ? args[2] : Const.NIL;
        var nm = args.Length > 3 ? args[3] : Sym.Intern("custom");
        return new Cell(Sym.Intern("comparator"), new Cell(eq, new Cell(lt, new Cell(hf, new Cell(nm, Const.NIL)))));
    }

    private static object? FxAdd(object?[] args)
    {
        long r = 0;
        foreach (var a in args) r = checked((long)(r + NumericHelper.ToLong(a)));
        return r;
    }

    private static object? FxSubtract(object?[] args)
    {
        if (args.Length == 0) return Const.FALSE;
        if (args.Length == 1) return checked((long)-NumericHelper.ToLong(args[0]));
        long r = NumericHelper.ToLong(args[0]);
        for (int i = 1; i < args.Length; i++) r = checked((long)(r - NumericHelper.ToLong(args[i])));
        return r;
    }

    private static object? FxMultiply(object?[] args)
    {
        long r = 1;
        foreach (var a in args) r = checked((long)(r * NumericHelper.ToLong(a)));
        return r;
    }

    private static object? FxAnd(object?[] args)
    {
        long r = FX_GREATEST;
        foreach (var a in args) r &= NumericHelper.ToLong(a);
        return r;
    }

    private static object? FxIor(object?[] args)
    {
        long r = 0;
        foreach (var a in args) r |= NumericHelper.ToLong(a);
        return r;
    }

    private static object? FxXor(object?[] args)
    {
        long r = 0;
        foreach (var a in args) r ^= NumericHelper.ToLong(a);
        return r;
    }

    private static object? FxCopyBit(object?[] args)
    {
        long x = NumericHelper.ToLong(args[0]);
        int i = NumericHelper.ToInt(args[1]);
        bool b = args.Length > 2 && Truthy(args[2]);
        return b ? (x | (1L << i)) : (x & ~(1L << i));
    }

    private static object? FxFirstSetBit(object?[] args)
    {
        long x = NumericHelper.ToLong(args[0]);
        return x == 0 ? -1L : (long)BitOperations.TrailingZeroCount((ulong)x);
    }

    private static object? FlSubtract(object?[] args)
    {
        if (args.Length == 0) return Const.FALSE;
        if (args.Length == 1) return -NumericHelper.ToDouble(args[0]);
        double r = NumericHelper.ToDouble(args[0]);
        for (int i = 1; i < args.Length; i++) r -= NumericHelper.ToDouble(args[i]);
        return r;
    }

    private static object? FlDivide(object?[] args)
    {
        if (args.Length == 0) return Const.FALSE;
        if (args.Length == 1) return 1.0 / NumericHelper.ToDouble(args[0]);
        double r = NumericHelper.ToDouble(args[0]);
        for (int i = 1; i < args.Length; i++) r /= NumericHelper.ToDouble(args[i]);
        return r;
    }

    private static object? IntegerToBooleans(object?[] args)
    {
        long n = NumericHelper.ToLong(args[0]);
        var bits = new List<object?>();
        while (n != 0) { bits.Add((n & 1) != 0 ? Const.TRUE : Const.FALSE); n >>= 1; }
        if (bits.Count == 0) bits.Add(Const.FALSE);
        return bits.ToCell();
    }

    private static object? MakeBitvector(object?[] args)
    {
        int n = NumericHelper.ToInt(args[0]);
        object? fill = args.Length > 1 ? args[1] : Const.FALSE;
        var data = new List<object?>();
        for (int i = 0; i < n; i++) data.Add(fill);
        return new SchemeVector(data);
    }

    private static object? BitvectorAppend(object?[] args)
    {
        var all = new List<object?>();
        foreach (var bv in args) all.AddRange(((SchemeVector)bv!).Data);
        return new SchemeVector(all);
    }

    private static object? ListToBitvector(object?[] args)
    {
        var data = new List<object?>();
        foreach (var x in args[0].Cells()) data.Add(ReferenceEquals(x, Const.TRUE) ? Const.TRUE : Const.FALSE);
        return new SchemeVector(data);
    }

    private static object? BitvectorToList(object?[] args)
    {
        var data = new List<object?>();
        foreach (var x in ((SchemeVector)args[0]!).Data) data.Add(ReferenceEquals(x, Const.FALSE) ? Const.FALSE : Const.TRUE);
        return data.ToCell();
    }

    private static object? SchemeGcd(object?[] args)
    {
        if (args.Length == 0) return 0L;
        bool anyFrac = args.Any(a => a is SchemeFraction);
        if (anyFrac) return SchemeGcdFrac(args);
        long r = NumericHelper.ToLong(args[0]);
        for (int i = 1; i < args.Length; i++) r = Gcd(r, NumericHelper.ToLong(args[i]));
        return r;
    }

    private static object? Factorial(object?[] args)
    {
        long n = NumericHelper.ToLong(args[0]);
        long r = 1;
        for (long i = 2; i <= n; i++) r *= i;
        return r;
    }

    private static object? Fibonacci(object?[] args)
    {
        long n = NumericHelper.ToLong(args[0]);
        if (n < 0) return 0L;
        long a = 0, b = 1;
        for (long i = 0; i < n; i++) { var t = a + b; a = b; b = t; }
        return a;
    }

    private static object? ChainCmp(object?[] args, Func<long, long, bool> cmp)
    {
        for (int i = 1; i < args.Length; i++)
            if (!cmp(NumericHelper.ToLong(args[i - 1]), NumericHelper.ToLong(args[i]))) return Const.FALSE;
        return Const.TRUE;
    }

    private static object? FxEqual(object?[] args) => ChainCmp(args, (a, b) => a == b);
    private static object? FxLessThan(object?[] args) => ChainCmp(args, (a, b) => a < b);
    private static object? FxGreaterThan(object?[] args) => ChainCmp(args, (a, b) => a > b);
    private static object? FxLessThanOrEqual(object?[] args) => ChainCmp(args, (a, b) => a <= b);
    private static object? FxGreaterThanOrEqual(object?[] args) => ChainCmp(args, (a, b) => a >= b);
    private static object? FlEqual(object?[] args) => ChainCmp(args, (a, b) => a == b);
    private static object? FlLessThan(object?[] args) => ChainCmp(args, (a, b) => a < b);
    private static object? FlGreaterThan(object?[] args) => ChainCmp(args, (a, b) => a > b);
    private static object? FlLessThanOrEqual(object?[] args) => ChainCmp(args, (a, b) => a <= b);
    private static object? FlGreaterThanOrEqual(object?[] args) => ChainCmp(args, (a, b) => a >= b);

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
        var pair = (Cell)FibPair(n / 2)!;
        long a = NumericHelper.ToLong(pair.Car);
        long b = NumericHelper.ToLong(pair.Cdr);
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
        if (e < 0)
        {
            long a = ((b % m) + m) % m, t = 0, newT = 1, rem = m, newRem = a;
            while (newRem != 0) { var q = rem / newRem; (t, newT) = (newT, t - q * newT); (rem, newRem) = (newRem, rem - q * newRem); }
            if (rem != 1) throw new SchemeException("expt-mod: non-invertible base");
            b = (t % m + m) % m;
            e = -e;
        }
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
