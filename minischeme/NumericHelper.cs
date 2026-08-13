using System.Numerics;

namespace Miniscm.Types;

public sealed class SchemeFraction : IEquatable<SchemeFraction>
{
    public BigInteger Num { get; }
    public BigInteger Den { get; }

    private static BigInteger Gcd(BigInteger a, BigInteger b)
    {
        a = BigInteger.Abs(a); b = BigInteger.Abs(b);
        while (b != 0) { var t = b; b = a % b; a = t; }
        return a;
    }

    public SchemeFraction(BigInteger num, BigInteger den)
    {
        if (den == 0) throw new DivideByZeroException();
        if (den < 0) { num = -num; den = -den; }
        var g = Gcd(num, den);
        Num = g <= 1 ? num : num / g;
        Den = g <= 1 ? den : den / g;
    }

    public bool Equals(SchemeFraction? other) => other is not null && Num == other.Num && Den == other.Den;
    public override bool Equals(object? obj) => obj is SchemeFraction f && Equals(f);
    public override int GetHashCode() => HashCode.Combine(Num, Den);
    public override string ToString() => $"{Num}/{Den}";

    public double ToDouble() => (double)Num / (double)Den;
}

public static class NumericHelper
{
    public enum NumType { Int, Fraction, Real, Complex }

    public static NumType Classify(object? x)
    {
        if (x is int or long or BigInteger) return NumType.Int;
        if (x is SchemeFraction) return NumType.Fraction;
        if (x is double or float) return NumType.Real;
        if (x is Complex) return NumType.Complex;
        throw new ArgumentException($"not a number: {Printer.Format(x)}");
    }

    public static NumType Wider(NumType a, NumType b) => (NumType)Math.Max((int)a, (int)b);

    public static BigInteger ToBigInt(object? x) => x switch
    {
        int i => new BigInteger(i),
        long l => new BigInteger(l),
        BigInteger bi => bi,
        SchemeFraction f when f.Den == 1 => f.Num,
        _ => new BigInteger(NumericHelper.ToLong(x))
    };

    public static double ToDouble(object? x) => x switch
    {
        int i => i,
        long l => l,
        double d => d,
        BigInteger bi => (double)bi,
        SchemeFraction f => f.ToDouble(),
        Complex c => c.Real,
        _ => Convert.ToDouble(x)
    };

    public static Complex ToComplex(object? x) => x switch
    {
        Complex c => c,
        int i => new Complex(i, 0),
        long l => new Complex(l, 0),
        double d => new Complex(d, 0),
        BigInteger bi => new Complex((double)bi, 0),
        SchemeFraction f => new Complex(f.ToDouble(), 0),
        _ => new Complex(Convert.ToDouble(x), 0)
    };

    public static SchemeFraction ToFraction(object? x) => x switch
    {
        int i => new SchemeFraction(i, 1),
        long l => new SchemeFraction(l, 1),
        BigInteger bi => new SchemeFraction(bi, 1),
        SchemeFraction f => f,
        double d => DoubleToExactFraction(d),
        _ => DoubleToExactFraction(Convert.ToDouble(x))
    };

    private static SchemeFraction DoubleToExactFraction(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d))
            throw new ArgumentException("cannot convert infinity/NaN to exact fraction");
        long bits = BitConverter.DoubleToInt64Bits(d);
        bool negative = (bits >> 63) != 0;
        int exponent = (int)((bits >> 52) & 0x7FF);
        long mantissa = bits & 0xFFFFFFFFFFFFF;
        if (exponent == 0)
            mantissa <<= 1;
        else
            mantissa |= 0x10000000000000;
        if (negative) mantissa = -mantissa;
        if (exponent == 0) exponent = 1;
        var exp = exponent - 1075;
        if (exp >= 0)
            return new SchemeFraction(mantissa << exp, 1);
        return new SchemeFraction(mantissa, BigInteger.One << (-exp));
    }

    public static object? CoerceTo(object? a, object? b, NumType target)
    {
        var ta = Classify(a); var tb = Classify(b);
        if (target <= ta && target <= tb) return null;
        if (target == NumType.Int)
        {
            var va = ta >= NumType.Fraction ? ToFraction(a) : null;
            var vb = tb >= NumType.Fraction ? ToFraction(b) : null;
            return (va, vb);
        }
        if (target == NumType.Fraction)
        {
            var va = ta >= NumType.Fraction ? ToFraction(a) : null;
            var vb = tb >= NumType.Fraction ? ToFraction(b) : null;
            return (va, vb);
        }
        if (target == NumType.Real)
        {
            var va = ta >= NumType.Real ? ToDouble(a) : (double?)null;
            var vb = tb >= NumType.Real ? ToDouble(b) : (double?)null;
            return (va, vb);
        }
        if (target == NumType.Complex)
        {
            var va = ta >= NumType.Complex ? ToComplex(a) : (Complex?)null;
            var vb = tb >= NumType.Complex ? ToComplex(b) : (Complex?)null;
            return (va, vb);
        }
        return null;
    }

    public static object? Add(object? a, object? b)
    {
        var ta = Classify(a); var tb = Classify(b);
        var w = Wider(ta, tb);

        if (w <= NumType.Int)
        {
            var ia = ToBigInt(a); var ib = ToBigInt(b);
            var r = ia + ib;
            return r <= long.MaxValue && r >= long.MinValue ? (long)r : r;
        }
        if (w == NumType.Fraction)
        {
            var fa = ToFraction(a); var fb = ToFraction(b);
            var num = fa.Num * fb.Den + fb.Num * fa.Den;
            var den = fa.Den * fb.Den;
            var r = new SchemeFraction(num, den);
            if (r.Den == 1) return r.Num <= long.MaxValue && r.Num >= long.MinValue ? (long)r.Num : r.Num;
            return r;
        }
        if (w == NumType.Real) return ToDouble(a) + ToDouble(b);
        return ToComplex(a) + ToComplex(b);
    }

    public static object? Sub(object? a, object? b)
    {
        var ta = Classify(a); var tb = Classify(b);
        var w = Wider(ta, tb);

        if (w <= NumType.Int)
        {
            var ia = ToBigInt(a); var ib = ToBigInt(b);
            var r = ia - ib;
            return r <= long.MaxValue && r >= long.MinValue ? (long)r : r;
        }
        if (w == NumType.Fraction)
        {
            var fa = ToFraction(a); var fb = ToFraction(b);
            var num = fa.Num * fb.Den - fb.Num * fa.Den;
            var den = fa.Den * fb.Den;
            var r = new SchemeFraction(num, den);
            if (r.Den == 1) return r.Num <= long.MaxValue && r.Num >= long.MinValue ? (long)r.Num : r.Num;
            return r;
        }
        if (w == NumType.Real) return ToDouble(a) - ToDouble(b);
        return ToComplex(a) - ToComplex(b);
    }

    public static object? Mul(object? a, object? b)
    {
        var ta = Classify(a); var tb = Classify(b);
        var w = Wider(ta, tb);

        if (w <= NumType.Int)
        {
            var ia = ToBigInt(a); var ib = ToBigInt(b);
            var r = ia * ib;
            return r <= long.MaxValue && r >= long.MinValue ? (long)r : r;
        }
        if (w == NumType.Fraction)
        {
            var fa = ToFraction(a); var fb = ToFraction(b);
            var num = fa.Num * fb.Num;
            var den = fa.Den * fb.Den;
            var r = new SchemeFraction(num, den);
            if (r.Den == 1) return r.Num <= long.MaxValue && r.Num >= long.MinValue ? (long)r.Num : r.Num;
            return r;
        }
        if (w == NumType.Real) return ToDouble(a) * ToDouble(b);
        return ToComplex(a) * ToComplex(b);
    }

    public static object? Div(object? a, object? b)
    {
        var ta = Classify(a); var tb = Classify(b);
        var w = Wider(ta, tb);

        if (w <= NumType.Int)
        {
            var ia = ToBigInt(a); var ib = ToBigInt(b);
            if (ia % ib == 0)
            {
                var r = ia / ib;
                return r <= long.MaxValue && r >= long.MinValue ? (long)r : r;
            }
            return new SchemeFraction(ia, ib);
        }
        if (w == NumType.Fraction)
        {
            var fa = ToFraction(a); var fb = ToFraction(b);
            var num = fa.Num * fb.Den;
            var den = fa.Den * fb.Num;
            if (den < 0) { num = -num; den = -den; }
            if (num % den == 0)
            {
                var r = num / den;
                return r <= long.MaxValue && r >= long.MinValue ? (long)r : r;
            }
            return new SchemeFraction(num, den);
        }
        if (w == NumType.Real) return ToDouble(a) / ToDouble(b);
        return ToComplex(a) / ToComplex(b);
    }

    public static object? Negate(object? a)
    {
        return a switch
        {
            int i => -i,
            long l => -l,
            double d => -d,
            BigInteger bi => -bi,
            SchemeFraction f => new SchemeFraction(-f.Num, f.Den),
            Complex c => -c,
            _ => throw new ArgumentException($"not a number: {Printer.Format(a)}")
        };
    }

    public static object? Recip(object? a)
    {
        return a switch
        {
            int i => new SchemeFraction(1, i),
            long l => new SchemeFraction(1, l),
            double d => 1.0 / d,
            BigInteger bi => new SchemeFraction(1, bi),
            SchemeFraction f => new SchemeFraction(f.Den, f.Num),
            Complex c => Complex.One / c,
            _ => throw new ArgumentException($"not a number: {Printer.Format(a)}")
        };
    }

    public static object? Quotient(object? a, object? b)
    {
        if (a is SchemeFraction || b is SchemeFraction)
        {
            var left = ToFraction(a); var right = ToFraction(b);
            var den = left.Den * right.Num;
            if (den == 0) throw new DivideByZeroException();
            var q = BigInteger.Divide(left.Num * right.Den, den);
            return q <= long.MaxValue && q >= long.MinValue ? (long)q : q;
        }
        var ia = ToBigInt(a); var ib = ToBigInt(b);
        var r = ia / ib;
        return r <= long.MaxValue && r >= long.MinValue ? (long)r : r;
    }

    public static object? Remainder(object? a, object? b)
    {
        if (a is SchemeFraction || b is SchemeFraction)
            return Sub(a, Mul(Quotient(a, b), b));
        var ia = ToBigInt(a); var ib = ToBigInt(b);
        var r = ia % ib;
        return r <= long.MaxValue && r >= long.MinValue ? (long)r : r;
    }

    public static object? Modulo(object? a, object? b)
    {
        if (a is SchemeFraction || b is SchemeFraction)
            return Remainder(a, b);
        var ia = ToBigInt(a); var ib = ToBigInt(b);
        var r = ((ia % ib) + ib) % ib;
        return r <= long.MaxValue && r >= long.MinValue ? (long)r : r;
    }

    public static int Compare(object? a, object? b)
    {
        var ta = Classify(a); var tb = Classify(b);
        var w = Wider(ta, tb);
        if (w <= NumType.Int)
        {
            var ia = ToBigInt(a); var ib = ToBigInt(b);
            return ia.CompareTo(ib);
        }
        if (w <= NumType.Fraction)
        {
            var fa = ToFraction(a); var fb = ToFraction(b);
            return (fa.Num * fb.Den).CompareTo(fb.Num * fa.Den);
        }
        var da = ToDouble(a); var db = ToDouble(b);
        if (double.IsNaN(da) || double.IsNaN(db)) return 1;
        return da.CompareTo(db);
    }

    public static bool IsZero(object? x) => x switch
    {
        int i => i == 0,
        long l => l == 0,
        double d => d == 0.0,
        BigInteger bi => bi.IsZero,
        SchemeFraction f => f.Num.IsZero,
        _ => false
    };

    public static bool IsEven(object? x) => x switch
    {
        int i => i % 2 == 0,
        long l => l % 2 == 0,
        BigInteger bi => bi.IsEven,
        SchemeFraction f => f.Num.IsEven,
        _ => false
    };

    public static long ToLong(object? x) => x switch
    {
        int i => i,
        long l => l,
        double d => (long)d,
        BigInteger bi => (long)bi,
        SchemeFraction f => (long)(f.Num / f.Den),
        _ => Convert.ToInt64(x)
    };

    public static int ToInt(object? x) => (int)ToLong(x);
}
