using System.Collections;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Miniscm.Types;
using Miniscm.Eval;
using Miniscm.Compiler;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    public sealed class SchemeHook { public List<object?> Procedures { get; set; } = []; }
    public sealed class SchemeRandomSource { public long State { get; set; } public SchemeRandomSource(long state) => State = state; }
    public sealed class SchemeListQueue { public List<object?> Items { get; } = []; }
    public sealed class SchemeBinaryHeap
    {
        public List<object?> Items { get; } = [];
        public object? Comparator { get; set; }
    }
    public sealed class SchemeBimap
    {
        public Dictionary<object, object?> Forward { get; } = [];
        public Dictionary<object, object?> Reverse { get; } = [];
    }
    public sealed class SchemeDeque { public List<object?> Items { get; } = []; }
    public sealed class SchemeArray { public SchemeVector Value { get; } public SchemeArray(SchemeVector value) => Value = value; }

    public sealed class SchemeText { public SchemeString Value { get; } public SchemeText(object? value) => Value = new SchemeString(value is SchemeString s ? s.ToString() : value?.ToString() ?? ""); }
    public sealed class SchemeFlexVector { public List<object?> Items { get; } public SchemeFlexVector(int n, object? fill) => Items = Enumerable.Repeat(fill, n).ToList(); }
    public sealed class SchemeIntegerSet { public HashSet<long> Items { get; } = []; }
    public sealed class SchemeEnumSet { public HashSet<object?> Items { get; } = []; }
    public sealed class SchemeIdeque { public List<object?> Items { get; } = []; }
    public sealed class SchemeEphemeron { public object? Key { get; } public object? Value { get; set; } public SchemeEphemeron(object? key, object? value) { Key = key; Value = value; } }
    public sealed class SchemeDomain { public long Low { get; } public long High { get; } public SchemeDomain(long low, long high) { Low = low; High = high; } }
    public sealed class SchemeColor { public double R, G, B, A; public SchemeColor(double r, double g, double b, double a = 1) { R = r; G = g; B = b; A = a; } }
    public sealed class SchemeOption { public object? Names, Default, Handler; public SchemeOption(object? n, object? d, object? h) { Names = n; Default = d; Handler = h; } }
    public sealed class SchemeArray2D { public int Rows, Columns; public object?[] Data; public SchemeArray2D(int r, int c, object? fill) { Rows = r; Columns = c; Data = Enumerable.Repeat(fill, r * c).ToArray(); } }

    static readonly Dictionary<string, SchemeString> MutableStringViews =
        new(ReferenceEqualityComparer.Instance);

    static object? PAppendBang(object?[] args) => PAppend(args);

    static object? PAppendReverseBang(object?[] args)
    {
        if (args.Length != 2) throw new SchemeException("append-reverse!: expected two lists");
        return PAppend([PReverse([args[0]]), args[1]]);
    }

    static object? PConcatenateBang(object?[] args)
    {
        if (args.Length != 1) throw new SchemeException("concatenate!: expected one list of lists");
        return Concatenate(args[0]);
    }

    static object? PAssertViolation(object?[] args)
    {
        var who = args.Length > 0 ? ToStr(args[0]) : "assertion";
        var message = args.Length > 1 ? args[1] : new SchemeString("assertion violation");
        var irritants = args.Length > 2 ? args[2..].ToList().ToCell() : Const.NIL;
        throw new SchemeException(new ErrorObject(new SchemeString($"{who}: {ToStr(message)}"), irritants));
    }

    static object? PCharSetUnfold(object?[] args)
    {
        if (args.Length < 4) throw new SchemeException("char-set-unfold: expected stop?, mapper, successor, seed");
        var result = new bool[256];
        object? state = args[3];
        while (!Truthy(App(args[0], state)))
        {
            var mapped = App(args[1], state);
            var cp = AsChar(mapped);
            if (cp < 256) result[cp] = true;
            state = App(args[2], state);
        }
        if (args.Length > 4) result = (bool[])CharSetBinOp([args[4], result], true)!;
        return result;
    }

    static object? PDropRightBang(object?[] args)
    {
        if (args.Length < 2 || args[0] is not Cell list) throw new SchemeException("drop-right!: expected a non-empty proper list and count");
        var n = NumericHelper.ToInt(args[1]);
        if (n < 0) throw new SchemeException("drop-right!: count must be non-negative");
        if (n == 0) return list;
        var items = list.Cells().ToList();
        if (n >= items.Count) throw new SchemeException("drop-right!: cannot mutate a list into the empty list");
        var keep = items.Count - n;
        var cur = list;
        for (int i = 1; i < keep; i++)
        {
            if (cur.Cdr is not Cell next) throw new SchemeException("drop-right!: expected a proper list");
            cur = next;
        }
        cur.Cdr = Const.NIL;
        return list;
    }

    static object? PFindTail(object?[] args)
    {
        object? cur = args[1];
        while (cur is Cell c)
        {
            if (Truthy(App(args[0], c.Car))) return cur;
            cur = c.Cdr;
        }
        return Const.FALSE;
    }

    static object? PFoldRight1(object?[] args)
    {
        if (args.Length < 2 || args[1] is not Cell) throw new SchemeException("fold-right-1: expected procedure and non-empty list");
        var values = args[1].Cells().ToList();
        object? acc = values[^1];
        for (int i = values.Count - 2; i >= 0; i--) acc = App(args[0], values[i], acc);
        return acc;
    }

    static object? PIncludeCi(object?[] args)
    {
        var requested = ToStr(args[0]);
        var full = Path.GetFullPath(requested);
        if (!File.Exists(full))
        {
            var directory = Path.GetDirectoryName(full) ?? Directory.GetCurrentDirectory();
            var name = Path.GetFileName(full);
            var match = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory).FirstOrDefault(x => string.Equals(Path.GetFileName(x), name, StringComparison.OrdinalIgnoreCase))
                : null;
            full = match ?? full;
        }
        if (!File.Exists(full)) throw new SchemeException($"include-ci: file not found: {requested}");
        return PLoad([full]);
    }

    static object? PIntegerCharSet(object?[] args)
    {
        var value = NumericHelper.ToBigInt(args[0]);
        var result = new bool[256];
        for (int i = 0; i < 256; i++) result[i] = !value.IsZero && (value & (System.Numerics.BigInteger.One << i)) != 0;
        return result;
    }

    static object? PLsetAdjoin(object?[] args)
    {
        if (args.Length < 2) throw new SchemeException("lset-adjoin: expected comparator and list");
        var result = args[1].Cells().ToList();
        foreach (var item in args[2..])
            if (!result.Any(x => Truthy(App(args[0], item, x)))) result.Add(item);
        return result.ToCell();
    }

    static object? PLsetSubset(object?[] args)
    {
        if (args.Length < 3) return Const.TRUE;
        for (int i = 1; i < args.Length - 1; i++)
            foreach (var item in args[i].Cells())
                if (!args[i + 1].Cells().Any(x => Truthy(App(args[0], item, x)))) return Const.FALSE;
        return Const.TRUE;
    }

    static object? PRandomSourceMakeIntegers(object?[] args)
    {
        var source = args[0] as SchemeRandomSource ?? throw new SchemeException("random-source-make-integers: expected random source");
        return (Func<object?[], object?>)(a => S12RandomInt(source, NumericHelper.ToInt(a[0])));
    }

    static object? PRandomSourceMakeReals(object?[] args)
    {
        var source = args[0] as SchemeRandomSource ?? throw new SchemeException("random-source-make-reals: expected random source");
        return (Func<object?[], object?>)(_ => S12RandomReal(source));
    }

    static object? UnsupportedPrimitive(string name, object?[] _) => throw new SchemeException($"{name}: unsupported by this implementation");





    static object? PMakeList(object?[] args)
    {
        var n = NumericHelper.ToInt(args[0]);
        var fill = args.Length > 1 ? args[1] : Const.NIL;
        return Enumerable.Repeat(fill, n).ToCell();
    }


    static object? PListSetBang(object?[] args)
    {
        var n = NumericHelper.ToInt(args[1]);
        object? cur = args[0];
        for (int i = 0; i < n; i++)
        {
            if (cur is not Cell c) throw new Exception("list-set!: index out of range");
            cur = c.Cdr;
        }
        if (cur is not Cell target) throw new Exception("list-set!: index out of range");
        target.Car = args[2];
        return Const.VOID;
    }


    static object? PMemv(object?[] args)
    {
        object? cur = args[1];
        while (cur is Cell c) { if (c.Car?.Equals(args[0]) == true) return cur; cur = c.Cdr; }
        return Const.FALSE;
    }

    static object? PMember(object?[] args)
    {
        object? cur = args[1];
        while (cur is Cell c)
        {
            if (ReferenceEquals(Miniscm.Compiler.JitRuntime.Equal2(c.Car, args[0]), Const.TRUE))
                return cur;
            cur = c.Cdr;
        }
        return Const.FALSE;
    }


    static object? PDiv(object?[] args)
    {
        if (args.Length == 1) return NumericHelper.Recip(args[0]);
        return args.Skip(1).Aggregate((object?)args[0], (acc, x) => NumericHelper.Div(acc!, x))!;
    }

    static object? PExpt(object?[] args)
    {
        var a = args[0]; var b = args[1];
        var ta = NumericHelper.Classify(a); var tb = NumericHelper.Classify(b);
        if (ta <= NumericHelper.NumType.Int && tb == NumericHelper.NumType.Int && NumericHelper.ToLong(b) >= 0)
        {
            var base_ = NumericHelper.ToBigInt(a); var exp = NumericHelper.ToInt(b);
            var r = BigInteger.Pow(base_, exp);
            return r <= long.MaxValue && r >= long.MinValue ? (long)r : r;
        }
        if (tb == NumericHelper.NumType.Int)
        {
            var fa = NumericHelper.ToFraction(a);
            var exp = NumericHelper.ToLong(b);
            BigInteger num = fa.Num, den = fa.Den;
            if (exp < 0)
            {
                (num, den) = (den, num);
                exp = -exp;
            }
            var rn = BigInteger.Pow(num, (int)exp);
            var rd = BigInteger.Pow(den, (int)exp);
            if (rd == 1)
                return rn <= long.MaxValue && rn >= long.MinValue ? (long)rn : rn;
            return new SchemeFraction(rn, rd);
        }
        return Math.Pow(NumericHelper.ToDouble(a), NumericHelper.ToDouble(b));
    }

    static object? PSqrt(object?[] args)
    {
        var a = args[0];
        if (NumericHelper.Classify(a) <= NumericHelper.NumType.Int)
        {
            var bi = NumericHelper.ToBigInt(a);
            if (bi >= 0)
            {
                var s = (long)Math.Floor(Math.Sqrt((double)bi));
                while ((s + 1) * (s + 1) <= (long)bi) s++;
                while (s * s > (long)bi) s--;
                if (s * s == bi) return (long)s;
            }
        }
        return Math.Sqrt(NumericHelper.ToDouble(a));
    }

    static object? PAbs(object?[] args)
    {
        var a = args[0];
        return a switch
        {
            int i => Math.Abs(i),
            long l => Math.Abs(l),
            BigInteger bi => BigInteger.Abs(bi),
            SchemeFraction f => new SchemeFraction(BigInteger.Abs(f.Num), f.Den),
            double d => Math.Abs(d),
            _ => Math.Abs(Convert.ToDouble(a))
        };
    }

    static object? PFloor(object?[] args)
    {
        var a = args[0];
        if (NumericHelper.Classify(a) <= NumericHelper.NumType.Int) return a;
        if (a is SchemeFraction f)
        {
            var q = f.Num / f.Den;
            if (f.Num >= 0) return NumericHelper.ToLong(q);
            var rem = f.Num % f.Den;
            return NumericHelper.ToLong(rem == 0 ? q : q - 1);
        }
        return Math.Floor(NumericHelper.ToDouble(a));
    }

    static object? PCeiling(object?[] args)
    {
        var a = args[0];
        if (NumericHelper.Classify(a) <= NumericHelper.NumType.Int) return a;
        if (a is SchemeFraction f)
        {
            var q = f.Num / f.Den;
            if (f.Num <= 0) return NumericHelper.ToLong(q);
            var rem = f.Num % f.Den;
            return NumericHelper.ToLong(rem == 0 ? q : q + 1);
        }
        return Math.Ceiling(NumericHelper.ToDouble(a));
    }

    static object? PTruncate(object?[] args)
    {
        var a = args[0];
        if (NumericHelper.Classify(a) <= NumericHelper.NumType.Int) return a;
        if (a is SchemeFraction f) return NumericHelper.ToLong(f.Num / f.Den);
        return Math.Truncate(NumericHelper.ToDouble(a));
    }

    static object? PRound(object?[] args)
    {
        var a = args[0];
        if (NumericHelper.Classify(a) <= NumericHelper.NumType.Int) return a;
        if (a is SchemeFraction f)
        {
            var q = f.Num / f.Den; var r = BigInteger.Abs(f.Num % f.Den);
            var d = f.Den;
            if (r * 2 < d) return NumericHelper.ToLong(q);
            if (r * 2 > d) return NumericHelper.ToLong(q + (f.Num >= 0 ? 1 : -1));
            return NumericHelper.ToLong(q.IsEven ? q : q + (f.Num >= 0 ? 1 : -1));
        }
        return Math.Round(NumericHelper.ToDouble(a), MidpointRounding.ToEven);
    }


    static object? PStringNumber(object?[] args)
    {
        var s = ToStr(args[0]);
        var radix = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 10;
        string prefix = radix switch { 2 => "#b", 8 => "#o", 16 => "#x", _ => "" };
        var full = prefix + s;
        return Reader.AtomParser.ParseAtom(full) is Sym ? Const.FALSE : Reader.AtomParser.ParseAtom(full);
    }

    static object? PEvenQ(object?[] args)
    {
        var x = NumericHelper.ToBigInt(args[0]);
        return x.IsEven ? Const.TRUE : Const.FALSE;
    }

    static object? POddQ(object?[] args)
    {
        var x = NumericHelper.ToBigInt(args[0]);
        return !x.IsEven ? Const.TRUE : Const.FALSE;
    }

    static object? PMax(object?[] args)
    {
        return args.Aggregate((object?)args[0], (best, x) =>
            NumericHelper.Compare(best!, x) >= 0 ? best : x)!;
    }

    static object? PMin(object?[] args)
    {
        return args.Aggregate((object?)args[0], (best, x) =>
            NumericHelper.Compare(best!, x) <= 0 ? best : x)!;
    }

    static object? PGcd(object?[] args)
    {
        BigInteger Gcd(BigInteger a, BigInteger b) => b == 0 ? BigInteger.Abs(a) : Gcd(b, a % b);
        BigInteger Lcm(BigInteger a, BigInteger b) => a == 0 || b == 0 ? 0 : BigInteger.Abs(a * b) / Gcd(a, b);
        object? acc = 0L;
        foreach (var x in args)
        {
            if (acc is SchemeFraction af || x is SchemeFraction)
            {
                var fa = acc is SchemeFraction f1 ? f1 : NumericHelper.ToFraction(acc);
                var fb = x is SchemeFraction f2 ? f2 : NumericHelper.ToFraction(x);
                var num = Gcd(fa.Num, fb.Num);
                var den = Lcm(fa.Den, fb.Den);
                acc = den == 1 ? (num <= long.MaxValue ? (object?)(long)num : num)
                               : new SchemeFraction(num, den);
            }
            else
            {
                var r = Gcd(NumericHelper.ToBigInt(acc), NumericHelper.ToBigInt(x));
                acc = r <= long.MaxValue && r >= long.MinValue ? (object?)(long)r : r;
            }
        }
        return acc!;
    }

    static object? PLcm(object?[] args)
    {
        BigInteger Gcd(BigInteger a, BigInteger b) => b == 0 ? BigInteger.Abs(a) : Gcd(b, a % b);
        BigInteger Lcm(BigInteger a, BigInteger b) => a == 0 || b == 0 ? 0 : BigInteger.Abs(a * b) / Gcd(a, b);
        if (args.Length == 0) return 1L;
        object? acc = args[0];
        for (int i = 1; i < args.Length; i++)
        {
            var x = args[i];
            if (acc is SchemeFraction af || x is SchemeFraction)
            {
                var fa = acc is SchemeFraction f1 ? f1 : NumericHelper.ToFraction(acc);
                var fb = x is SchemeFraction f2 ? f2 : NumericHelper.ToFraction(x);
                var num = Lcm(fa.Num, fb.Num);
                var den = Gcd(fa.Den, fb.Den);
                acc = den == 1 ? (num <= long.MaxValue ? (object?)(long)num : num)
                               : new SchemeFraction(num, den);
            }
            else
            {
                var r = Lcm(NumericHelper.ToBigInt(acc), NumericHelper.ToBigInt(x));
                acc = r <= long.MaxValue ? (long)r : r;
            }
        }
        return acc!;
    }






    static object? PDigitValue(object?[] args)
    {
        var c = ToChar(args[0]);
        if (c >= '0' && c <= '9') return (long)(c - '0');
        if (c >= 'a' && c <= 'f') return (long)(c - 'a' + 10);
        if (c >= 'A' && c <= 'F') return (long)(c - 'A' + 10);
        return Const.FALSE;
    }

    static object? PMakeString(object?[] args)
    {
        var len = NumericHelper.ToInt(args[0]);
        var cp = args.Length > 1 ? AsChar(args[1]) : (int)' ';
        return new SchemeString(Enumerable.Repeat(cp, len));
    }

    static object? PStringList(object?[] args)
    {
        var s = ToStr(args[0]);
        var cells = new List<object?>();
        foreach (var rune in s.EnumerateRunes())
            cells.Add(new SchemeChar(rune.Value));
        return cells.ToCell();
    }

    static object? PListString(object?[] args)
    {
        var chars = new List<int>();
        object? cur = args[0];
        while (cur is Cell c) { chars.Add(AsChar(c.Car)); cur = c.Cdr; }
        return new SchemeString(chars);
    }

    static object? PStringLength(object?[] args)
    {
        if (args[0] is SchemeString ss) return ss.Length;
        int count = 0;
        foreach (var _ in ToStr(args[0]).EnumerateRunes()) count++;
        return count;
    }

    static object? PStringRef(object?[] args)
    {
        if (args[0] is SchemeString ss) return new SchemeChar(ss.Data[NumericHelper.ToInt(args[1])]);
        int idx = NumericHelper.ToInt(args[1]);
        int count = 0;
        foreach (var rune in ToStr(args[0]).EnumerateRunes())
        {
            if (count == idx) return new SchemeChar(rune.Value);
            count++;
        }
        throw new IndexOutOfRangeException();
    }

    static object? PStringSetBang(object?[] args)
    {
        if (args[0] is SchemeString s)
        {
            s.Data[NumericHelper.ToInt(args[1])] = args[2] is SchemeChar sc ? sc.Codepoint : AsChar(args[2]);
            return Const.VOID;
        }
        if (args[0] is string raw)
        {
            if (!MutableStringViews.TryGetValue(raw, out var view))
            {
                view = new SchemeString(raw);
                MutableStringViews[raw] = view;
            }
            view.Data[NumericHelper.ToInt(args[1])] = args[2] is SchemeChar sc ? sc.Codepoint : AsChar(args[2]);
            return Const.VOID;
        }
        throw new Exception("string-set! requires mutable SchemeString");
    }

    static object? PStringCopy(object?[] args)
    {
        if (args.Length == 1)
        {
            if (args[0] is SchemeString ss) return new SchemeString(ss.Data);
            return new SchemeString(ToStr(args[0]));
        }
        var start = NumericHelper.ToInt(args[1]);
        var end = args.Length > 2 ? NumericHelper.ToInt(args[2]) : -1;
        if (args[0] is SchemeString ss2)
        {
            var endIdx = end < 0 ? ss2.Length : end;
            return new SchemeString(ss2.Data.GetRange(start, endIdx - start));
        }
        var s = ToStr(args[0]);
        int count = 0;
        var sb = new StringBuilder();
        foreach (var rune in s.EnumerateRunes())
        {
            if (count >= start)
            {
                if (end >= 0 && count >= end) break;
                sb.Append(rune);
            }
            count++;
        }
        return new SchemeString(sb.ToString());
    }

    static object? PSymbolString(object?[] args)
    {
        if (args[0] is Sym sym) return new SchemeString(sym.Name);
        return new SchemeString(args[0].AsString());
    }

    static object? PStringFillBang(object?[] args)
    {
        var cp = AsChar(args[1]);
        if (args[0] is SchemeString s) { for (int i = 0; i < s.Data.Count; i++) s.Data[i] = cp; }
        return Const.VOID;
    }

    static object? PSubstring(object?[] args)
    {
        var start = NumericHelper.ToInt(args[1]);
        var end = NumericHelper.ToInt(args[2]);
        if (args[0] is SchemeString ss)
            return new SchemeString(ss.Data.GetRange(start, end - start));
        var s = ToStr(args[0]);
        int count = 0;
        var sb = new StringBuilder();
        foreach (var rune in s.EnumerateRunes())
        {
            if (count >= start && count < end) sb.Append(rune);
            count++;
            if (count >= end) break;
        }
        return new SchemeString(sb.ToString());
    }

    static object? PCharEq(object?[] args)
    {
        for (int i = 1; i < args.Length; i++)
            if (AsChar(args[i - 1]) != AsChar(args[i])) return Const.FALSE;
        return Const.TRUE;
    }

    static object? PCharLt(object?[] args)
    {
        for (int i = 1; i < args.Length; i++)
            if (AsChar(args[i - 1]) >= AsChar(args[i])) return Const.FALSE;
        return Const.TRUE;
    }

    static object? PCharGt(object?[] args)
    {
        for (int i = 1; i < args.Length; i++)
            if (AsChar(args[i - 1]) <= AsChar(args[i])) return Const.FALSE;
        return Const.TRUE;
    }

    static object? PCharLe(object?[] args)
    {
        for (int i = 1; i < args.Length; i++)
            if (AsChar(args[i - 1]) > AsChar(args[i])) return Const.FALSE;
        return Const.TRUE;
    }

    static object? PCharGe(object?[] args)
    {
        for (int i = 1; i < args.Length; i++)
            if (AsChar(args[i - 1]) < AsChar(args[i])) return Const.FALSE;
        return Const.TRUE;
    }

    static object? PCharCiEq(object?[] args)
    {
        for (int i = 1; i < args.Length; i++)
        {
            var r1 = new Rune(AsChar(args[i - 1]));
            var r2 = new Rune(AsChar(args[i]));
            if (Rune.ToLowerInvariant(r1) != Rune.ToLowerInvariant(r2)) return Const.FALSE;
        }
        return Const.TRUE;
    }

    static object? PCharAlphabeticQ(object?[] args)
    {
        try { return Rune.IsLetter(new Rune(AsChar(args[0]))) ? Const.TRUE : Const.FALSE; }
        catch { return Const.FALSE; }
    }

    static object? PCharNumericQ(object?[] args)
    {
        try { return Rune.IsDigit(new Rune(AsChar(args[0]))) ? Const.TRUE : Const.FALSE; }
        catch { return Const.FALSE; }
    }

    static object? PCharWhitespaceQ(object?[] args)
    {
        try { return Rune.IsWhiteSpace(new Rune(AsChar(args[0]))) ? Const.TRUE : Const.FALSE; }
        catch { return Const.FALSE; }
    }

    static object? PCharLowerCaseQ(object?[] args)
    {
        try { return Rune.IsLower(new Rune(AsChar(args[0]))) ? Const.TRUE : Const.FALSE; }
        catch { return Const.FALSE; }
    }

    static object? PCharUpperCaseQ(object?[] args)
    {
        try { return Rune.IsUpper(new Rune(AsChar(args[0]))) ? Const.TRUE : Const.FALSE; }
        catch { return Const.FALSE; }
    }

    static object? PMakeVector(object?[] args)
    {
        var n = NumericHelper.ToInt(args[0]);
        var fill = args.Length > 1 ? args[1] : Const.NIL;
        return new SchemeVector(Enumerable.Repeat(fill, n));
    }

    static object? PVectorAppend(object?[] args)
    {
        var all = new List<object?>();
        foreach (var vec in args)
            if (vec is SchemeVector sv) all.AddRange(sv.Data);
        return new SchemeVector(all);
    }

    static object? PMakeBytevector(object?[] args)
    {
        var n = NumericHelper.ToInt(args[0]);
        var fill = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
        var arr = new byte[n];
        for (int i = 0; i < n; i++) arr[i] = (byte)fill;
        return new SchemeBytevector(arr);
    }




    static object? PFold(object?[] args)
    {
        var fn = args[0];
        var acc = args[1];
        object? cur = args[2];
        while (cur is Cell c) { acc = App(fn, c.Car, acc); cur = c.Cdr; }
        return acc;
    }

    static object? PFoldRight(object?[] args)
    {
        var fn = args[0];
        var items = new List<object?>();
        object? cur = args[2];
        while (cur is Cell c) { items.Add(c.Car); cur = c.Cdr; }
        var acc = args[1];
        for (int i = items.Count - 1; i >= 0; i--)
            acc = App(fn, items[i], acc);
        return acc;
    }

    static object? PFind(object?[] args)
    {
        var pred = args[0];
        object? cur = args[1];
        while (cur is Cell c) { if (App(pred, c.Car) is Sym s && s != Const.FALSE) return c.Car; cur = c.Cdr; }
        return Const.FALSE;
    }

    static object? PAny(object?[] args)
    {
        var pred = args[0];
        object? cur = args[1];
        while (cur is Cell c) { var r = App(pred, c.Car); if (r is Sym s && s != Const.FALSE) return r; cur = c.Cdr; }
        return Const.FALSE;
    }

    static object? PEvery(object?[] args)
    {
        var pred = args[0];
        object? cur = args[1];
        while (cur is Cell c) { var r = App(pred, c.Car); if (r is Sym s && s == Const.FALSE) return Const.FALSE; cur = c.Cdr; }
        return Const.TRUE;
    }

    static object? PPartition(object?[] args)
    {
        var pred = args[0];
        var pass = new List<object?>();
        var fail = new List<object?>();
        object? cur = args[1];
        while (cur is Cell c) { if (App(pred, c.Car) is Sym s && s != Const.FALSE) pass.Add(c.Car); else fail.Add(c.Car); cur = c.Cdr; }
        return new Cell(pass.ToCell(), new Cell(fail.ToCell(), Const.NIL));
    }

    static object? PTake(object?[] args)
    {
        var result = new List<object?>();
        object? cur = args[0]; int i = 0; int n = NumericHelper.ToInt(args[1]);
        while (cur is Cell c && i < n) { result.Add(c.Car); cur = c.Cdr; i++; }
        return result.ToCell();
    }

    static object? PDrop(object?[] args)
    {
        object? cur = args[0]; int i = 0; int n = NumericHelper.ToInt(args[1]);
        while (cur is Cell c && i < n) { cur = c.Cdr; i++; }
        return cur;
    }

    static object? PTakeWhile(object?[] args)
    {
        var pred = args[0];
        var result = new List<object?>();
        object? cur = args[1];
        while (cur is Cell c && App(pred, c.Car) is Sym s && s != Const.FALSE)
        { result.Add(c.Car); cur = c.Cdr; }
        return result.ToCell();
    }

    static object? PDropWhile(object?[] args)
    {
        var pred = args[0];
        object? cur = args[1];
        while (cur is Cell c && App(pred, c.Car) is Sym s && s != Const.FALSE) cur = c.Cdr;
        return cur;
    }

    static object? PSpan(object?[] args)
    {
        var pred = args[0];
        var pass = new List<object?>();
        object? cur = args[1];
        while (cur is Cell c && App(pred, c.Car) is Sym s && s != Const.FALSE)
        { pass.Add(c.Car); cur = c.Cdr; }
        return new Cell(pass.ToCell(), new Cell(cur, Const.NIL));
    }

    static object? PBreak(object?[] args)
    {
        var pred = args[0];
        var before = new List<object?>();
        object? cur = args[1];
        while (cur is Cell c && App(pred, c.Car) is Sym s && s == Const.FALSE)
        { before.Add(c.Car); cur = c.Cdr; }
        return new Cell(before.ToCell(), new Cell(cur, Const.NIL));
    }

    static object? PIota(object?[] args)
    {
        var n = NumericHelper.ToInt(args[0]);
        var start = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
        var step = args.Length > 2 ? NumericHelper.ToInt(args[2]) : 1;
        var result = new List<object?>();
        long cur = start;
        for (int i = 0; i < n; i++) { result.Add(cur); cur += step; }
        return result.ToCell();
    }




    static object? PRead(object?[] args)
    {
        if (args.Length > 0 && args[0] is ITuple t && t.Length >= 3 && t[0] is string s0 && s0 == "port" && t[1] is "input")
        {
            if (t[2] is StreamReader sr)
            {
                var lineSr = sr.ReadLine();
                if (lineSr is null) return Const.EOF;
                var exprRead1 = Reader.Parser.Read(lineSr);
                return exprRead1 ?? Const.EOF;
            }
            if (t[2] is StringPort sp)
            {
                if (sp.Pos >= sp.Data.Length) return Const.EOF;
                var remaining = sp.Data[sp.Pos..];
                var tokList = Reader.Tokenizer.TokenizeWithPos(remaining);
                if (tokList.Count == 0) { sp.Pos = sp.Data.Length; return Const.EOF; }
                var tokens = tokList.Select(t => t.text).ToList();
                var reader = new Reader.ReaderState(tokens);
                var expr = Reader.Parser.ParseExpr(reader);
                int consumed = reader.Pos;
                if (consumed > 0)
                {
                    var lastTok = tokList[consumed - 1];
                    int charEnd = lastTok.pos + lastTok.text.Length;
                    while (charEnd < remaining.Length && char.IsWhiteSpace(remaining[charEnd]))
                        charEnd++;
                    sp.Pos += charEnd;
                }
                return expr;
            }
        }
        var line = Console.ReadLine();
        if (line is null) return Const.EOF;
        var exprRead3 = Reader.Parser.Read(line);
        return exprRead3 ?? Const.EOF;
    }

    static object? PReadLine(object?[] args)
    {
        if (args.Length > 0 && args[0] is ITuple t && t.Length >= 3 && t[0] is string s0 && s0 == "port" && t[1] is "input")
        {
            if (t[2] is StreamReader sr)
            {
                var line2 = sr.ReadLine();
                return line2 is null ? Const.EOF : line2;
            }
            if (t[2] is StringBuilder sb)
            {
                var s = sb.ToString();
                if (string.IsNullOrEmpty(s)) return Const.EOF;
                var idx = s.IndexOf('\n');
                var line2 = idx >= 0 ? s[..idx] : s;
                if (idx >= 0) sb.Remove(0, idx + 1); else sb.Clear();
                return line2;
            }
            if (t[2] is StringPort sp)
            {
                var s = sp.Data[sp.Pos..];
                if (string.IsNullOrEmpty(s)) return Const.EOF;
                var idx = s.IndexOf('\n');
                var line = idx >= 0 ? s[..idx] : s;
                sp.Pos += idx >= 0 ? idx + 1 : s.Length;
                return line;
            }
        }
        var line3 = Console.ReadLine();
        return line3 is null ? Const.EOF : line3;
    }

    static object? PReadString(object?[] args)
    {
        var n = NumericHelper.ToInt(args[0]);
        if (n <= 0) return new SchemeString("");
        if (args.Length > 1 && args[1] is System.Runtime.CompilerServices.ITuple port && port.Length >= 3 && port[0] is "port" && port[1] is "input")
        {
            if (port[2] is StreamReader sr) { var buf = new char[n]; var read = sr.ReadBlock(buf, 0, n); return read > 0 ? new string(buf, 0, read) : Const.EOF; }
            if (port[2] is StringBuilder sb) { var s = sb.ToString(); var take = Math.Min(n, s.Length); if (take == 0) return Const.EOF; var r = s[..take]; sb.Remove(0, take); return r; }
            if (port[2] is StringPort sp) { var take = Math.Min(n, sp.Data.Length - sp.Pos); if (take <= 0) return Const.EOF; var r = sp.Data.Substring(sp.Pos, take); sp.Pos += take; return r; }
        }
        var buf2 = new char[n];
        var read2 = Console.In.ReadBlock(buf2, 0, n);
        return read2 > 0 ? new string(buf2, 0, read2) : Const.EOF;
    }

    static object? PPeekChar(object?[] args)
    {
        if (args.Length > 0 && args[0] is System.Runtime.CompilerServices.ITuple port)
        {
            if (port.Length >= 3 && port[0] is "port" && port[1] is "input")
            {
                if (port[2] is StreamReader sr) { var c = sr.Peek(); return c == -1 ? Const.EOF : new SchemeChar(c); }
                if (port[2] is StringBuilder sb) { var s = sb.ToString(); if (s.Length == 0) return Const.EOF; if (s.Length >= 2 && char.IsHighSurrogate(s[0])) return new SchemeChar(char.ConvertToUtf32(s[0], s[1])); return new SchemeChar((int)s[0]); }
                if (port[2] is StringPort sp) { if (sp.Pos >= sp.Data.Length) return Const.EOF; if (sp.Pos + 1 < sp.Data.Length && char.IsHighSurrogate(sp.Data[sp.Pos])) return new SchemeChar(char.ConvertToUtf32(sp.Data[sp.Pos], sp.Data[sp.Pos + 1])); return new SchemeChar((int)sp.Data[sp.Pos]); }
            }
        }
        var cin = Console.In.Peek(); return cin == -1 ? Const.EOF : new SchemeChar(cin);
    }

    static object? PReadChar(object?[] args)
    {
        if (args.Length > 0 && args[0] is System.Runtime.CompilerServices.ITuple port)
        {
            if (port.Length >= 3 && port[0] is "port" && port[1] is "input")
            {
                if (port[2] is StreamReader sr) { int first = sr.Read(); if (first == -1) return Const.EOF; if (char.IsHighSurrogate((char)first)) { int second = sr.Read(); if (second != -1 && char.IsLowSurrogate((char)second)) return new SchemeChar(char.ConvertToUtf32((char)first, (char)second)); } return new SchemeChar(first); }
                if (port[2] is StringBuilder sb) { var s = sb.ToString(); if (s.Length == 0) return Const.EOF; if (s.Length >= 2 && char.IsHighSurrogate(s[0])) { int cp = char.ConvertToUtf32(s[0], s[1]); sb.Remove(0, 2); return new SchemeChar(cp); } var fc = s[0]; sb.Remove(0, 1); return new SchemeChar((int)fc); }
                if (port[2] is StringPort sp) { if (sp.Pos >= sp.Data.Length) return Const.EOF; if (sp.Pos + 1 < sp.Data.Length && char.IsHighSurrogate(sp.Data[sp.Pos])) { int cp = char.ConvertToUtf32(sp.Data[sp.Pos], sp.Data[sp.Pos + 1]); sp.Pos += 2; return new SchemeChar(cp); } return new SchemeChar((int)sp.Data[sp.Pos++]); }
            }
        }
        int cinFirst = Console.In.Read(); if (cinFirst == -1) return Const.EOF; if (char.IsHighSurrogate((char)cinFirst)) { int cinSecond = Console.In.Read(); if (cinSecond != -1 && char.IsLowSurrogate((char)cinSecond)) return new SchemeChar(char.ConvertToUtf32((char)cinFirst, (char)cinSecond)); } return new SchemeChar(cinFirst);
    }

    static object? PPortPosition(object?[] args)
    {
        if (args[0] is ITuple t && t.Length >= 3 && t[0] is "port" && t[1] is "input" && t[2] is StringPort sp)
            return (long)sp.Pos;
        if (args[0] is ITuple tb && tb.Length >= 3 && tb[0] is "port" && tb[1] is "input" && tb[2] is BytePort bp)
            return (long)bp.Pos;
        return Const.FALSE;
    }

    static object? PSetPortPositionBang(object?[] args)
    {
        if (args[0] is ITuple t && t.Length >= 3 && t[0] is "port" && t[1] is "input" && t[2] is StringPort sp)
        {
            sp.SetPos(NumericHelper.ToInt(args[1]));
            return Const.VOID;
        }
        if (args[0] is ITuple tb && tb.Length >= 3 && tb[0] is "port" && tb[1] is "input" && tb[2] is BytePort bp)
        {
            bp.Pos = Math.Clamp(NumericHelper.ToInt(args[1]), 0, bp.Data.Length);
            return Const.VOID;
        }
        return Const.FALSE;
    }

    static object? PGetOutputString(object?[] args)
    {
        var p = args[0];
        if (p is ITuple it && it.Length >= 3 && it[0] is "port" && it[1] is "output" && it[2] is StringBuilder sb)
            return sb.ToString();
        return "";
    }

    static object? PGetOutputBytevector(object?[] args)
    {
        if (args[0] is ITuple it && it.Length >= 3 && it[0] is "port" && it[2] is BytePort bp)
            return new SchemeBytevector([.. bp.Data]);
        return new SchemeBytevector(Array.Empty<byte>());
    }

    static object? CallWithStringOutput(object? proc)
    {
        var port = MakePort("output", new StringBuilder());
        App(proc, port);
        var portTuple = (ITuple)port!;
        var output = portTuple[2] as StringBuilder;
        return new SchemeString(output!.ToString());
    }

    static object? CallWithBytevectorOutput(object? proc)
    {
        var port = MakePort("output", new BytePort(Array.Empty<byte>()));
        App(proc, port);
        var portTuple = (ITuple)port!;
        var output = portTuple[2] as BytePort;
        return new SchemeBytevector([.. output!.Data]);
    }

    static object? PCallWithInputFile(object?[] args)
    {
        var path = ToStr(args[0]);
        var proc = args[1];
        using var sr = new StreamReader(path);
        var port = MakePort("input", sr);
        return App(proc, port);
    }

    static object? PCallWithOutputFile(object?[] args)
    {
        var path = ToStr(args[0]);
        var proc = args[1];
        using var sw = new StreamWriter(path);
        var port = MakePort("output", sw);
        return App(proc, port);
    }

    static object? PWithInputFromFile(object?[] args)
    {
        var path = ToStr(args[0]);
        var thunk = args[1];
        using var sr = new StreamReader(path);
        var oldIn = Console.In;
        Console.SetIn(sr);
        try { return App(thunk); }
        finally { Console.SetIn(oldIn); }
    }

    static object? PWithOutputToFile(object?[] args)
    {
        var path = ToStr(args[0]);
        var thunk = args[1];
        using var sw = new StreamWriter(path);
        var oldOut = Console.Out;
        Console.SetOut(sw);
        try { return App(thunk); }
        finally { Console.SetOut(oldOut); }
    }

    static object? PCurrentOutputPort(object?[] args)
    {
        if (args.Length > 0)
        {
            var old = Console.Out;
            if (args[0] is ITuple t && t.Length >= 3 && t[0] is string s0 && s0 == "port" && t[1] is "output")
            {
                if (t[2] is StreamWriter sw)
                    Console.SetOut(sw);
                else if (t[2] is StringBuilder sb)
                    Console.SetOut(new StringWriter(sb));
                else if (t[2] is TextWriter tw)
                    Console.SetOut(tw);
            }
            return MakePort("output", old);
        }
        return MakePort("output", Console.Out);
    }


    static object? PRaise(object?[] args)
    {
        var obj = args[0];
        if (obj is SchemeException se) throw se;
        throw new SchemeException(obj);
    }

    static object? PSetBoxBang(object?[] args)
    {
        var b = args[0]; var x = args[1];
        if (b is ValueTuple<string, object?> t && t.Item1 == "box")
        {
            b.GetType().GetField("Item2")!.SetValue(b, x);
        }
        return Const.VOID;
    }

    static object? PCallWithValues(object?[] args)
    {
        var producer = args[0];
        var consumer = args[1];
        var vals = App(producer);
        if (vals is SchemeVector sv) return App(consumer, [.. sv.Data]);
        if (vals is Cell c && c.Cdr is not Cell && c.Cdr is not Nil) return App(consumer, [c.Car, c.Cdr]);
        if (vals is Cell c2 && c2.Cdr is Cell c2r && c2r.Cdr is Nil)
        {
            if (c2.Car is Cell or Nil || c2r.Car is Cell or Nil)
                return App(consumer, [c2.Car, c2r.Car]);
        }
        return App(consumer, vals);
    }

    static object? PApply(object?[] args)
    {
        var fn = args[0];
        var items = new List<object?>();
        for (int i = 1; i < args.Length; i++)
        {
            var cur = args[i];
            if (cur is Cell c)
            {
                while (cur is Cell cc) { items.Add(cc.Car); cur = cc.Cdr; }
            }
            else if (cur is not Nil)
            {
                items.Add(cur);
            }
        }
        return App(fn, [.. items]);
    }

    static object? PForce(object?[] args)
    {
        var x = args[0];
        if (x is Promise p)
        {
            if (!p.Forced)
            {
                var thunkResult = p.Thunk!();
                if (thunkResult is LambdaProc lp)
                {
                    var nenv = new Env(lp.ClosureEnv, lp.Params.Count);
                    var r = Evaluator.SeqTailCall(lp.Body, nenv);
                    while (r is TailCall tcr) r = Evaluator.EvalCore(tcr.Expr, tcr.Env);
                    p.Val = r;
                }
                else
                {
                    p.Val = thunkResult;
                }
                p.Forced = true;
            }
            return p.Val;
        }
        return x;
    }

    static object? PCallCc(object?[] args)
    {
        var receiver = args[0];
        object? result = null;
        var myId = ++ContCounter.Value;
        object? captured = null;
        
        // If receiver is a CompiledLambda or LambdaProc with compiled version, 
        // execute trampoline within try/catch to ensure ContinuationEscape is caught
        // (JitRuntime.Invoke doesn't have the handler)
        if (receiver is Miniscm.Compiler.CompiledLambda cl)
        {
            var escape = new Func<object?[], object?>(_ => { captured = _[0]; throw new ContinuationEscape(captured, myId); });
            try 
            { 
                result = InvokeContTrampoline(cl, [escape], cl.Env); 
            }
            catch (ContinuationEscape ce) { if (ce.Id != myId) throw; result = ce.Val; }
            return result;
        }
        if (receiver is LambdaProc lp && lp.CompiledVersion is Miniscm.Compiler.CompiledLambda cl2)
        {
            var escape = new Func<object?[], object?>(_ => { captured = _[0]; throw new ContinuationEscape(captured, myId); });
            try 
            { 
                result = InvokeContTrampoline(cl2, [escape], lp.ClosureEnv); 
            }
            catch (ContinuationEscape ce) { if (ce.Id != myId) throw; result = ce.Val; }
            return result;
        }
        
        try { result = App(receiver, new Func<object?[], object?>(_ => { captured = _[0]; throw new ContinuationEscape(captured, myId); })); }
        catch (ContinuationEscape ce) { if (ce.Id != myId) throw; result = ce.Val; }
        return result;
    }

    static object? PCallWithCurrentContinuation(object?[] args)
    {
        var receiver = args[0];
        object? result = null;
        var myId = ++ContCounter.Value;
        object? captured = null;
        
        // If receiver is a CompiledLambda or LambdaProc with compiled version, 
        // execute trampoline within try/catch
        if (receiver is Miniscm.Compiler.CompiledLambda cl)
        {
            var escape = new Func<object?[], object?>(_ => { captured = _[0]; throw new ContinuationEscape(captured, myId); });
            try 
            { 
                result = InvokeContTrampoline(cl, [escape], cl.Env); 
            }
            catch (ContinuationEscape ce) { if (ce.Id != myId) throw; result = ce.Val; }
            return result;
        }
        if (receiver is LambdaProc lp && lp.CompiledVersion is Miniscm.Compiler.CompiledLambda cl2)
        {
            var escape = new Func<object?[], object?>(_ => { captured = _[0]; throw new ContinuationEscape(captured, myId); });
            try 
            { 
                result = InvokeContTrampoline(cl2, [escape], lp.ClosureEnv); 
            }
            catch (ContinuationEscape ce) { if (ce.Id != myId) throw; result = ce.Val; }
            return result;
        }
        
        try { result = App(receiver, new Func<object?[], object?>(_ => { captured = _[0]; throw new ContinuationEscape(captured, myId); })); }
        catch (ContinuationEscape ce) { if (ce.Id != myId) throw; result = ce.Val; }
        return result;
    }

    // Execute continuation trampoline within call/cc's try/catch context
    // Handles CompiledLambda, LambdaProc, and Delegate continuations
    private static object? InvokeContTrampoline(Miniscm.Compiler.CompiledLambda cont, object?[] args, Env env)
    {
        object? procVal = cont;
        object?[] argsVal = args;
        Env curEnv = env;
        
        while (true)
        {
            if (procVal is Func<object?[], object?> fn)
                return fn(argsVal);
            if (procVal is Miniscm.Compiler.CompiledLambda cv)
            {
                var r = cv.Invoke(cv.Env, argsVal);
                if (r is not TailCall tc) return r;
                if (!Miniscm.Compiler.JitRuntime.TryUnpackTailCall(tc, out var u))
                    return Evaluator.EvalCore(tc.Expr, tc.Env);
                (procVal, argsVal, curEnv) = u;
                continue;
            }
            if (procVal is LambdaProc lp)
            {
                Evaluator.EnsureCompiled(lp);
                if (lp.CompiledVersion is Miniscm.Compiler.CompiledLambda cl)
                {
                    var r = cl.Invoke(lp.ClosureEnv, argsVal);
                    if (r is not TailCall tc) return r;
                    if (!Miniscm.Compiler.JitRuntime.TryUnpackTailCall(tc, out var u))
                        return Evaluator.EvalCore(tc.Expr, tc.Env);
                    (procVal, argsVal, curEnv) = u;
                    continue;
                }
                var nenv = new Env(lp.ClosureEnv, lp.Params.Count);
                Evaluator.BindParams(lp.Params, argsVal, nenv);
                var r2 = Evaluator.SeqTailCall(lp.Body, nenv);
                if (r2 is not TailCall tc2) return r2;
                if (!Miniscm.Compiler.JitRuntime.TryUnpackTailCall(tc2, out var u2))
                    return Evaluator.EvalCore(tc2.Expr, tc2.Env);
                (procVal, argsVal, curEnv) = u2;
                continue;
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
                    if (r is not TailCall tc) return r;
                    if (!Miniscm.Compiler.JitRuntime.TryUnpackTailCall(tc, out var u))
                        return Evaluator.EvalCore(tc.Expr, tc.Env);
                    (procVal, argsVal, curEnv) = u;
                    continue;
                }
            }
            throw new Exception($"not callable: {Printer.Format(procVal)}");
        }
    }

    static object? PLoad(object?[] args)
    {
        var p = ToStr(args[0]);
        try
        {
            var src = File.ReadAllText(p);
            var exprs = Reader.Parser.ReadAll(src);
            int n = 0;
            foreach (var expr in exprs)
            {
                try { Evaluator.Eval(expr, Evaluator.GlobalEnv); n++; }
                catch { }
            }
            return n;
        }
        catch { return 0; }
    }



    static object? PHashTableRef(object?[] args)
    {
        var ht = (Dictionary<object, object?>)args[0]!;
        var key = args[1] ?? throw new Exception("hash-table-ref: null key");
        return ht.TryGetValue(key, out var v) ? v
            : args.Length > 2 ? args[2]
            : Const.FALSE; // 对齐 Python: 无默认值时缺失返回 #f (bimap 等依赖)
    }

    static object? PHashTableSetBang(object?[] args)
    {
        var ht = (Dictionary<object, object?>)args[0]!;
        ht[args[1] ?? throw new Exception("hash-table-set!: null key")] = args[2];
        return Const.VOID;
    }

    static object? PHashTableUpdateBang(object?[] args)
    {
        var ht = (Dictionary<object, object?>)args[0]!;
        var key = args[1]!;
        if (!ht.TryGetValue(key, out var value)) value = args.Length > 3 ? args[3] : Const.FALSE;
        ht[key] = App(args[2], value);
        return Const.VOID;
    }

    static object? PHashTableMergeBang(object?[] args)
    {
        var target = (Dictionary<object, object?>)args[0]!;
        for (var i = 1; i < args.Length; i++)
            foreach (var pair in (Dictionary<object, object?>)args[i]!) target[pair.Key] = pair.Value;
        return target;
    }

    static object? PHashTableWalk(object?[] args)
    {
        var fn = args[0];
        foreach (var pair in (Dictionary<object, object?>)args[1]!) App(fn, pair.Key, pair.Value);
        return Const.VOID;
    }

    static object? PHashTableDeleteBang(object?[] args)
    {
        var ht = (Dictionary<object, object?>)args[0]!;
        ht.Remove(args[1] ?? throw new Exception("hash-table-delete!: null key"));
        return Const.VOID;
    }

    static object? PHashTableContainsQ(object?[] args)
    {
        var ht = (Dictionary<object, object?>)args[0]!;
        return ht.ContainsKey(args[1] ?? throw new Exception("hash-table-contains?: null key")) ? Const.TRUE : Const.FALSE;
    }

    static object? PHashTableCount(object?[] args)
    {
        var ht = (Dictionary<object, object?>)args[0]!;
        return (long)ht.Count;
    }

    static object? PHashTableClearBang(object?[] args)
    {
        var ht = (Dictionary<object, object?>)args[0]!;
        ht.Clear();
        return Const.VOID;
    }

    static object? PConstantly(object?[] args)
    {
        var x = args[0];
        return (Func<object?[], object?>)(_ => x);
    }

    static object? PComplement(object?[] args)
    {
        var pred = args[0];
        return (Func<object?[], object?>)(x => App(pred, x[0]) is Sym s && s != Const.FALSE ? Const.FALSE : Const.TRUE);
    }

    static object? PFlip(object?[] args)
    {
        var fn = args[0];
        return (Func<object?[], object?>)(a => App(fn, a[1], a[0]));
    }

    static object? PDefinedQ(object?[] args)
    {
        var name = (args[0] as Sym)?.Name ?? args[0]?.ToString() ?? "";
        return Evaluator.GlobalEnv.LookupSilent(name, null) is not null ? Const.TRUE : Const.FALSE;
    }

    static object? PInexactExact(object?[] args)
    {
        var x = args[0];
        if (x is int or long or BigInteger) return x;
        if (x is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
                throw new Exception("cannot convert infinity/NaN to exact");
            var f = NumericHelper.ToFraction(x);
            if (f.Den == 1)
            {
                var n = f.Num;
                return n <= long.MaxValue && n >= long.MinValue ? (long)n : n;
            }
            return f;
        }
        if (x is SchemeFraction) return x;
        if (x is Complex c)
        {
            if (c.Imaginary != 0)
                throw new Exception("cannot convert complex with non-zero imaginary part to exact");
            return NumericHelper.ToFraction(c.Real);
        }
        return x;
    }

    static object? PCompose(object?[] args)
    {
        if (args.Length == 0) return new Func<object?[], object?>(x => x.Length > 0 ? x[0] : Const.VOID);
        var fns = args.ToList();
        return new Func<object?[], object?>(callArgs =>
        {
            var r = App(fns[^1], callArgs);
            for (int i = fns.Count - 2; i >= 0; i--)
                r = App(fns[i], r);
            return r;
        });
    }

    static object? PArithmeticShift(object?[] args)
    {
        var a = NumericHelper.ToLong(args[0]);
        var b = NumericHelper.ToInt(args[1]);
        return b >= 0 ? a << b : a >> (-b);
    }

    static object? PBitwiseIf(object?[] args)
    {
        var mask = NumericHelper.ToLong(args[0]);
        var t = NumericHelper.ToLong(args[1]);
        var e = NumericHelper.ToLong(args[2]);
        return (mask & t) | (~mask & e);
    }

    static object? PBitwiseLength(object?[] args)
    {
        var n = NumericHelper.ToBigInt(args[0]);
        if (n >= 0) return (long)n.GetBitLength();
        if (n == -1) return 0L;
        return (long)(~n).GetBitLength() - 1;
    }

    static object? PBitwiseCount(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        n = n < 0 ? -n - 1 : n;
        long count = 0;
        while (n != 0) { count += n & 1; n >>= 1; }
        return count;
    }

    static object? PBitCount(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        n = n < 0 ? -n - 1 : n;
        long count = 0;
        while (n != 0) { count += n & 1; n >>= 1; }
        return count;
    }

    static object? PIntegerLength(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        if (n == 0) return 0L;
        return (long)(Math.Floor(Math.Log(n < 0 ? -n : n, 2)) + 1);
    }

    static object? PFirstSetBit(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        if (n == 0) return -1L;
        long i = 0;
        while ((n & 1) == 0) { n >>= 1; i++; }
        return i;
    }

    static object? PBitwiseShift(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var cnt = NumericHelper.ToInt(args[1]);
        return cnt >= 0 ? n << cnt : n >> (-cnt);
    }

    static object? PBitShift(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var cnt = NumericHelper.ToInt(args[1]);
        return cnt >= 0 ? n << cnt : n >> (-cnt);
    }

    static object? PBitwiseArithmeticShift(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var cnt = NumericHelper.ToInt(args[1]);
        return cnt >= 0 ? n << cnt : n >> (-cnt);
    }

    static object? PBitwiseArithmeticShiftRight(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        return n >> NumericHelper.ToInt(args[1]);
    }

    static object? PBitwiseReverseBitField(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var start = NumericHelper.ToInt(args[1]);
        var end = NumericHelper.ToInt(args[2]);
        long result = n;
        for (int i = start; i < (start + end) / 2; i++)
        {
            var j = end - 1 - (i - start);
            var bi = (n >> i) & 1;
            var bj = (n >> j) & 1;
            if (bi != bj)
            {
                result ^= (1L << i);
                result ^= (1L << j);
            }
        }
        return result;
    }

    static object? PBitwiseRotate(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var cnt = NumericHelper.ToInt(args[1]);
        var len = NumericHelper.ToInt(args[2]);
        if (len == 0) return n;
        cnt %= len; if (cnt < 0) cnt += len;
        var mask = (1L << len) - 1;
        n &= mask;
        return ((n << cnt) | (n >> (len - cnt))) & mask;
    }

    static object? PBitwiseRotateBitField(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var cnt = NumericHelper.ToInt(args[1]);
        var start = NumericHelper.ToInt(args[2]);
        var end = NumericHelper.ToInt(args[3]);
        var len = end - start;
        if (len <= 0) return n;
        cnt %= len; if (cnt < 0) cnt += len;
        var mask = ((1L << len) - 1) << start;
        var field = (n >> start) & ((1L << len) - 1);
        var rotated = ((field << cnt) | (field >> (len - cnt))) & ((1L << len) - 1);
        return (n & ~mask) | (rotated << start);
    }

    static object? PBitwiseCopyBit(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var i = NumericHelper.ToInt(args[1]);
        var v = args[2] is Sym s && s.Name == "#t" ? 1L : args[2] is Sym ? 0L : NumericHelper.ToLong(args[2]);
        return v != 0 ? (n | (1L << i)) : (n & ~(1L << i));
    }

    static object? PCopyBit(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var i = NumericHelper.ToInt(args[1]);
        var v = args[2] is Sym s && s.Name == "#t" ? 1L : args[2] is Sym ? 0L : NumericHelper.ToLong(args[2]);
        return v != 0 ? (n | (1L << i)) : (n & ~(1L << i));
    }

    static object? PBitwiseCopyBitField(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var start = NumericHelper.ToInt(args[1]);
        var end = NumericHelper.ToInt(args[2]);
        var newVal = NumericHelper.ToLong(args[3]);
        var len = end - start;
        if (len <= 0) return n;
        var mask = ((1L << len) - 1) << start;
        return (n & ~mask) | ((newVal << start) & mask);
    }

    static object? PBitwiseBitField(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var start = NumericHelper.ToInt(args[1]);
        var end = NumericHelper.ToInt(args[2]);
        var len = end - start;
        if (len <= 0) return 0L;
        return (n >> start) & ((1L << len) - 1);
    }

    static object? PBitField(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var start = NumericHelper.ToInt(args[1]);
        var end = NumericHelper.ToInt(args[2]);
        var len = end - start;
        if (len <= 0) return 0L;
        return (n >> start) & ((1L << len) - 1);
    }

    static object? PNumerator(object?[] args)
    {
        if (args[0] is SchemeFraction f)
        {
            var n = f.Num;
            return n <= long.MaxValue && n >= long.MinValue ? (long)n : n;
        }
        return args[0] is int or long or BigInteger ? args[0] : NumericHelper.ToLong(args[0]);
    }

    static object? PDenominator(object?[] args)
    {
        if (args[0] is SchemeFraction f)
        {
            var d = f.Den;
            return d <= long.MaxValue && d >= long.MinValue ? (long)d : d;
        }
        return 1L;
    }

    static object? PRationalize(object?[] args)
    {
        if (NumericHelper.ToDouble(args[1]) == 0 && args[0] is double d)
        {
            var exact = NumericHelper.ToFraction(d);
            return new SchemeFraction(exact.Num, exact.Den);
        }
        var x = NumericHelper.ToFraction(args[0]);
        var eps = NumericHelper.ToFraction(args[1]);
        var lo = new SchemeFraction(x.Num * eps.Den - eps.Num * x.Den, x.Den * eps.Den);
        var hi = new SchemeFraction(x.Num * eps.Den + eps.Num * x.Den, x.Den * eps.Den);
        var (l, h) = lo.Num * hi.Den <= hi.Num * lo.Den ? (lo, hi) : (hi, lo);
        var r = FindSimplestInRange(l, h);
        if (r.Den == 1)
        {
            var n = r.Num;
            return n <= long.MaxValue && n >= long.MinValue ? (long)n : n;
        }
        return r;
    }

    static object? PExactIntegerSqrt(object?[] args)
    {
        var n = NumericHelper.ToBigInt(args[0]);
        var s = (long)Math.Floor(Math.Sqrt((double)(n <= 1000000 ? (long)n : 0)));
        if (n > 1000000) s = (long)Math.Floor(Math.Pow(10.0, (int)BigInteger.Log10(n) / 2 + 1));
        var bigS = new BigInteger(s);
        while ((bigS + 1) * (bigS + 1) <= n) bigS++;
        while (bigS * bigS > n) bigS--;
        var retS = bigS <= long.MaxValue && bigS >= long.MinValue ? (long)bigS : bigS;
        var rem = n - bigS * bigS;
        var retR = rem <= long.MaxValue && rem >= long.MinValue ? (long)rem : rem;
        return new SchemeVector([retS, retR]);
    }

    static object? PAngle(object?[] args)
    {
        var z = args[0];
        if (z is Complex c) return Math.Atan2(c.Imaginary, c.Real);
        return NumericHelper.Compare(z, 0L) >= 0 ? 0.0 : Math.PI;
    }

    static object? PRealPart(object?[] args)
    {
        var z = args[0];
        if (z is Complex c) return c.Real;
        if (NumericHelper.Classify(z) <= NumericHelper.NumType.Int) return z;
        if (z is double d) return d;
        if (z is SchemeFraction f) return f.ToDouble();
        return z;
    }

    static object? PImagPart(object?[] args)
    {
        var z = args[0];
        if (z is Complex c) return c.Imaginary;
        return 0.0;
    }

    static object? PMakePolar(object?[] args)
    {
        var r = Convert.ToDouble(args[0]);
        var theta = Convert.ToDouble(args[1]);
        return new Complex(r * Math.Cos(theta), r * Math.Sin(theta));
    }

    static object? PMagnitude(object?[] args)
    {
        var z = args[0];
        if (z is Complex c) return c.Magnitude;
        if (z is long l) return Math.Abs(l);
        if (z is int i) return Math.Abs((long)i);
        if (z is BigInteger bi) return bi < 0 ? -bi : bi;
        if (z is SchemeFraction f) return Math.Abs(f.ToDouble());
        return Math.Abs(NumericHelper.ToDouble(z));
    }

    static object? PStreamCar(object?[] args)
    {
        var s = args[0];
        return s is Cell c ? c.Car : s;
    }

    static object? PStreamCdr(object?[] args)
    {
        var s = args[0];
        if (s is Cell c && c.Cdr is Promise p)
            return Evaluator.Eval(new Cell(Sym.Intern("force"), new Cell(c.Cdr, Const.NIL)), Evaluator.GlobalEnv);
        return s is Cell c2 ? c2.Cdr : s;
    }

    static object? PStreamRef(object?[] args)
    {
        var s = args[0];
        var n = NumericHelper.ToInt(args[1]);
        var cur = s;
        for (int i = 0; i < n; i++)
        {
            if (cur is Cell c && c.Cdr is Promise)
                cur = Evaluator.Eval(new Cell(Sym.Intern("force"), new Cell(c.Cdr, Const.NIL)), Evaluator.GlobalEnv);
            else if (cur is Cell c2)
                cur = c2.Cdr;
            else
                throw new Exception("stream-ref: out of bounds");
        }
        return cur is Cell c3 ? c3.Car : Const.NIL;
    }

    static object? PStreamMap(object?[] args)
    {
        var f = args[0];
        var s = args[1];
        object? Advance(object? s) => s is Promise p
            ? Evaluator.Eval(new Cell(Sym.Intern("force"), new Cell(s, Const.NIL)), Evaluator.GlobalEnv)
            : s;
        object? cur = s;
        Func<object?> step = null!;
        step = () =>
        {
            if (cur is Nil) return Const.NIL;
            if (cur is not Cell c) return Const.NIL;
            var mapped = App(f, c.Car);
            cur = Advance(c.Cdr);
            return new Cell(mapped, new Promise(step));
        };
        return step();
    }

    static object? PStreamFilter(object?[] args)
    {
        var pred = args[0];
        var s = args[1];
        object? cur = s;
        Func<object?> step = null!;
        object? Advance(object? s) => s is Promise p
            ? Evaluator.Eval(new Cell(Sym.Intern("force"), new Cell(s, Const.NIL)), Evaluator.GlobalEnv)
            : s;
        step = () =>
        {
            while (true)
            {
                if (cur is Nil) return Const.NIL;
                if (cur is not Cell c) return Const.NIL;
                if (App(pred, c.Car) == Const.TRUE)
                {
                    cur = Advance(c.Cdr);
                    return new Cell(c.Car, new Promise(step));
                }
                cur = Advance(c.Cdr);
            }
        };
        return step();
    }

    static object? PStreamTake(object?[] args)
    {
        var s = args[0];
        var n = NumericHelper.ToInt(args[1]);
        var items = new List<object?>();
        var cur = s;
        for (int i = 0; i < n; i++)
        {
            if (cur is Cell c)
            {
                items.Add(c.Car);
                cur = c.Cdr is Promise p
                    ? Evaluator.Eval(new Cell(Sym.Intern("force"), new Cell(c.Cdr, Const.NIL)), Evaluator.GlobalEnv)
                    : c.Cdr;
            }
            else break;
        }
        return items.ToCell();
    }

    static object? PDynamicWind(object?[] args)
    {
        var before = args[0];
        var thunk = args[1];
        var after = args[2];
        App(before);
        try { return App(thunk); }
        finally { App(after); }
    }

    static object? PWithExceptionHandler(object?[] args)
    {
        var handler = args[0];
        var thunk = args[1];
        try { return App(thunk); }
        catch (SchemeException se) { return App(handler, se.Val); }
        catch (Exception ex) { return App(handler, ex.Message); }
    }

    static object? PRaiseContinuable(object?[] args)
    {
        throw new SchemeException(args[0]);
    }

    static object? PExactInexact(object?[] args)
    {
        var x = args[0];
        if (x is int i) return (double)i;
        if (x is long l) return (double)l;
        if (x is BigInteger bi) return (double)bi;
        if (x is SchemeFraction f) return f.ToDouble();
        if (x is double d) return d;
        if (x is Complex c) return c;
        return Convert.ToDouble(x!);
    }

    static object? PStringContainsQ(object?[] args)
    {
        var s = ToStr(args[0]);
        var substr = ToStr(args[1]);
        return s.Contains(substr) ? Const.TRUE : Const.FALSE;
    }
    // ── Internal helpers ──

    private static int _gensymCtr;

    private static object? AlistCopy(object?[] args)
    {
        object? result = Const.NIL;
        foreach (var p in args[0].Cells())
        {
            if (p is Cell pc && pc.Cdr is Cell pc2 && pc2.Cdr is Nil)
                result = new Cell(new Cell(pc.Car, new Cell(pc2.Car, Const.NIL)), result);
            else if (p is Cell pc3)
                result = new Cell(new Cell(pc3.Car, pc3.Cdr), result);
            else
                result = new Cell(p, result);
        }
        object? prev = Const.NIL;
        var cur = result;
        while (cur is Cell cc)
        {
            var nxt = cc.Cdr;
            cc.Cdr = prev;
            prev = cur;
            cur = nxt;
        }
        return prev;
    }

    private static object? BreakList(object? pred, object? lst)
    {
        var yes = new List<object?>();
        var cur = lst;
        while (cur is Cell c)
        {
            if (ReferenceEquals(App(pred, c.Car), Const.TRUE)) break;
            yes.Add(c.Car);
            cur = c.Cdr;
        }
        return new Cell(yes.ToCell(), new Cell(cur, Const.NIL));
    }

    private static object? LastPair(object? lst)
    {
        var cur = lst;
        while (cur is Cell c && c.Cdr is Cell) cur = c.Cdr;
        return cur;
    }

    private static object? PSort(object?[] args)
    {
        var less = args[0];
        var items = args[1].Cells();
        var copy = new List<object?>(items);
        StableSort(copy, less);
        return copy.ToCell();
    }

    private static void StableSort(List<object?> items, object? less)
    {
        for (int i = 1; i < items.Count; i++)
        {
            var key = items[i];
            int j = i - 1;
            while (j >= 0 && IsLess(less, key, items[j]))
            {
                items[j + 1] = items[j];
                j--;
            }
            items[j + 1] = key;
        }
    }

    private static bool IsLess(object? less, object? a, object? b)
    {
        var r = App(less, a, b);
        return !ReferenceEquals(r, Const.FALSE) && r is not Nil;
    }

    private static long BitsToInteger(object? lst)
    {
        long r = 0;
        int bit = 0;
        foreach (var v in lst.Cells())
        {
            if (ReferenceEquals(v, Const.TRUE) || (v is Sym vs && vs.Name == "1") ||
                (v is long l && l == 1) || (v is int iv && iv == 1) ||
                (v is BigInteger bi && bi == BigInteger.One))
                r |= 1L << bit;
            bit++;
        }
        return r;
    }

    private static object? IntegerToBitsList(long n, int k)
    {
        var bits = new List<object?>();
        long temp = Math.Abs(n);
        while (temp != 0) { bits.Add(temp & 1); temp >>= 1; }
        if (bits.Count == 0) bits.Add(0L);
        while (bits.Count < k) bits.Add(0L);
        return bits.ToCell();
    }

    private static object? PFormat(object?[] args)
    {
        if (args.Length >= 2 && args[0] is ITuple it && it.Length > 2 && it[0] is "port" && it[1] is "output" && it[2] is StringBuilder sb)
        {
            sb.Append(FormatScheme(args[1], args[2..]));
            return Const.VOID;
        }
        if (args[0] is Sym s && s.Name == "#f")
        {
            return new SchemeString(FormatScheme(args[1], args[2..]));
        }
        return new SchemeString(FormatScheme(args[0], args[1..]));
    }

    private static string FormatScheme(object? fmt, object?[] args)
    {
        var parts = new StringBuilder();
        var f = ToStr(fmt);
        int i = 0, ai = 0;
        while (i < f.Length)
        {
            if (f[i] == '~' && i + 1 < f.Length)
            {
                char c = f[i + 1];
                if (c == 'a')
                {
                    parts.Append(args[ai] is string or SchemeString ? ToStr(args[ai]) : Printer.Format(args[ai]));
                    ai++; i += 2;
                }
                else if (c == 's')
                {
                    parts.Append(Printer.Format(args[ai]));
                    ai++; i += 2;
                }
                else if (c == 'd')
                {
                    if (ai >= args.Length) throw new SchemeException("format: not enough arguments");
                    parts.Append(NumericHelper.ToLong(args[ai]));
                    ai++; i += 2;
                }
                else if (c == '%') { parts.Append('\n'); i += 2; }
                else if (c == '~') { parts.Append('~'); i += 2; }
                else { parts.Append(f[i]); i += 2; }
            }
            else { parts.Append(f[i]); i++; }
        }
        return parts.ToString();
    }

    private static Func<object?[], object?> MakeParameter(object? init, object? converter)
    {
        object? box = converter is not null ? App(converter, init) : init;
        return args =>
        {
            if (args.Length == 0) return box;
            if (args.Length == 1)
            {
                box = converter is not null ? App(converter, args[0]) : args[0];
                return Const.VOID;
            }
            throw new SchemeException("make-parameter: too many arguments");
        };
    }

    private static Func<object?[], object?> MakeCoroutineGenerator(object? proc)
    {
        var vals = new System.Collections.Concurrent.ConcurrentQueue<object?>();
        var done = new ManualResetEventSlim(false);
        var resume = new ManualResetEventSlim(false);
        bool started = false;
        Func<object?[], object?> yield = v =>
        {
            vals.Enqueue(v.Length > 0 ? v[0] : Const.VOID);
            resume.Reset();
            while (!resume.Wait(50) && !done.IsSet) { }
            return Const.VOID;
        };
        void Run()
        {
            try { App(proc, yield); }
            finally { vals.Enqueue(Const.EOF); done.Set(); }
        }
        var t = new Thread(Run) { IsBackground = true };
        t.Start();
        return _ =>
        {
            if (done.IsSet && vals.IsEmpty) return Const.EOF;
            if (!started) { started = true; resume.Set(); }
            object? v = Const.EOF;
            while (!vals.TryDequeue(out v))
            {
                if (done.IsSet && vals.IsEmpty) return Const.EOF;
                Thread.Sleep(5);
            }
            resume.Set();
            return v;
        };
    }

    private static object? App(object? proc, params object?[] args)
    {
        if (proc is Func<object?[], object?> fn) return fn(args);
        if (proc is Miniscm.Compiler.CompiledLambda cl) return cl.Invoke(cl.Env, args);
        if (proc is LambdaProc lp)
        {
            var nenv = new Env(lp.ClosureEnv, lp.Params.Count);
            Evaluator.BindParams(lp.Params, args, nenv);
            var r = Evaluator.SeqTailCall(lp.Body, nenv);
            while (r is TailCall tc) r = Evaluator.Eval(tc.Expr, tc.Env);
            return r;
        }
        if (proc is Delegate d) return d.DynamicInvoke(args);
        throw new Exception($"cannot call: {Printer.Format(proc)}");
    }

    // Identifier equality: syntax objects are plain symbols here.
    private static bool EqSymbols(object? a, object? b)
    {
        if (a is Sym sa && b is Sym sb) return sa.Name == sb.Name;
        if (a is SyntaxObject oa) return EqSymbols(oa.Expr, b);
        if (b is SyntaxObject ob) return EqSymbols(a, ob.Expr);
        return ReferenceEquals(a, b);
    }

    // R7RS integer?: exact integers, fractions with value 1, and inexact
    // numbers with an integral value (e.g. 3.0) count.
    private static bool IsInteger(object? v) => v switch
    {
        int or long or BigInteger => true,
        double d => !double.IsNaN(d) && !double.IsInfinity(d) && d == Math.Floor(d),
        float f => !float.IsNaN(f) && !float.IsInfinity(f) && f == MathF.Floor(f),
        decimal m => m == decimal.Truncate(m),
        SchemeFraction f => f.Den == 1,
        _ => false,
    };

    private static object? Eql(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return Const.TRUE;
        if (a is null || b is null) return Const.FALSE;        if (a is Cell ca && b is Cell cb)
        {
            if (Eql(ca.Car, cb.Car) != Const.TRUE) return Const.FALSE;
            if (Eql(ca.Cdr, cb.Cdr) != Const.TRUE) return Const.FALSE;
            return Const.TRUE;
        }
        if (a is string sa && b is SchemeString ssb) return sa == ssb.ToString() ? Const.TRUE : Const.FALSE;
        if (a is SchemeString ssa && b is string sb) return ssa.ToString() == sb ? Const.TRUE : Const.FALSE;
        if (a is BigInteger bia && b is BigInteger bib) return bia == bib ? Const.TRUE : Const.FALSE;
        if (a is BigInteger bia2 && b is long lb1) return bia2 == lb1 ? Const.TRUE : Const.FALSE;
        if (a is long la1 && b is BigInteger bib2) return la1 == bib2 ? Const.TRUE : Const.FALSE;
        if (a is BigInteger bia3 && b is int ib1) return bia3 == ib1 ? Const.TRUE : Const.FALSE;
        if (a is int ia1 && b is BigInteger bib3) return ia1 == bib3 ? Const.TRUE : Const.FALSE;
        if (a is SchemeFraction fa && b is SchemeFraction fb) return fa.Equals(fb) ? Const.TRUE : Const.FALSE;
        if (a is SchemeFraction fa2 && b is long lf) return fa2.Equals(new SchemeFraction(lf, 1)) ? Const.TRUE : Const.FALSE;
        if (a is long lf2 && b is SchemeFraction fb2) return fb2.Equals(new SchemeFraction(lf2, 1)) ? Const.TRUE : Const.FALSE;
        if (a is long la && b is long lb) return la == lb ? Const.TRUE : Const.FALSE;
        if (a is long la2 && b is int ib) return la2 == ib ? Const.TRUE : Const.FALSE;
        if (a is int ia && b is long lb2) return ia == lb2 ? Const.TRUE : Const.FALSE;
        if (a is Complex cxa && b is Complex cxb) return cxa == cxb ? Const.TRUE : Const.FALSE;
        if (a is double da && b is double db)
        {
            if (double.IsNaN(da) && double.IsNaN(db)) return Const.TRUE;
            if (double.IsPositiveInfinity(da) && double.IsPositiveInfinity(db)) return Const.TRUE;
            if (double.IsNegativeInfinity(da) && double.IsNegativeInfinity(db)) return Const.TRUE;
            if (da == db) return Const.TRUE;
            return Math.Abs(da - db) < 1e-15 ? Const.TRUE : Const.FALSE;
        }
        if (a is SchemeVector va && b is SchemeVector vb)
            return va.Data.SequenceEqual(vb.Data, EqualityComparer<object?>.Create((x, y) => Eql(x, y) == Const.TRUE)) ? Const.TRUE : Const.FALSE;
        if (a is SchemeBytevector bva && b is SchemeBytevector bvb)
            return bva.Data.AsSpan().SequenceEqual(bvb.Data) ? Const.TRUE : Const.FALSE;
        if (a is System.Collections.IDictionary dictA && b is System.Collections.IDictionary dictB)
        {
            if (dictA.Count != dictB.Count) return Const.FALSE;
            foreach (System.Collections.DictionaryEntry entry in dictA)
            {
                if (!dictB.Contains(entry.Key)) return Const.FALSE;
                if (Eql(entry.Value, dictB[entry.Key]) != Const.TRUE) return Const.FALSE;
            }
            return Const.TRUE;
        }
        if (a.Equals(b)) return Const.TRUE;
        return Const.FALSE;
    }

    private static object? CarFn(object? p) => p is Cell c ? c.Car : throw new Exception($"pair required: car of {Miniscm.Compiler.MinRef.SxPrint(p)}");
    private static object? CdrFn(object? p) => p is Cell c ? c.Cdr : throw new Exception("pair required");

    private static string ToStr(object? x) => x switch
    {
        string s => MutableStringViews.TryGetValue(s, out var view) ? view.ToString() : s,
        SchemeString ss => ss.ToString(),
        _ => x?.ToString() ?? ""
    };

    private static int AsChar(object? x) => x switch
    {
        SchemeChar sc => sc.Codepoint,
        string s when s.Length > 0 => char.IsHighSurrogate(s[0]) && s.Length > 1 && char.IsLowSurrogate(s[1]) ? char.ConvertToUtf32(s[0], s[1]) : (int)s[0],
        _ => (int)' '
    };

    private static int ToChar(object? x) => AsChar(x);

    private static SchemeVector AsVector(object? x) => x is SchemeVector sv ? sv : throw new Exception("vector required");
    private static SchemeBytevector AsBytevector(object? x) => x is SchemeBytevector bv ? bv : throw new Exception("bytevector required");

    private static object? Assoc(object? key, object? alist, bool useEq)
    {
        object? cur = alist;
        while (cur is Cell c)
        {
            if (c.Car is Cell entry && (useEq ? ReferenceEquals(entry.Car, key) : entry.Car?.Equals(key) == true))
                return entry;
            cur = c.Cdr;
        }
        return Const.FALSE;
    }

    private static bool IsPort(object? x, string? direction)
    {
        if (x is not ITuple it || it.Length < 2) return false;
        if (it[0] is not "port") return false;
        if (direction is not null)
        {
            if (it[1] is not string dir || dir != direction) return false;
        }
        return true;
    }

    private static object? MakePort(string direction, object? stream)
    {
        return ("port", direction, stream);
    }

    private static SchemeFraction FindSimplestInRange(SchemeFraction lo, SchemeFraction hi)
    {
        // Find the simplest fraction (smallest denominator) within [lo, hi]
        // Uses the Stern-Brocot / continued-fraction property (R7RS rationalize).
        // Ensure lo <= hi
        if (lo.Num * hi.Den > hi.Num * lo.Den)
            return FindSimplestInRange(hi, lo);
        return Simplest(lo, hi);
    }

    // Assumes x <= y. Returns the simplest rational in [x, y].
    private static SchemeFraction Simplest(SchemeFraction x, SchemeFraction y)
    {
        if (y.Num < 0) // both negative
        {
            var r = Simplest(new SchemeFraction(-y.Num, y.Den), new SchemeFraction(-x.Num, x.Den));
            return new SchemeFraction(-r.Num, r.Den);
        }
        if (x.Num <= 0) return new SchemeFraction(0, 1);
        return SimplestAux(x, y);
    }

    // Assumes 0 <= x <= y.
    private static SchemeFraction SimplestAux(SchemeFraction x, SchemeFraction y)
    {
        var fy = y.Num / y.Den; // floor(y)
        if (x.Num * y.Den < fy * y.Den) // x < floor(y)
            return new SchemeFraction(fy, 1);
        var fxCeil = (x.Num + x.Den - 1) / x.Den; // ceiling(x)
        if (fxCeil == fy)
            return new SchemeFraction(fxCeil, 1);
        // x and y share the same integer part n; strip it and invert.
        var n = x.Num / x.Den; // floor(x)
        var xn = new SchemeFraction(x.Num - n * x.Den, x.Den); // x - n
        var yn = new SchemeFraction(y.Num - n * y.Den, y.Den); // y - n
        // 1/y' <= 1/x' in (0, 1]-ish range; recurse on the inverted interval.
        var r = SimplestAux(new SchemeFraction(yn.Den, yn.Num),
                            new SchemeFraction(xn.Den, xn.Num));
        return new SchemeFraction(n * r.Num + r.Den, r.Num); // n + 1/r
    }

    private static string ToRadixString(BigInteger n, int radix)
    {
        if (n == 0) return "0";
        var digits = "0123456789abcdef";
        var result = new System.Text.StringBuilder();
        while (n > 0) { result.Insert(0, digits[(int)(n % radix)]); n /= radix; }
        return result.ToString();
    }


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



    static object? GenNext(object? g)
    {
        if (g is Func<object?> f) return f();
        if (g is Func<object?[], object?> fa) return fa(System.Array.Empty<object?>());
        throw new Exception("not a generator");
    }

    // generator: (generator v ...) — 生成返回各值的 thunk
    static object? PGenerator(object?[] args)
    {
        var vals = args.ToList();
        int idx = 0;
        return (Func<object?>)(() => idx < vals.Count ? vals[idx++] : Const.EOF);
    }

    // make-generator: (make-generator gen) — 恒等
    static object? PMakeGenerator(object?[] args) => args[0];

    // list->generator: 列表转 generator
    static object? PListGenerator(object?[] args)
    {
        var items = new List<object?>();
        object? cur = args[0];
        while (cur is Cell c) { items.Add(c.Car); cur = c.Cdr; }
        int idx = 0;
        return (Func<object?>)(() => idx < items.Count ? items[idx++] : Const.EOF);
    }

    // vector->generator
    static object? PVectorGenerator(object?[] args)
    {
        var items = args[0] is SchemeVector sv ? sv.Data.ToList() : new List<object?>();
        int idx = 0;
        return (Func<object?>)(() => idx < items.Count ? items[idx++] : Const.EOF);
    }

    // string->generator
    static object? PStringGenerator(object?[] args)
    {
        var items = ToStr(args[0]).EnumerateRunes().Select(r => (object?)new SchemeChar(r.Value)).ToList();
        int idx = 0;
        return (Func<object?>)(() => idx < items.Count ? items[idx++] : Const.EOF);
    }

    // generator->list
    static object? PGeneratorToList(object?[] args)
    {
        var results = new List<object?>();
        while (true) { var v = GenNext(args[0]); if (v is Eof) break; results.Add(v); }
        return results.ToCell();
    }

    // generator->vector
    static object? PGeneratorToVector(object?[] args)
    {
        var results = new List<object?>();
        while (true) { var v = GenNext(args[0]); if (v is Eof) break; results.Add(v); }
        return new SchemeVector(results);
    }

    // generator->string
    static object? PGeneratorToString(object?[] args)
    {
        var sb = new StringBuilder();
        while (true) { var v = GenNext(args[0]); if (v is Eof) break; sb.Append(char.ConvertFromUtf32(AsChar(v))); }
        return new SchemeString(sb.ToString());
    }

    // make-range-generator: (make-range-generator start end [step])
    static object? PMakeRangeGenerator(object?[] args)
    {
        var start = NumericHelper.ToLong(args[0]);
        var end = NumericHelper.ToLong(args[1]);
        var step = args.Length > 2 ? NumericHelper.ToLong(args[2]) : 1;
        long cur = start;
        return (Func<object?>)(() =>
        {
            if (step > 0 ? cur < end : cur > end) { var v = cur; cur += step; return v; }
            return Const.EOF;
        });
    }

    // make-iota-generator: (make-iota-generator n [step [start]])
    static object? PMakeIotaGenerator(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var step = args.Length > 1 ? NumericHelper.ToLong(args[1]) : 1;
        var start = args.Length > 2 ? NumericHelper.ToLong(args[2]) : 0;
        long cnt = 0;
        return (Func<object?>)(() => cnt < n ? (start + cnt++ * step) : Const.EOF);
    }

    // generator-map: (generator-map fn g)
    static object? PGeneratorMap(object?[] args)
    {
        var fn = args[0];
        var g = args[1];
        return (Func<object?>)(() =>
        {
            var v = GenNext(g);
            return v is Eof ? Const.EOF : App(fn, v);
        });
    }

    // generator-filter: (generator-filter pred g)
    static object? PGeneratorFilter(object?[] args)
    {
        var pred = args[0];
        var g = args[1];
        return (Func<object?>)(() =>
        {
            while (true)
            {
                var v = GenNext(g);
                if (v is Eof) return Const.EOF;
                if (Truthy(App(pred, v))) return v;
            }
        });
    }

    // generator-take: (generator-take g n)
    static object? PGeneratorTake(object?[] args)
    {
        var g = args[0];
        var n = NumericHelper.ToLong(args[1]);
        long cnt = 0;
        return (Func<object?>)(() =>
        {
            if (cnt >= n) return Const.EOF;
            var v = GenNext(g);
            if (v is Eof) return Const.EOF;
            cnt++;
            return v;
        });
    }

    // generator-count: (generator-count pred g)
    static object? PGeneratorCount(object?[] args)
    {
        var pred = args[0];
        var g = args[1];
        long cnt = 0;
        while (true) { var v = GenNext(g); if (v is Eof) break; if (Truthy(App(pred, v))) cnt++; }
        return cnt;
    }

    // generator-find: (generator-find pred g)
    static object? PGeneratorFind(object?[] args)
    {
        var pred = args[0];
        var g = args[1];
        while (true) { var v = GenNext(g); if (v is Eof) return Const.EOF; if (Truthy(App(pred, v))) return v; }
    }

    // generator-for-each: (generator-for-each fn g)
    static object? PGeneratorForEach(object?[] args)
    {
        var fn = args[0];
        var g = args[1];
        while (true) { var v = GenNext(g); if (v is Eof) break; App(fn, v); }
        return Const.VOID;
    }


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



    static object? PEqvQ(object?[] args)
    {
        var a = args[0];
        var b = args[1];
        if (ReferenceEquals(a, b)) return Const.TRUE;
        if (a is null || b is null) return Const.FALSE;
        // 数值：跨类型同值（如 int 1 vs long 1）也 #t，但 exact/inexact 混合必须 #f
        if (a is int or long or BigInteger or SchemeFraction or double or float or Complex
            && b is int or long or BigInteger or SchemeFraction or double or float or Complex)
        {
            if (a is Complex || b is Complex)
            {
                if (a.GetType() != b.GetType()) return Const.FALSE;
                return a.Equals(b) ? Const.TRUE : Const.FALSE;
            }
            var ta = NumericHelper.Classify(a);
            var tb = NumericHelper.Classify(b);
            var exactA = ta <= NumericHelper.NumType.Fraction;
            var exactB = tb <= NumericHelper.NumType.Fraction;
            if (exactA != exactB) return Const.FALSE;
            return NumericHelper.Compare(a, b) == 0 ? Const.TRUE : Const.FALSE;
        }
        if (a.GetType() == b.GetType())
        {
            if (a is string s) return s == (string)b ? Const.TRUE : Const.FALSE;
            if (a is SchemeChar sc) return sc.Codepoint == ((SchemeChar)b).Codepoint ? Const.TRUE : Const.FALSE;
        }
        return Const.FALSE;
    }


    static object? PListTail(object?[] args)
    {
        var n = NumericHelper.ToInt(args[1]);
        object? cur = args[0];
        for (int i = 0; i < n; i++) cur = cur is Cell c ? c.Cdr : Const.NIL;
        return cur;
    }


    static object? PAppend(object?[] args)
    {
        if (args.Length == 0) return Const.NIL;
        object? result = args[^1];
        for (int i = args.Length - 2; i >= 0; i--)
        {
            var lst = args[i];
            if (lst is Cell cc)
            {
                var items = new List<object?> { cc.Car };
                var cur = cc.Cdr;
                while (cur is Cell c) { items.Add(c.Car); cur = c.Cdr; }
                for (int j = items.Count - 1; j >= 0; j--)
                    result = new Cell(items[j], result);
            }
        }
        return result;
    }


    static object? PReverse(object?[] args)
    {
        var items = new List<object?>();
        object? cur = args[0];
        while (cur is Cell c) { items.Add(c.Car); cur = c.Cdr; }
        if (cur is not Nil) throw new Exception("reverse: not a proper list");
        return CellHelper.ToCell(items.AsEnumerable().Reverse());
    }


    static object? PListQ(object?[] args)
    {
        var x = args[0];
        if (x is Nil) return Const.TRUE;
        if (x is not Cell) return Const.FALSE;
        object? slow = x, fast = x;
        while (fast is Cell fc && fc.Cdr is Cell fcc)
        {
            slow = ((Cell)slow!).Cdr;
            fast = fcc.Cdr;
            if (ReferenceEquals(slow, fast)) return Const.FALSE;
        }
        // Loop exited because fast's cdr is not a Cell. A proper list ends
        // with the empty list (fast may be Nil or a single Cell whose cdr
        // is Nil); a dotted list ends with a non-Nil atom.
        return (fast is Nil) || (fast is Cell last && last.Cdr is Nil)
            ? Const.TRUE : Const.FALSE;
    }


    static object? PListCopy(object?[] args)
    {
        if (args[0] is Nil) return Const.NIL;
        if (args[0] is not Cell first) return args[0];
        var head = new Cell(first.Car, Const.NIL);
        var tail = head;
        object? cur = first.Cdr;
        while (cur is Cell c)
        {
            var n = new Cell(c.Car, Const.NIL);
            tail.Cdr = n;
            tail = n;
            cur = c.Cdr;
        }
        if (cur is not Nil) tail.Cdr = cur;  // 保留点对尾
        return head;
    }


    static object? PMemq(object?[] args)
    {
        object? cur = args[1];
        while (cur is Cell c) { if (ReferenceEquals(c.Car, args[0]) || c.Car?.Equals(args[0]) == true) return cur; cur = c.Cdr; }
        return Const.FALSE;
    }


    static object? PMinus(object?[] args)
    {
        if (args.Length == 0) return 0L;
        if (args.Length == 1) return NumericHelper.Negate(args[0]);
        return args.Skip(1).Aggregate((object?)args[0], (acc, x) => NumericHelper.Sub(acc!, x))!;
    }


    static object? PNumberString(object?[] args)
    {
        var radix = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 10;
        if (radix == 10) return Printer.Format(args[0]);
        var n = NumericHelper.ToBigInt(args[0]);
        if (n < 0) return "-" + ToRadixString(-n, radix);
        return ToRadixString(n, radix);
    }


    static object? PEq(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        var first = args[0];
        for (int i = 1; i < args.Length; i++)
        {
            var other = args[i];
            var firstBool = first is Sym fs && (fs.Name == "#t" || fs.Name == "#f");
            var otherBool = other is Sym os && (os.Name == "#t" || os.Name == "#f");
            if (firstBool != otherBool) return Const.FALSE;
            if (firstBool ? !ReferenceEquals(first, other) : NumericHelper.Compare(first, other) != 0)
                return Const.FALSE;
        }
        return Const.TRUE;
    }


    static object? PLt(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        for (int i = 1; i < args.Length; i++)
            if (NumericHelper.Compare(args[i - 1], args[i]) >= 0) return Const.FALSE;
        return Const.TRUE;
    }


    static object? PGt(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        for (int i = 1; i < args.Length; i++)
            if (NumericHelper.Compare(args[i - 1], args[i]) <= 0) return Const.FALSE;
        return Const.TRUE;
    }


    static object? PLe(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        for (int i = 1; i < args.Length; i++)
            if (NumericHelper.Compare(args[i - 1], args[i]) > 0) return Const.FALSE;
        return Const.TRUE;
    }


    static object? PGe(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        for (int i = 1; i < args.Length; i++)
            if (NumericHelper.Compare(args[i - 1], args[i]) < 0) return Const.FALSE;
        return Const.TRUE;
    }


    static object? PMap(object?[] args)
    {
        var fn = args[0];
        var results = new List<object?>();
        if (args.Length == 2)
        {
            object? cur = args[1];
            while (cur is Cell c) { results.Add(App(fn, c.Car)); cur = c.Cdr; }
        }
        else
        {
            var lists = new List<object?>[args.Length - 1];
            for (int i = 0; i < lists.Length; i++) lists[i] = args[i + 1].Cells();
            int minLen = lists.Min(l => l.Count);
            for (int i = 0; i < minLen; i++)
            {
                var callArgs = new object?[lists.Length];
                for (int j = 0; j < lists.Length; j++) callArgs[j] = lists[j][i];
                results.Add(App(fn, callArgs));
            }
        }
        return results.ToCell();
    }


    static object? PForEach(object?[] args)
    {
        var fn = args[0];
        if (args.Length == 2)
        {
            object? cur = args[1];
            while (cur is Cell c) { App(fn, c.Car); cur = c.Cdr; }
        }
        else
        {
            var lists = new List<object?>[args.Length - 1];
            for (int i = 0; i < lists.Length; i++) lists[i] = args[i + 1].Cells();
            int minLen = lists.Min(l => l.Count);
            for (int i = 0; i < minLen; i++)
            {
                var callArgs = new object?[lists.Length];
                for (int j = 0; j < lists.Length; j++) callArgs[j] = lists[j][i];
                App(fn, callArgs);
            }
        }
        return Const.VOID;
    }


    static object? PFilter(object?[] args)
    {
        var pred = args[0];
        var results = new List<object?>();
        object? cur = args[1];
        while (cur is Cell c) { if (App(pred, c.Car) is Sym s && s != Const.FALSE) results.Add(c.Car); cur = c.Cdr; }
        return results.ToCell();
    }


    static object? PDisplay(object?[] args)
    {
        var obj = args[0];
        object? port = null;
        if (args.Length > 1 && args[1] is ITuple t && t.Length >= 3 && t[0] is string s0 && s0 == "port" && (t[1] is "output" || t[1] is "input"))
            port = t[2];
        if (port is StreamWriter sw) { sw.Write(Printer.ToDisplayString(obj)); sw.Flush(); }
        else if (port is StringBuilder sb) { sb.Append(Printer.ToDisplayString(obj)); }
        else Console.Write(Printer.ToDisplayString(obj));
        return Const.VOID;
    }


    static object? PWriteChar(object?[] args)
    {
        var cp = AsChar(args[0]);
        var cs = char.ConvertFromUtf32(cp);
        object? port = null;
        if (args.Length > 1 && args[1] is ITuple t && t.Length >= 3 && t[0] is string s0 && s0 == "port" && (t[1] is "output" || t[1] is "input"))
            port = t[2];
        if (port is StreamWriter sw) { sw.Write(cs); sw.Flush(); }
        else if (port is StringBuilder sb) { sb.Append(cs); }
        else Console.Write(cs);
        return Const.VOID;
    }


    static object? PWrite(object?[] args)
    {
        var obj = args[0];
        object? port = null;
        if (args.Length > 1 && args[1] is ITuple t && t.Length >= 3 && t[0] is string s0 && s0 == "port" && (t[1] is "output" || t[1] is "input"))
            port = t[2];
        if (port is StreamWriter sw) { sw.Write(Printer.Format(obj)); sw.Flush(); }
        else if (port is StringBuilder sb) { sb.Append(Printer.Format(obj)); }
        else Console.Write(Printer.Format(obj));
        return Const.VOID;
    }

    static object? PPrint(object?[] args)
    {
        var value = args.Length > 0 ? args[0] : Const.VOID;
        var port = args.Length > 1 ? args[1] : null;
        var text = Printer.Format(value);
        if (port is ITuple t && t.Length > 2 && t[0] is "port")
        {
            if (t[2] is StreamWriter sw) { sw.WriteLine(text); sw.Flush(); }
            else if (t[2] is StringBuilder sb) sb.AppendLine(text);
            else if (t[2] is BytePort bp) { foreach (var b in Encoding.UTF8.GetBytes(text + "\n")) bp.Append(b); }
        }
        else Console.WriteLine(text);
        return Const.VOID;
    }


    static object? PError(object?[] args)
    {
        var irrList = args.Skip(1).ToList();
        throw new SchemeException(new ErrorObject(args[0], irrList.ToCell()));
    }


    static object? PSxDefmacro(object?[] args)
    {
        if (args.Length >= 3 && args[0] is Sym nameSym && args[1] is not null && args[2] is not null)
        {
            // (sx-defmacro name pattern body) — Scheme 端宏注册桥接原语。
            // 微解释器无 C# define-macro 特殊形式, my-definemacro 经此注册
            // "macro" 元组到全局环境。pattern 固定为 rest 符号 args,
            // 真正的模式解构与宏体求值在 Scheme (sx-macro-expand)。
            var defEnv = args.Length > 3 && args[3] is Env de ? de : Evaluator.GlobalEnv;
            Evaluator.GlobalEnv.Data[nameSym.Name] = ("macro", args[1], args[2], defEnv, true);
            return nameSym;
        }
        throw new Exception("sx-defmacro: expected (sx-defmacro name pattern body [env])");
    }

    static object? PSxDefinedQ(object?[] args)
    {
        var name = (args[0] as Sym)?.Name ?? args[0]?.ToString() ?? "";
        var env = args.Length > 1 && args[1] is Env e2 ? e2 : Evaluator.GlobalEnv;
        return env.LookupSilent(name, null) is not null ? Const.TRUE : Const.FALSE;
    }



    private static object? PSxExpandCall(object?[] args)
    {
        if (args.Length >= 1 && args[0] is Cell call)
        {
            var env = args.Length > 1 && args[1] is Env e2 ? e2 : Evaluator.GlobalEnv;
            var op = call.Car;
            if (op is Sym ops)
            {
                var proc = env.LookupSilent(ops.Name, null);
                if (proc is not null)
                {
                    var expanded = Evaluator.ExpandMacro(proc, call.Cdr, env);
                    if (expanded is not null) return expanded;
                }
            }
        }
        return Const.FALSE;
    }

    private static object? S12True(object? x) => Truthy(x) ? Const.TRUE : Const.FALSE;
    private static object? S12List(object? x) => x is Cell ? x.Cells() : [];
    private static SchemeString S12String(object? x) => x is SchemeString s ? s : new SchemeString(ToStr(x));

    private static object? S12LastPair(object? x)
    {
        if (x is not Cell c) throw new SchemeException("last-pair: expected pair");
        while (c.Cdr is Cell n) c = n;
        return c;
    }

    private static bool S12Eq(object? a, object? b) => ReferenceEquals(a, b) || Equals(a, b);
    private static object? S12Call(object? p, params object?[] a) => App(p, a);

    private static object? S12Record(object? tag, params object?[] fields) => new Cell(tag, fields.ToCell());
    private static bool S12Tag(object? x, string tag) => x is Cell c && c.Car is Sym s && s.Name == tag;
    private static object? S12Field(object? x, int i) => x is Cell c && c.Cdr is Cell f ? f.Cells().ElementAt(i) : throw new SchemeException("record field");
    private static object? S12SetField(object? x, int i, object? value)
    {
        if (x is not Cell c || c.Cdr is not Cell f) throw new SchemeException("record field");
        for (int n = 0; n < i; n++) f = (Cell)f.Cdr!;
        f.Car = value;
        return Const.VOID;
    }

    private static object? S12ListQueue(object?[] args)
    {
        var q = new SchemeListQueue();
        if (args.Length == 1 && args[0] is Cell)
            q.Items.AddRange(args[0].Cells());
        else if (args.Length == 1 && args[0] is Nil)
            return q;
        else
            q.Items.AddRange(args);
        return q;
    }
    private static object? S12QueueRemove(object? q, bool back)
    {
        var queue = (SchemeListQueue)q!;
        if (queue.Items.Count == 0) throw new SchemeException("list-queue-remove!: empty queue");
        var i = back ? queue.Items.Count - 1 : 0;
        var value = queue.Items[i]; queue.Items.RemoveAt(i); return value;
    }
    private static bool S12HeapLess(SchemeBinaryHeap heap, object? a, object? b)
        => Truthy(S12Call(heap.Comparator, a, b));
    private static void S12HeapUp(SchemeBinaryHeap heap, int i)
    {
        while (i > 0)
        {
            var parent = (i - 1) / 2;
            if (!S12HeapLess(heap, heap.Items[i], heap.Items[parent])) break;
            (heap.Items[i], heap.Items[parent]) = (heap.Items[parent], heap.Items[i]);
            i = parent;
        }
    }
    private static void S12HeapDown(SchemeBinaryHeap heap, int i)
    {
        while (true)
        {
            var best = i; var left = i * 2 + 1; var right = left + 1;
            if (left < heap.Items.Count && S12HeapLess(heap, heap.Items[left], heap.Items[best])) best = left;
            if (right < heap.Items.Count && S12HeapLess(heap, heap.Items[right], heap.Items[best])) best = right;
            if (best == i) return;
            (heap.Items[i], heap.Items[best]) = (heap.Items[best], heap.Items[i]); i = best;
        }
    }
    private static void S12Heapify(SchemeBinaryHeap heap)
    { for (int i = heap.Items.Count / 2 - 1; i >= 0; i--) S12HeapDown(heap, i); }
    private static object? S12ArrayBuild(List<int> dims, int at, object? fill)
    {
        var v = new SchemeVector(dims[at]);
        for (int i = 0; i < v.Length; i++) v[i] = at == dims.Count - 1 ? fill : S12ArrayBuild(dims, at + 1, fill);
        return v;
    }
    private static SchemeVector S12ArrayValue(object? x) => x is SchemeArray a ? a.Value : (SchemeVector)x!;
    private static object? S12ArrayRef(object? array, object?[] indices)
    {
        object? cur = S12ArrayValue(array);
        foreach (var index in indices) cur = ((SchemeVector)cur!)[NumericHelper.ToInt(index)];
        return cur;
    }
    private static object? S12ArraySet(object? array, object? value, object?[] indices)
    {
        if (indices.Length == 0) throw new SchemeException("array-set!: no indices");
        object? cur = S12ArrayValue(array);
        for (int i = 0; i < indices.Length - 1; i++) cur = ((SchemeVector)cur!)[NumericHelper.ToInt(indices[i])];
        ((SchemeVector)cur!)[NumericHelper.ToInt(indices[^1])] = value;
        return Const.VOID;
    }
    private static object? S12ArrayDims(object? x)
    {
        var result = new List<object?>(); object? cur = S12ArrayValue(x);
        while (cur is SchemeVector v) { result.Add((long)v.Length); cur = v.Length == 0 ? null : v[0]; }
        return result.ToCell();
    }


    private static object? S12StringMap(object?[] args){var strings=args[1..].Select(x=>S12String(x).ToString().EnumerateRunes().ToList()).ToList();var n=strings.Min(x=>x.Count);var chars=new List<int>();for(int i=0;i<n;i++)chars.Add(AsChar(App(args[0],strings.Select(x=>(object?)new SchemeChar(x[i].Value)).ToArray())));return new SchemeString(chars);}
    private static object? S12StringForEach(object?[] args){var strings=args[1..].Select(x=>S12String(x).ToString().EnumerateRunes().ToList()).ToList();var n=strings.Min(x=>x.Count);for(int i=0;i<n;i++)App(args[0],strings.Select(x=>(object?)new SchemeChar(x[i].Value)).ToArray());return Const.VOID;}
    private static object? S12StringQuantifier(object?[] args,bool every){var s=S12String(args[1]).ToString();object? last=Const.TRUE;foreach(var rune in s.EnumerateRunes()){var r=App(args[0],new SchemeChar(rune.Value));if(every){if(!Truthy(r))return Const.FALSE;last=r;}else if(Truthy(r))return r;}return every?last:Const.FALSE;}
    private static object? S12Trim(object?[] args,int mode){var s=S12String(args[0]).ToString();return new SchemeString(mode==0?s.Trim():mode==1?s.TrimEnd():s.Trim());}
    private static string _charName(int cp)=>cp switch{' '=>"space",'\n'=>"newline",'\t'=>"tab",'\r'=>"return",'\0'=>"null",'\a'=>"alarm",'\b'=>"backspace",'\x1b'=>"escape",'\x7f'=>"delete",_=>char.ConvertFromUtf32(cp)};
    private static long S12RandomStep(SchemeRandomSource s){s.State=(1103515245L*s.State+12345)&0x7fffffff;return s.State;}
    private static object? S12RandomInt(SchemeRandomSource s,int n){if(n<=0)return 0L;var state=S12RandomStep(s);return (long)Math.Floor((state/2147483648.0)*n+0.5)%n;}
    private static object? S12RandomReal(SchemeRandomSource s)=>S12RandomStep(s)/2147483648.0;
    private static object? MappingRef(object?[] a){foreach(var p in a[0].Cells()){var c=(Cell)p!;if(S12Eq(c.Car,a[1]))return c.Cdr;}return a.Length>2?a[2]:Const.FALSE;}
    private static object? MappingSet(object?[] a){var r=new List<object?>{new Cell(a[1],a[2])};foreach(var p in a[0].Cells()){var c=(Cell)p!;if(!S12Eq(c.Car,a[1]))r.Add(c);}return r.ToCell();}
    private static object? MappingDelete(object?[] a)=>a[0].Cells().Where(p=>!S12Eq(((Cell)p!).Car,a[1])).ToCell();
    private static object? PS12StringSplit(object?[] args)
    {
        var s = S12String(args[0]).ToString(); var sep = args.Length > 1 ? (args[1] is SchemeChar c ? char.ConvertFromUtf32(c.Codepoint) : ToStr(args[1])) : " ";
        if (sep.Length == 0) throw new SchemeException("string-split: empty separator");
        return s.Split(sep, StringSplitOptions.None).Select(x => (object?)new SchemeString(x)).ToCell();
    }
    private static object? TestList(object? value) => value is Cell ? value.Cells().ToCell() : value;
    private static object? TestCall(object? p, params object?[] args) => App(p, args);
    private static bool TestTrue(object? x) => Truthy(x);

    private static object? Everywhere(object? proc, object? x)
    {
        if (x is Cell c) return new Cell(Everywhere(proc, c.Car), Everywhere(proc, c.Cdr));
        if (x is SchemeVector sv)
        {
            var nv = new SchemeVector(sv.Data.Count);
            for (int i = 0; i < sv.Data.Count; i++) nv[i] = Everywhere(proc, sv.Data[i]);
            return nv;
        }
        return App(proc, x);
    }


    private static string Base32(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder(); int buffer = 0, bits = 0;
        foreach (var b in data) { buffer = (buffer << 8) | b; bits += 8; while (bits >= 5) { bits -= 5; sb.Append(alphabet[(buffer >> bits) & 31]); } }
        if (bits > 0) sb.Append(alphabet[(buffer << (5 - bits)) & 31]);
        while (sb.Length % 8 != 0) sb.Append('=');
        return sb.ToString();
    }

    private static object? PCsvRead(object?[] a)
    {
        var port = a[0]; var text = port is ITuple t && t.Length > 2 && t[2] is StringPort sp ? sp.Data : "";
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split(',').Select(x => (object?)new SchemeString(x)).ToCell()).ToCell();
    }

    private static object? PGroupBy(object?[] a)
    {
        var yes = new List<object?>(); var no = new List<object?>();
        foreach (var x in a[1].Cells()) (Truthy(App(a[0], x)) ? yes : no).Add(x);
        return new Cell(yes.ToCell(), new Cell(no.ToCell(), Const.NIL));
    }
    private static object? StringTakeRight(object?[] args)
    {
        var s = Str(args[0]);
        int n = NumericHelper.ToInt(args[1]);
        return n == 0 ? new SchemeString("") : new SchemeString(s[^Math.Min(n, s.Length)..]);
    }

    private static object? StringDropRight(object?[] args)
    {
        var s = Str(args[0]);
        int n = NumericHelper.ToInt(args[1]);
        return n == 0 ? new SchemeString(s) : new SchemeString(s[..^Math.Min(n, s.Length)]);
    }

    private static object? StringReplace(object?[] args)
    {
        var s = Str(args[0]);
        var rep = Str(args[1]);
        int start = NumericHelper.ToInt(args[2]);
        int end = NumericHelper.ToInt(args[3]);
        return new SchemeString(s[..start] + rep + s[end..]);
    }

    private static object? StringForEach(object?[] args)
    {
        var fn = args[0];
        var s = Str(args[1]);
        foreach (var rune in s.EnumerateRunes()) App(fn, new SchemeChar(rune.Value));
        return Const.VOID;
    }

    private static object? StringForEachIndex(object?[] args)
    {
        var fn = args[0];
        var s = Str(args[1]);
        for (int i = 0; i < s.Length; i++) App(fn, (long)i);
        return Const.VOID;
    }

    private static object? StringTabulate(object?[] args)
    {
        int n = NumericHelper.ToInt(args[0]);
        var fn = args[1];
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++) sb.Append(char.ConvertFromUtf32(AsChar(App(fn, (long)i))));
        return new SchemeString(sb.ToString());
    }

    private static string Str(object? x) => x is SchemeString ss ? ss.ToString() : ToStr(x);

    private static object? StrPad(object?[] args, bool right)
    {
        var s = Str(args[0]);
        int n = NumericHelper.ToInt(args[1]);
        var ch = args.Length > 2 ? CharStr(args[2]) : " ";
        if (s.Length >= n) return new SchemeString(s[..n]);
        var pad = new string(ch[0], n - s.Length);
        return right ? new SchemeString(s + pad) : new SchemeString(pad + s);
    }

    private static string CharStr(object? c) => c is SchemeChar sc ? char.ConvertFromUtf32(sc.Codepoint) : Str(c);

    private static object? StrSplit(object?[] args)
    {
        var s = Str(args[0]);
        string[] parts;
        if (args.Length < 2 || args[1] is null)
            parts = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        else if (args[1] is SchemeChar sc)
            parts = s.Split(char.ConvertFromUtf32(sc.Codepoint), StringSplitOptions.RemoveEmptyEntries);
        else
            parts = s.Split(new[] { Str(args[1]) }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Select(p => (object?)new SchemeString(p)).ToCell();
    }

    private static object? StrJoin(object?[] args)
    {
        var parts = args[0].Cells().Select(x => Str(x)).ToList();
        var delim = args.Length > 1 ? Str(args[1]) : " ";
        return new SchemeString(string.Join(delim, parts));
    }

    private static object? StrContains(object? s, object? needle)
    {
        var str = Str(s);
        var sub = Str(needle);
        var strRunes = str.EnumerateRunes().Select(r => r.Value).ToList();
        var subRunes = sub.EnumerateRunes().Select(r => r.Value).ToList();
        for (int i = 0; i + subRunes.Count <= strRunes.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < subRunes.Count; j++)
                if (strRunes[i + j] != subRunes[j]) { match = false; break; }
            if (match) return (long)i;
        }
        return Const.FALSE;
    }

    private static object? PrefixLen(object?[] args, bool ci)
    {
        var s1 = Str(args[0]);
        var s2 = Str(args[1]);
        int n = 0;
        int max = Math.Min(s1.Length, s2.Length);
        for (int i = 0; i < max; i++)
        {
            bool eq = ci
                ? char.ToLowerInvariant(s1[i]) == char.ToLowerInvariant(s2[i])
                : s1[i] == s2[i];
            if (!eq) break;
            n++;
        }
        return (long)n;
    }

    private static object? SuffixLen(object?[] args, bool ci)
    {
        var s1 = Str(args[0]);
        var s2 = Str(args[1]);
        int n = 0;
        int max = Math.Min(s1.Length, s2.Length);
        for (int i = 0; i < max; i++)
        {
            bool eq = ci
                ? char.ToLowerInvariant(s1[s1.Length - 1 - i]) == char.ToLowerInvariant(s2[s2.Length - 1 - i])
                : s1[s1.Length - 1 - i] == s2[s2.Length - 1 - i];
            if (!eq) break;
            n++;
        }
        return (long)n;
    }

    private static object? StrCount(object?[] args)
    {
        var s = Str(args[0]);
        int n = 0;
        if (args[1] is not (string or SchemeString or SchemeChar))
        {
            foreach (var rune in s.EnumerateRunes())
                if (ReferenceEquals(App(args[1], new SchemeChar(rune.Value)), Const.TRUE)) n++;
        }
        else
        {
            var needle = args[1] is SchemeChar scc ? char.ConvertFromUtf32(scc.Codepoint) : Str(args[1]);
            int i = 0;
            while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        }
        return (long)n;
    }

    private static object? StrMap(object?[] args)
    {
        var fn = args[0];
        var s = Str(args[1]);
        var sb = new StringBuilder();
        foreach (var rune in s.EnumerateRunes())
            sb.Append(char.ConvertFromUtf32(AsChar(App(fn, new SchemeChar(rune.Value)))));
        return new SchemeString(sb.ToString());
    }

    private static object? StrFold(object?[] args, bool right)
    {
        var fn = args[0];
        object? acc = args[1];
        var s = Str(args[2]);
        var runes = s.EnumerateRunes().ToList();
        if (right)
        {
            for (int i = runes.Count - 1; i >= 0; i--) acc = App(fn, new SchemeChar(runes[i].Value), acc);
        }
        else
        {
            foreach (var rune in runes) acc = App(fn, new SchemeChar(rune.Value), acc);
        }
        return acc;
    }

    private static object? StrIndex(object? s, object? pred, bool right, bool skip)
    {
        var str = Str(s);
        var runes = str.EnumerateRunes().ToList();
        if (right)
        {
            for (int i = runes.Count - 1; i >= 0; i--)
            {
                var r = App(pred, new SchemeChar(runes[i].Value));
                bool hit = skip ? !ReferenceEquals(r, Const.TRUE) : ReferenceEquals(r, Const.TRUE);
                if (hit) return (long)i;
            }
        }
        else
        {
            for (int i = 0; i < runes.Count; i++)
            {
                var r = App(pred, new SchemeChar(runes[i].Value));
                bool hit = skip ? !ReferenceEquals(r, Const.TRUE) : ReferenceEquals(r, Const.TRUE);
                if (hit) return (long)i;
            }
        }
        return right && !skip ? Const.FALSE : (skip ? (object?)(long)runes.Count : Const.FALSE);
    }

    private static object? StrAnyEvery(object?[] args, bool every)
    {
        var pred = args[0];
        var s = Str(args[1]);
        object? last = Const.TRUE;
        foreach (var rune in s.EnumerateRunes())
        {
            var r = App(pred, new SchemeChar(rune.Value));
            if (every)
            {
                if (ReferenceEquals(r, Const.FALSE)) return Const.FALSE;
                last = r;
            }
            else
            {
                if (!ReferenceEquals(r, Const.FALSE)) return r;
            }
        }
        return every ? last : Const.FALSE;
    }

    private static object? StrCopyBang(object?[] args)
    {
        var target = args[0] as SchemeString;
        int tstart = NumericHelper.ToInt(args[1]);
        var src = Str(args[2]);
        int sstart = args.Length > 3 ? NumericHelper.ToInt(args[3]) : 0;
        int send = args.Length > 4 ? NumericHelper.ToInt(args[4]) : src.Length;
        if (target is not null)
        {
            for (int i = sstart; i < send; i++)
            {
                int idx = tstart + i - sstart;
                if (idx < target.Length) target[idx] = char.ConvertToUtf32(src, i);
            }
        }
        return Const.VOID;
    }

    private static object? StrFilter(object?[] args, bool keep)
    {
        var pred = args[0];
        var s = Str(args[1]);
        var sb = new StringBuilder();
        foreach (var rune in s.EnumerateRunes())
        {
            bool hit = ReferenceEquals(App(pred, new SchemeChar(rune.Value)), Const.TRUE);
            if (hit == keep) sb.Append(char.ConvertFromUtf32(rune.Value));
        }
        return new SchemeString(sb.ToString());
    }

    private static string RevStr(string s)
    {
        var chars = s.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    private static string TitleCase(string s)
    {
        var sb = new StringBuilder(s.ToLowerInvariant());
        bool cap = true;
        for (int i = 0; i < sb.Length; i++)
        {
            if (char.IsWhiteSpace(sb[i])) cap = true;
            else if (cap) { sb[i] = char.ToUpperInvariant(sb[i]); cap = false; }
        }
        return sb.ToString();
    }

    private static object? Tokenize(object?[] args)
    {
        var s = Str(args[0]);
        var tokens = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Select(t => (object?)new SchemeString(t)).ToCell();
    }

    private static object? StrUnfold(object?[] args)
    {
        var pred = args[0];
        var gen = args[1];
        var step = args.Length > 2 ? args[2] : null;
        var seed = args.Length > 3 ? args[3] : Const.FALSE;
        var sb = new StringBuilder();
        var s = seed;
        while (true)
        {
            if (ReferenceEquals(App(pred, s), Const.TRUE)) break;
            var ch = App(gen, s);
            sb.Append(char.ConvertFromUtf32(AsChar(ch)));
            if (step is not null) s = App(step, s);
            else
            {
                if (s is Cell c) s = c.Cdr;
                else break;
            }
        }
        return new SchemeString(sb.ToString());
    }

    private static object? StrToVector(object? s)
    {
        var data = new List<object?>();
        foreach (var rune in Str(s).EnumerateRunes()) data.Add(new SchemeChar(rune.Value));
        return new SchemeVector(data);
    }

    private static object? VectorToStr(object? v)
    {
        var sb = new StringBuilder();
        if (v is SchemeVector sv)
        {
            foreach (var x in sv.Data)
            {
                if (x is SchemeChar sc) sb.Append(char.ConvertFromUtf32(sc.Codepoint));
                else sb.Append(ToStr(x));
            }
        }
        return new SchemeString(sb.ToString());
    }
    private static bool[] CharsetData(object? value)
    {
        if (value is bool[] bits) return bits;
        if (value is SchemeVector vector)
            return vector.Data.Select(x => x switch
            {
                bool b => b,
                Sym s => !ReferenceEquals(s, Const.FALSE),
                _ => x is not Nil
            }).ToArray();
        throw new ArgumentException("not a character set");
    }

    private static bool IsControlChar(int cp) => cp < 32 || cp == 127;

    private static object? CharName(object? c)
    {
        if (c is SchemeString or string)
        {
            var name = Str(c);
            int cp = name switch
            {
                "space" => ' ',
                "newline" => '\n',
                "tab" => '\t',
                "return" => '\r',
                "null" => '\0',
                "alarm" => '\a',
                "backspace" => '\b',
                "escape" => 0x1b,
                "delete" => 0x7f,
                _ => name.Length == 1 ? name[0] : -1
            };
            return cp >= 0 ? new Cell(Sym.Intern("char"), new Cell(new SchemeChar(cp), Const.NIL)) : Const.FALSE;
        }
        int cc = AsChar(c);
        string nm = cc switch
        {
            ' ' => "space",
            '\n' => "newline",
            '\t' => "tab",
            '\r' => "return",
            '\0' => "null",
            '\a' => "alarm",
            '\b' => "backspace",
            0x1b => "escape",
            0x7f => "delete",
            _ => ((char)cc).ToString()
        };
        return new SchemeString(nm);
    }

    private static object? MakeCharSet(object?[] args)
    {
        var cs = new bool[256];
        foreach (var c in args)
        {
            int cp = AsChar(c);
            if (cp < 256) cs[cp] = true;
        }
        return cs;
    }

    private static object? MakeCharSet(string s)
    {
        var cs = new bool[256];
        foreach (var rune in s.EnumerateRunes())
            if (rune.Value < 256) cs[rune.Value] = true;
        return cs;
    }

    private static bool CharSetContains(object? cs, object? c)
    {
        int i = AsChar(c);
        return i < 256 && CharsetData(cs)[i];
    }

    private static object? CharSetToList(object? cs)
    {
        var data = CharsetData(cs);
        var res = new List<object?>();
        for (int i = 0; i < 256; i++) if (data[i]) res.Add(new SchemeChar(i));
        return res.ToCell();
    }

    private static object? CharSetToString(object? cs)
    {
        var data = CharsetData(cs);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 256; i++) if (data[i]) sb.Append(char.ConvertFromUtf32(i));
        return new SchemeString(sb.ToString());
    }

    private static object? CharSetBinOp(object?[] args, bool union)
    {
        if (args.Length == 0) return new bool[256];
        var result = (bool[])CharsetData(args[0]).Clone();
        for (int k = 1; k < args.Length; k++)
        {
            var other = CharsetData(args[k]);
            for (int i = 0; i < 256; i++)
                result[i] = union ? (result[i] || other[i]) : (result[i] && other[i]);
        }
        return result;
    }

    private static object? CharSetDiff(object?[] args)
    {
        var result = (bool[])CharsetData(args[0]).Clone();
        for (int k = 1; k < args.Length; k++)
        {
            var other = CharsetData(args[k]);
            for (int i = 0; i < 256; i++) if (other[i]) result[i] = false;
        }
        return result;
    }

    private static object? CharSetXor(object?[] args)
    {
        var result = new bool[256];
        foreach (var csArg in args)
        {
            var cs = CharsetData(csArg);
            for (int i = 0; i < 256; i++) if (cs[i]) result[i] = !result[i];
        }
        return result;
    }

    private static object? CharSetComplement(object? cs)
    {
            var data = CharsetData(cs);
        var result = new bool[256];
        for (int i = 0; i < 256; i++) result[i] = !data[i];
        return result;
    }

    private static object? CharSetAdjoin(object?[] args, bool add)
    {
        var result = (bool[])CharsetData(args[0]).Clone();
        for (int k = 1; k < args.Length; k++)
        {
            int cp = AsChar(args[k]);
            if (cp < 256) result[cp] = add;
        }
        return result;
    }

    private static object? CharSetAny(object? pred, object? cs)
    {
        var data = CharsetData(cs);
        for (int i = 0; i < 256; i++)
            if (data[i] && !ReferenceEquals(App(pred, new SchemeChar(i)), Const.FALSE)) return new SchemeChar(i);
        return Const.FALSE;
    }

    private static object? CharSetEvery(object? pred, object? cs)
    {
        var data = CharsetData(cs);
        for (int i = 0; i < 256; i++)
            if (data[i] && !ReferenceEquals(App(pred, new SchemeChar(i)), Const.TRUE)) return Const.FALSE;
        return Const.TRUE;
    }

    private static object? CharSetFilter(object?[] args)
    {
        var pred = args[0];
        var basis = args.Length > 2 ? CharsetData(args[2]) : CharsetData(args[1]);
        var result = new bool[256];
        for (int i = 0; i < 256; i++)
            if (basis[i] && ReferenceEquals(App(pred, new SchemeChar(i)), Const.TRUE)) result[i] = true;
        return result;
    }

    private static object? CharSetFold(object? kons, object? knil, object? cs)
    {
        var data = CharsetData(cs);
        object? acc = knil;
        for (int i = 0; i < 256; i++)
            if (data[i]) acc = App(kons, acc, new SchemeChar(i));
        return acc;
    }

    private static object? CharSetForEach(object? proc, object? cs)
    {
        var data = CharsetData(cs);
        for (int i = 0; i < 256; i++)
            if (data[i]) App(proc, new SchemeChar(i));
        return Const.VOID;
    }

    private static object? CharSetMap(object? proc, object? cs)
    {
        var data = CharsetData(cs);
        var result = new bool[256];
        for (int i = 0; i < 256; i++)
        {
            if (data[i])
            {
                var r = App(proc, new SchemeChar(i));
                int cp;
                if (r is SchemeChar rc) cp = rc.Codepoint;
                else if (r is string str && str.Length > 0) cp = str[0];
                else throw new SchemeException("char-set-map: proc must return a char");
                if (cp < 256) result[cp] = true;
            }
        }
        return result;
    }

    private static object? CharSetHash(object?[] args)
    {
        var cs = CharsetData(args[0]);
        long bound = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 65536;
        long h = 0;
        for (int i = 0; i < 256; i++)
            if (cs[i]) h = (h * 41 + i) % bound;
        return h;
    }

    private static object? CharSetEqual(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        var first = CharsetData(args[0]);
        for (int k = 1; k < args.Length; k++)
        {
            var other = CharsetData(args[k]);
            for (int i = 0; i < 256; i++)
                if (first[i] != other[i]) return Const.FALSE;
        }
        return Const.TRUE;
    }
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
    private static object? ReverseListToVector(object?[] args)
    {
        var items = args[0].Cells().ToList();
        items.Reverse();
        return new SchemeVector(items);
    }

    private static object? VectorFill(object?[] args)
    {
        var v = (SchemeVector)args[0]!;
        int start = args.Length > 2 ? NumericHelper.ToInt(args[2]) : 0;
        int end = args.Length > 3 ? NumericHelper.ToInt(args[3]) : v.Length;
        for (int i = start; i < end && i < v.Length; i++) v[i] = args[1];
        return Const.VOID;
    }

    private static object? VectorMap(object?[] args)
    {
        var fn = args[0];
        var vdata = args[1..].Select(a => ((SchemeVector)a!).Data).ToList();
        int len = vdata[0].Count;
        var result = new List<object?>();
        for (int i = 0; i < len; i++)
        {
            var cargs = vdata.Select(d => d[i]).ToArray();
            result.Add(App(fn, cargs));
        }
        return new SchemeVector(result);
    }

    private static object? VectorMapBang(object?[] args)
    {
        var fn = args[0];
        var v = (SchemeVector)args[1]!;
        for (int i = 0; i < v.Length; i++) v[i] = App(fn, (long)i, v[i]);
        return v;
    }

    private static object? VectorForEach(object?[] args)
    {
        var fn = args[0];
        var vdata = args[1..].Select(a => ((SchemeVector)a!).Data).ToList();
        int len = vdata[0].Count;
        for (int i = 0; i < len; i++)
        {
            var cargs = vdata.Select(d => d[i]).ToArray();
            App(fn, cargs);
        }
        return Const.VOID;
    }

    private static object? VectorCount(object? pred, object? v)
    {
        int n = 0;
        foreach (var x in ((SchemeVector)v!).Data)
            if (ReferenceEquals(App(pred, x), Const.TRUE)) n++;
        return (long)n;
    }

    private static object? VectorAnyEvery(object?[] args, bool every)
    {
        var pred = args[0];
        foreach (var x in ((SchemeVector)args[1]!).Data)
        {
            var r = App(pred, x);
            if (every) { if (ReferenceEquals(r, Const.FALSE)) return Const.FALSE; }
            else { if (!ReferenceEquals(r, Const.FALSE)) return Const.TRUE; }
        }
        return every ? Const.TRUE : Const.FALSE;
    }

    private static object? VectorFold(object?[] args, bool right)
    {
        var fn = args[0];
        object? acc = args[1];
        var data = ((SchemeVector)args[2]!).Data;
        if (right)
        {
            for (int i = data.Count - 1; i >= 0; i--) acc = App(fn, (long)i, data[i], acc);
        }
        else
        {
            for (int i = 0; i < data.Count; i++) acc = App(fn, (long)i, data[i], acc);
        }
        return acc;
    }

    private static object? VectorUnfold(object?[] args)
    {
        var fn = args[0];
        int n = NumericHelper.ToInt(args[1]);
        object? s = args[2];
        var result = new List<object?>();
        for (int i = 0; i < n; i++)
        {
            var r = App(fn, (long)i, s);
            if (r is Cell c) { result.Add(c.Car); s = c.Cdr; }
            else if (r is SchemeVector v && v.Length >= 2)
            {
                // The host represents multiple values as a vector.
                result.Add(v[0]);
                s = v.Length == 2 ? v[1] : v.Data.Skip(1).ToCell();
            }
            else if (r is System.Runtime.CompilerServices.ITuple t && t.Length >= 2)
            {
                result.Add(t[0]);
                if (t.Length == 2) s = t[1];
                else
                {
                    object? tail = Const.NIL;
                    for (int j = t.Length - 1; j >= 1; j--)
                        tail = new Cell(t[j], tail);
                    s = tail;
                }
            }
            else { result.Add(r); s = r; }
        }
        return new SchemeVector(result);
    }

    private static object? VectorIndex(object? pred, object? v)
    {
        var data = ((SchemeVector)v!).Data;
        for (int i = 0; i < data.Count; i++)
            if (!ReferenceEquals(App(pred, data[i]), Const.FALSE)) return (long)i;
        return Const.FALSE;
    }

    private static object? VectorSkip(object? pred, object? v)
    {
        var data = ((SchemeVector)v!).Data;
        for (int i = 0; i < data.Count; i++)
            if (ReferenceEquals(App(pred, data[i]), Const.FALSE)) return (long)i;
        return (long)data.Count;
    }

    private static object? VectorSwap(object?[] args)
    {
        var v = (SchemeVector)args[0]!;
        int i = NumericHelper.ToInt(args[1]);
        int j = NumericHelper.ToInt(args[2]);
        (v[j], v[i]) = (v[i], v[j]);
        return Const.VOID;
    }

    private static object? VectorReverseBang(object?[] args)
    {
        var v = (SchemeVector)args[0]!;
        for (int i = 0, j = v.Length - 1; i < j; i++, j--)
            (v[i], v[j]) = (v[j], v[i]);
        return Const.VOID;
    }

    private static object? VectorAppend(object?[] args)
    {
        var result = new List<object?>();
        foreach (var v in args) result.AddRange(((SchemeVector)v!).Data);
        return new SchemeVector(result);
    }

    private static object? VectorCopy(object?[] args)
    {
        var v = (SchemeVector)args[0]!;
        int start = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
        int end = args.Length > 2 ? NumericHelper.ToInt(args[2]) : v.Length;
        return new SchemeVector(v.Data.GetRange(start, end - start));
    }

    private static object? VectorCopyBang(object?[] args)
    {
        var target = (SchemeVector)args[0]!;
        int tstart = NumericHelper.ToInt(args[1]);
        var src = (SchemeVector)args[2]!;
        int sstart = args.Length > 3 ? NumericHelper.ToInt(args[3]) : 0;
        int send = args.Length > 4 ? NumericHelper.ToInt(args[4]) : src.Length;
        for (int i = sstart; i < send; i++)
        {
            int idx = tstart + i - sstart;
            if (idx < target.Length) target[idx] = src[i];
        }
        return Const.VOID;
    }

    private static object? VectorConcat(object? vecs)
    {
        var result = new List<object?>();
        foreach (var v in vecs.Cells())
        {
            if (v is SchemeVector sv) result.AddRange(sv.Data);
        }
        return new SchemeVector(result);
    }

    private static object? VectorReverse(object?[] args)
    {
        var v = (SchemeVector)args[0]!;
        int start = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
        int end = args.Length > 2 ? NumericHelper.ToInt(args[2]) : v.Length;
        var slice = v.Data.GetRange(start, end - start);
        slice.Reverse();
        var result = new List<object?>(v.Data);
        for (int i = 0; i < slice.Count; i++) result[start + i] = slice[i];
        return new SchemeVector(result);
    }

    private static object? VectorSort(object?[] args)
    {
        var pred = args[0];
        var v = (SchemeVector)args[1]!;
        var items = v.Data.ToList();
        StableSortC(items, pred);
        return new SchemeVector(items);
    }

    private static object? VectorEqual(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        var eq = args[0];
        var first = ((SchemeVector)args[1]!).Data;
        for (int k = 2; k < args.Length; k++)
        {
            var other = ((SchemeVector)args[k]!).Data;
            if (other.Count != first.Count) return Const.FALSE;
            for (int i = 0; i < first.Count; i++)
                if (!ReferenceEquals(App(eq, first[i], other[i]), Const.TRUE)) return Const.FALSE;
        }
        return Const.TRUE;
    }
    private static long NextRandom(int limit)
    {
        _extRandomState = (1103515245L * _extRandomState + 12345) & 0x7fffffff;
        return limit <= 0 ? 0 : _extRandomState % limit;
    }

    private static void SeedRandom(long seed) => _extRandomState = seed;



    private static object? ExactNonnegativeIntegerP(object?[] args)
        => args[0] is long l && l >= 0 || args[0] is int i && i >= 0 || args[0] is BigInteger bi && bi >= 0 ? Const.TRUE : Const.FALSE;

    private static object? PSubstringCount(object?[] args)
    {
        var s = ToStr(args[0]); var sub = ToStr(args[1]);
        if (sub.Length == 0) return 0L;
        long count = 0;
        for (var at = 0; (at = s.IndexOf(sub, at, StringComparison.Ordinal)) >= 0; at++) count++;
        return count;
    }

    private static object? PWriteString(object?[] args)
    {
        var s = args[0] is SchemeString ss ? ss.ToString() : ToStr(args[0]);
        object? port = null;
        int start = 0, end = s.Length;
        if (args.Length >= 3) { port = args[1]; start = NumericHelper.ToInt(args[2]); end = args.Length > 3 ? NumericHelper.ToInt(args[3]) : s.Length; }
        else if (args.Length == 2) { port = args[1]; }
        else if (args.Length == 1) { }
        var sub = s[start..Math.Min(end, s.Length)];
        if (port is ITuple t && t.Length > 2 && t[0] is "port" && t[2] is StreamWriter sw) { sw.Write(sub); sw.Flush(); }
        else if (port is ITuple t2 && t2.Length > 2 && t2[0] is "port" && t2[2] is StringBuilder sb) sb.Append(sub);
        else Console.Write(sub);
        return Const.VOID;
    }

    private static object? TreeToList(object? tree)
    {
        var result = new List<object?>();
        void Visit(object? node)
        {
            if (node is Cell c) { Visit(c.Car); Visit(c.Cdr); }
            else if (node is not Nil) result.Add(node);
        }
        Visit(tree);
        return result.ToCell();
    }

    private static object? UcsRangeCharSet(object?[] args)
    {
        int lower = NumericHelper.ToInt(args[0]);
        int upper = NumericHelper.ToInt(args[1]);
        var result = new bool[256];
        for (int cp = Math.Max(0, lower); cp < Math.Min(256, upper); cp++) result[cp] = true;
        return result;
    }

    private static object? Transduce(object? xform, object? reducer, object? init, object? input, string kind)
    {
        var xfReducer = App(xform, reducer);
        object? acc = init;
        IEnumerable<object?> values = kind switch
        {
            "vector" when input is SchemeVector v => v.Data,
            "string" => ToStr(input).EnumerateRunes().Select(r => (object?)new SchemeChar(r.Value)),
            _ => input.Cells()
        };
        foreach (var value in values) acc = App(xfReducer, acc, value);
        return App(xfReducer, acc);
    }

    private static object? NumEqual(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        for (int i = 1; i < args.Length; i++)
            if (!NumericHelper.IsZero(NumericHelper.Sub(args[0], args[i]))) return Const.FALSE;
        return Const.TRUE;
    }

    private static object? BoolEqual(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        for (int i = 1; i < args.Length; i++)
            if (!Equals(args[0], args[i])) return Const.FALSE;
        return Const.TRUE;
    }

    private static bool FiniteP(object? x)
    {
        return x is int or long or BigInteger or SchemeFraction ||
               (x is double d && !double.IsNaN(d) && !double.IsInfinity(d)) ||
               (x is Complex c && !double.IsNaN(c.Real) && !double.IsInfinity(c.Real) && !double.IsNaN(c.Imaginary) && !double.IsInfinity(c.Imaginary));
    }

    private static object? SchemeLcm(object?[] args)
    {
        if (args.Length == 0) return 1L;
        object? r = args[0];
        for (int i = 1; i < args.Length; i++)
        {
            var g = SchemeGcd2(r, args[i]);
            r = NumericHelper.Div(NumericHelper.Mul(r, args[i]), g);
        }
        return r;
    }

    private static long Gcd2(object? a, object? b)
    {
        long x = Math.Abs(NumericHelper.ToLong(a));
        long y = Math.Abs(NumericHelper.ToLong(b));
        while (y != 0) { var t = x % y; x = y; y = t; }
        return x;
    }

    private static object? SchemeGcd2(object? a, object? b)
    {
        var fa = NumericHelper.ToFraction(a); var fb = NumericHelper.ToFraction(b);
        var den = BigInteger.Abs(fa.Den / BigInteger.GreatestCommonDivisor(fa.Den, fb.Den) * fb.Den);
        var result = new SchemeFraction(BigInteger.GreatestCommonDivisor(fa.Num, fb.Num), den);
        return result.Den == 1 ? (object?)(long)result.Num : result;
    }

    private static object? SymbolEqual(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        foreach (var a in args)
            if (a is not Sym) return Const.FALSE;
        var first = ((Sym)args[0]!).Name;
        for (int i = 1; i < args.Length; i++)
            if (((Sym)args[i]!).Name != first) return Const.FALSE;
        return Const.TRUE;
    }

    private static object? CartesianProduct(object?[] args)
    {
        var lists = args.Select(a => a.Cells().ToList()).ToList();
        List<List<object?>> result = [[]];
        foreach (var lst in lists)
        {
            var next = new List<List<object?>>();
            foreach (var r in result)
                foreach (var x in lst)
                {
                    var nr = new List<object?>(r) { x };
                    next.Add(nr);
                }
            result = next;
        }
        return result.Select(r => r.ToCell()).ToCell();
    }

    private static object? Unfold(object?[] args, bool right)
    {
        var p = args[0];
        var f = args[1];
        var g = args[2];
        object? s = args[3];
        object? tail = args.Length > 4 ? args[4] : null;
        var result = new List<object?>();
        while (!ReferenceEquals(App(p, s), Const.TRUE))
        {
            result.Add(App(f, s));
            s = App(g, s);
        }
        if (right)
        {
            object? cell = tail ?? Const.NIL;
            if (tail is not null)
            {
                var t = tail is Func<object?[], object?> tf ? tf([]) : tail;
                if (t is Cell tc) cell = tc;
            }
            for (int i = 0; i < result.Count; i++) cell = new Cell(result[i], cell);
            return cell;
        }
        return result.ToCell();
    }

    private static bool IsErrorType(object? x)
    {
        if (x is ErrorObject) return true;
        if (x is ITuple t && t.Length > 2 && t[1] is Sym s && s.Name == "error") return true;
        return false;
    }

    private static bool IsFileError(object? x)
    {
        if (x is ErrorObject) return true;
        if (x is ITuple t && t.Length > 2 && t[1] is Sym s && s.Name == "file") return true;
        return false;
    }

    private static bool IsReadError(object? x)
    {
        if (x is ITuple t && t.Length > 2 && t[1] is Sym s && s.Name == "read") return true;
        return false;
    }

    private static bool HasConditionType(object? c, object? t)
    {
        if (c is ITuple ct && ct.Length > 2 && ct[1] is Sym s && s.Name == ToStr(t)) return true;
        return false;
    }

    private static bool IsConditionType(object? x)
    {
        if (x is ErrorObject) return true;
        if (x is ITuple t && t.Length > 2 && t[0] is Sym s && s.Name == "condition") return true;
        return false;
    }

    private static string ReportString(object? c)
    {
        if (c is ITuple ct && ct.Length > 2) return ToStr(ct[2]);
        return ToStr(c);
    }

    private static bool MaybeP(object? x)
    {
        if (x is Nil) return true;
        if (ReferenceEquals(x, Const.FALSE)) return true;
        if (x is Cell c) return c.Cdr is Nil;
        return false;
    }

    private static object? CharReady(object?[] args)
    {
        return Const.TRUE;
    }

    private static object? ReadU8(object?[] args, bool peek)
    {
        if (args.Length > 0 && args[0] is ITuple t && t.Length > 2 && t[0] is "port")
        {
            if (t[2] is StringPort sp)
            {
                if (sp.Pos >= sp.Data.Length) return Const.EOF;
                int b = sp.Data[sp.Pos];
                if (!peek) sp.Pos++;
                return (long)b;
            }
            if (t[2] is BytePort bp)
            {
                if (bp.Pos >= bp.Data.Length) return Const.EOF;
                var b = bp.Data[bp.Pos];
                if (!peek) bp.Pos++;
                return (long)b;
            }
            if (t[2] is StreamReader sr)
            {
                int b = sr.Peek();
                if (!peek && b >= 0) sr.Read();
                return b >= 0 ? (object?)(long)b : Const.EOF;
            }
        }
        return Const.EOF;
    }

    private static object? WriteU8(object?[] args)
    {
        var b = NumericHelper.ToInt(args[0]) & 0xFF;
        if (args.Length > 1 && args[1] is ITuple t && t.Length > 2 && t[0] is "port")
        {
            if (t[2] is StreamWriter sw) { sw.Write((char)b); sw.Flush(); }
            else if (t[2] is StringBuilder sb) sb.Append((char)b);
            else if (t[2] is BytePort bp) bp.Append((byte)b);
        }
        else
        {
            Console.Write((char)b);
        }
        return Const.VOID;
    }

    private static object? ReadBytevector(object?[] args, bool intoExisting)
    {
        SchemeBytevector? target = intoExisting ? AsBytevector(args[0]) : null;
        var count = intoExisting ? target!.Length : NumericHelper.ToInt(args[0]);
        var port = intoExisting ? args[1] : args[1];
        var bytes = new List<byte>();
        while (bytes.Count < count)
        {
            var value = ReadU8([port], false);
            if (value is Eof) break;
            bytes.Add((byte)NumericHelper.ToInt(value));
        }
        if (target is not null)
        {
            for (var i = 0; i < bytes.Count; i++) target[i] = bytes[i];
            return (long)bytes.Count;
        }
        return new SchemeBytevector(bytes.ToArray());
    }

    private static object? WriteBytevector(object?[] args)
    {
        var bytes = AsBytevector(args[0]);
        var port = args.Length > 1 ? args[1] : null;
        for (var i = 0; i < bytes.Length; i++) WriteU8([bytes[i], port]);
        return Const.VOID;
    }

    private static object? JsonRead(object?[] args)
    {
        if (args.Length > 0 && args[0] is ITuple t && t.Length > 2 && t[0] is "port" && t[2] is StringPort sp)
        {
            var s = sp.Data[sp.Pos..].Trim();
            if (s.Length == 0) return Const.EOF;
            try
            {
                var val = System.Text.Json.JsonSerializer.Deserialize<object?>(s);
                return JsonToScheme(val);
            }
            catch { return Const.EOF; }
        }
        var line = Console.ReadLine();
        if (line is null) return Const.EOF;
        try
        {
            var val = System.Text.Json.JsonSerializer.Deserialize<object?>(line);
            return JsonToScheme(val);
        }
        catch { return Const.EOF; }
    }

    private static object? JsonWrite(object?[] args)
    {
        var js = SchemeToJson(args[0]);
        var str = System.Text.Json.JsonSerializer.Serialize(js);
        if (args.Length > 1 && args[1] is ITuple t && t.Length > 2 && t[0] is "port" && t[2] is StreamWriter sw)
        {
            sw.Write(str);
            sw.Flush();
            return Const.VOID;
        }
        return new SchemeString(str);
    }

    private static object? JsonToScheme(object? v)
    {
        return v switch
        {
            null => Const.NIL,
            System.Text.Json.JsonElement je => je.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number => je.TryGetInt64(out long l) ? (object?)l : (je.TryGetDouble(out double d) ? d : Const.NIL),
                System.Text.Json.JsonValueKind.True => Const.TRUE,
                System.Text.Json.JsonValueKind.False => Const.FALSE,
                System.Text.Json.JsonValueKind.String => new SchemeString(je.GetString()!),
                System.Text.Json.JsonValueKind.Array => je.EnumerateArray().Select(e => JsonToScheme(e)).ToCell(),
                System.Text.Json.JsonValueKind.Object => JsonObjectToAlist(je),
                _ => Const.NIL
            },
            long l => l,
            double d => d,
            string s => new SchemeString(s),
            bool b => b ? Const.TRUE : Const.FALSE,
            _ => Const.NIL
        };
    }

    private static object? JsonObjectToAlist(System.Text.Json.JsonElement obj)
    {
        var items = new List<object?>();
        foreach (var prop in obj.EnumerateObject())
            items.Add(new Cell(Sym.Intern(prop.Name), JsonToScheme(prop.Value)));
        return items.ToCell();
    }

    private static object? SchemeToJson(object? v)
    {
        return v switch
        {
            null or Nil => null,
            Void => null,
            Sym s when s == Const.TRUE => true,
            Sym s2 when s2 == Const.FALSE => false,
            Sym sy => sy.Name,
            SchemeString ss => ss.ToString(),
            string str => str,
            long or int or BigInteger or double or float => v,
            SchemeFraction fr => fr.ToString(),
            Cell c => c.Cells().Select(SchemeToJson).ToList(),
            SchemeVector sv => sv.Data.Select(SchemeToJson).ToList(),
            _ => Printer.Format(v)
        };
    }

    private static object? Mapping(object?[] args)
    {
        var items = args.Select(a => a).ToList();
        object? result = Const.NIL;
        for (int i = items.Count - 1; i > 0; i -= 2)
        {
            var pair = new Cell(items[i - 1], items[i]);
            result = new Cell(pair, result);
        }
        return result;
    }

    private static bool MappingP(object? x)
    {
        if (x is Nil) return true;
        var cur = x;
        while (cur is Cell c)
        {
            if (c.Car is not Cell) return false;
            cur = c.Cdr;
        }
        return true;
    }

    private static object? GeneratorAppend(object?[] args)
    {
        var gens = args.ToList();
        int idx = 0;
        return (Func<object?[], object?>)(_ =>
        {
            while (idx < gens.Count)
            {
                var v = App(gens[idx]);
                if (!(v is Eof)) return v;
                idx++;
            }
            return Const.EOF;
        });
    }

    private static object? GeneratorDrop(object?[] args)
    {
        var g = args[0];
        int n = NumericHelper.ToInt(args[1]);
        for (int i = 0; i < n; i++) { var v = App(g); if (v is Eof) break; }
        return g;
    }

    private static object? GeneratorFold(object?[] args)
    {
        var f = args[0];
        object? acc = args[1];
        var g = args[2];
        while (true)
        {
            var v = App(g);
            if (v is Eof) break;
            acc = App(f, v, acc);
        }
        return acc;
    }

    private static object? StreamNext(object? s)
    {
        if (s is not Cell c) return Const.NIL;
        if (c.Cdr is Promise) return ForcePromiseEval(c.Cdr);
        if (c.Cdr is Func<object?[], object?> f) return f([]);
        return c.Cdr;
    }

    private static object? ForcePromiseEval(object? prom)
    {
        return Evaluator.Eval(new Cell(Sym.Intern("force"), new Cell(prom, Const.NIL)), Evaluator.GlobalEnv);
    }

    private static object? ForcePromise(Promise p)
    {
        if (p.Forced) return p.Val;
        p.Val = p.Thunk is not null ? p.Thunk() : Const.NIL;
        p.Forced = true;
        return p.Val;
    }

    private static object? StreamRef(object? s, int n)
    {
        var cur = s;
        for (int i = 0; i < n; i++)
        {
            if (cur is not Cell) return Const.NIL;
            cur = StreamNext(cur);
        }
        return cur is Cell c ? c.Car : Const.NIL;
    }

    private static object? StreamMap(object? f, object? s)
    {
        if (s is not Cell c) return Const.NIL;
        return new Cell(App(f, c.Car), (Func<object?[], object?>)(_ => StreamMap(f, StreamNext(s))));
    }

    private static object? StreamFilter(object? pred, object? s)
    {
        var cur = s;
        while (cur is Cell c && ReferenceEquals(App(pred, c.Car), Const.FALSE)) cur = StreamNext(cur);
        if (cur is not Cell hit) return Const.NIL;
        return new Cell(hit.Car, (Func<object?[], object?>)(_ => StreamFilter(pred, StreamNext(cur))));
    }

    private static object? StreamTake(object? s, int n)
    {
        var result = new List<object?>();
        var cur = s;
        for (int i = 0; i < n; i++)
        {
            if (cur is not Cell c) break;
            result.Add(c.Car);
            cur = StreamNext(cur);
        }
        return result.ToCell();
    }

    private static object? StreamToList(object? s)
    {
        var result = new List<object?>();
        var cur = s;
        while (cur is Cell c)
        {
            result.Add(c.Car);
            cur = StreamNext(cur);
        }
        return result.ToCell();
    }

    private static object? ListToStream(object? lst)
    {
        object? Build(object? rest)
        {
            if (rest is not Cell c) return Const.NIL;
            var car = c.Car;
            var tail = c.Cdr;
            return new Cell(car, (Func<object?[], object?>)(_ => Build(tail)));
        }
        return Build(lst);
    }

    private static object? NatStream(object?[] args)
    {
        long start = args.Length > 0 ? NumericHelper.ToInt(args[0]) : 0;
        object? Make(long n) => new Cell(n, (Func<object?[], object?>)(_ => Make(n + 1)));
        return Make(start);
    }

    private static object? Sieve(object? s)
    {
        if (s is not Cell c) return Const.NIL;
        var n = c.Car;
        long m = NumericHelper.ToLong(n);
        var rest = StreamNext(s);
        object? MakeNext()
        {
            var cur = rest;
            while (cur is Cell cc)
            {
                if (NumericHelper.ToLong(cc.Car) % m != 0)
                {
                    var val = cc.Car;
                    var nxt = StreamNext(cur);
                    return new Cell(val, (Func<object?[], object?>)(_ => Sieve(nxt)));
                }
                cur = StreamNext(cur);
            }
            return Const.NIL;
        }
        return new Cell(n, (Func<object?[], object?>)(_ => MakeNext()));
    }

    private static object? Primes()
    {
        return Sieve(NatStream([2L]));
    }
    private static object? Iota(object?[] args)
    {
        long n = NumericHelper.ToInt(args[0]);
        long s = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
        long st = args.Length > 2 ? NumericHelper.ToInt(args[2]) : 1;
        var res = new List<object?>();
        for (long i = 0; i < n; i++) res.Add(s + i * st);
        return res.ToCell();
    }

    private static object? ConsStar(object?[] args)    {
        if (args.Length == 0) return Const.NIL;
        object? r = args[^1];
        for (int i = args.Length - 2; i >= 0; i--) r = new Cell(args[i], r);
        return r;
    }

    private static object? CopyList(object? lst)
    {
        if (lst is not Cell) return lst;
        var head = new Cell(((Cell)lst).Car, Const.NIL);
        var tail = head;
        var cur = ((Cell)lst).Cdr;
        while (cur is Cell c)
        {
            tail.Cdr = new Cell(c.Car, Const.NIL);
            tail = (Cell)tail.Cdr;
            cur = c.Cdr;
        }
        tail.Cdr = cur; // preserve dotted tail
        return head;
    }

    private static object? Nth(object? lst, int n)
    {
        var cur = lst;
        for (int i = 0; i < n; i++)
        {
            if (cur is not Cell c || c.Cdr is not Cell) return Const.FALSE;
            cur = c.Cdr;
        }
        return cur is Cell cc ? cc.Car : Const.FALSE;
    }

    private static object? TakeList(object? lst, int n)
    {
        var res = new List<object?>();
        var cur = lst;
        int i = 0;
        while (cur is Cell c && i < n) { res.Add(c.Car); cur = c.Cdr; i++; }
        return res.ToCell();
    }

    private static object? DropList(object? lst, int n)
    {
        var cur = lst;
        int i = 0;
        while (cur is Cell c && i < n) { cur = c.Cdr; i++; }
        return cur;
    }

    private static object? TakeRight(object? lst, int n)
    {
        if (n <= 0 || lst is not Cell) return Const.NIL;
        var items = lst.Cells();
        if (n >= items.Count) return items.ToCell();
        return items.Skip(items.Count - n).ToCell();
    }

    private static object? DropRight(object? lst, int n)
    {
        if (n <= 0 || lst is not Cell) return lst;
        var items = lst.Cells();
        if (n >= items.Count) return Const.NIL;
        return items.Take(items.Count - n).ToCell();
    }

    private static object? TakeWhileList(object? pred, object? lst)
    {
        var res = new List<object?>();
        var cur = lst;
        while (cur is Cell c && ReferenceEquals(App(pred, c.Car), Const.TRUE)) { res.Add(c.Car); cur = c.Cdr; }
        return res.ToCell();
    }

    private static object? DropWhileList(object? pred, object? lst)
    {
        var cur = lst;
        while (cur is Cell c && ReferenceEquals(App(pred, c.Car), Const.TRUE)) cur = c.Cdr;
        return cur;
    }

    private static object? LastList(object? lst)
    {
        var cur = lst;
        while (cur is Cell c && c.Cdr is Cell) cur = c.Cdr;
        return cur is Cell c2 ? c2.Car : Const.FALSE;
    }

    private static object? ButLast(object? lst)
    {
        var items = lst.Cells();
        if (items.Count <= 1) return Const.NIL;
        return items.Take(items.Count - 1).ToCell();
    }

    private static object? LengthPlus(object? lst)
    {
        if (lst is not Cell) return lst is Nil ? (object?)0L : Const.FALSE;
        var cur = lst;
        int n = 0;
        while (cur is Cell c) { n++; cur = c.Cdr; }
        return cur is Nil ? (object?)(long)n : Const.FALSE;
    }

    private static object? ListTabulate(object?[] args)
    {
        int n = NumericHelper.ToInt(args[0]);
        var fn = args[1];
        var res = new List<object?>();
        for (int i = 0; i < n; i++) res.Add(App(fn, (long)i));
        return res.ToCell();
    }

    private static object? ListIndex(object? pred, object? lst)
    {
        int i = 0;
        foreach (var x in lst.Cells())
        {
            if (ReferenceEquals(App(pred, x), Const.TRUE)) return (long)i;
            i++;
        }
        return Const.FALSE;
    }

    private static object? ListSetBang(object?[] args)
    {
        var cur = args[0];
        int i = 0;
        while (cur is Cell c)
        {
            if (i == NumericHelper.ToInt(args[1])) { c.Car = args[2]; return Const.VOID; }
            cur = c.Cdr; i++;
        }
        throw new SchemeException("list-set!: index out of bounds");
    }

    private static object? ListFind(object? pred, object? lst)
    {
        foreach (var x in lst.Cells())
            if (ReferenceEquals(App(pred, x), Const.TRUE)) return x;
        return Const.FALSE;
    }

    private static object? ListFindIndex(object? pred, object? lst)
    {
        int i = 0;
        foreach (var x in lst.Cells())
        {
            if (ReferenceEquals(App(pred, x), Const.TRUE)) return (long)i;
            i++;
        }
        return Const.FALSE;
    }

    private static object? ListAny(object?[] args)
    {
        if (args.Length < 2) return Const.FALSE;
        var pred = args[0];
        var curs = args[1..];
        while (true)
        {
            var curArgs = new List<object?>();
            foreach (var c in curs)
            {
                if (c is not Cell cc) return Const.FALSE;
                curArgs.Add(cc.Car);
            }
            if (ReferenceEquals(App(pred, curArgs.ToArray()), Const.TRUE)) return Const.TRUE;
            for (int i = 0; i < curs.Length; i++) curs[i] = ((Cell)curs[i]!).Cdr;
        }
    }

    private static object? ListEvery(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        var pred = args[0];
        var curs = args[1..];
        while (true)
        {
            var curArgs = new List<object?>();
            foreach (var c in curs)
            {
                if (c is not Cell cc) return Const.TRUE;
                curArgs.Add(cc.Car);
            }
            if (!ReferenceEquals(App(pred, curArgs.ToArray()), Const.TRUE)) return Const.FALSE;
            for (int i = 0; i < curs.Length; i++) curs[i] = ((Cell)curs[i]!).Cdr;
        }
    }

    private static object? ListFilterMap(object? fn, object? lst)
    {
        var res = new List<object?>();
        foreach (var x in lst.Cells())
        {
            var r = App(fn, x);
            if (!ReferenceEquals(r, Const.FALSE)) res.Add(r);
        }
        return res.ToCell();
    }

    private static object? ListPartition(object? pred, object? lst)
    {
        var yes = new List<object?>();
        var no = new List<object?>();
        foreach (var x in lst.Cells())
        {
            if (ReferenceEquals(App(pred, x), Const.TRUE)) yes.Add(x);
            else no.Add(x);
        }
        return new Cell(yes.ToCell(), new Cell(no.ToCell(), Const.NIL));
    }

    private static object? ListRemove(object? pred, object? lst)
    {
        var res = new List<object?>();
        foreach (var x in lst.Cells())
            if (!ReferenceEquals(App(pred, x), Const.TRUE)) res.Add(x);
        return res.ToCell();
    }

    private static object? FlattenList(object? lst)
    {
        var res = new List<object?>();
        FlattenRec(lst, res);
        return res.ToCell();
    }

    private static void FlattenRec(object? x, List<object?> res)
    {
        if (x is Cell cc) { FlattenRec(cc.Car, res); FlattenRec(cc.Cdr, res); }
        else if (x is not Nil) res.Add(x);
    }

    private static object? Zip(object?[] args)
    {
        var result = new List<object?>();
        var curs = args.ToList();
        while (curs.All(c => c is Cell))
        {
            var row = new List<object?>();
            for (int i = 0; i < curs.Count; i++) { row.Add(((Cell)curs[i]!).Car); curs[i] = ((Cell)curs[i]!).Cdr; }
            result.Add(row.ToCell());
        }
        return result.ToCell();
    }

    private static object? SortList(object?[] args)
    {
        var less = args[0];
        var items = args[1].Cells().ToList();
        StableSortC(items, less);
        return items.ToCell();
    }

    private static void StableSortC(List<object?> items, object? less)
    {
        for (int i = 1; i < items.Count; i++)
        {
            var key = items[i];
            int j = i - 1;
            while (j >= 0 && IsLessC(less, key, items[j])) { items[j + 1] = items[j]; j--; }
            items[j + 1] = key;
        }
    }

    private static bool IsLessC(object? less, object? a, object? b)
    {
        var r = App(less, a, b);
        return !ReferenceEquals(r, Const.FALSE) && r is not Nil;
    }

    private static object? ListEqual(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        var eq = args[0];
        var first = args[1].Cells().ToList();
        for (int k = 2; k < args.Length; k++)
        {
            var other = args[k].Cells().ToList();
            if (other.Count != first.Count) return Const.FALSE;
            for (int i = 0; i < first.Count; i++)
                if (!ReferenceEquals(App(eq, first[i], other[i]), Const.TRUE)) return Const.FALSE;
        }
        return Const.TRUE;
    }

    private static object? SortedP(object?[] args)
    {
        var less = args[0];
        var items = args[1].Cells().ToList();
        for (int i = 1; i < items.Count; i++)
            if (!IsLessC(less, items[i - 1], items[i])) return Const.FALSE;
        return Const.TRUE;
    }

    private static object? Merge(object?[] args)
    {
        var pred = args[0];
        var a = args[1].Cells().ToList();
        var b = args[2].Cells().ToList();
        var res = new List<object?>();
        int i = 0, j = 0;
        while (i < a.Count && j < b.Count)
        {
            if (ReferenceEquals(App(pred, a[i], b[j]), Const.TRUE)) res.Add(a[i++]);
            else res.Add(b[j++]);
        }
        while (i < a.Count) res.Add(a[i++]);
        while (j < b.Count) res.Add(b[j++]);
        return res.ToCell();
    }

    private static object? FoldLeft(object? f, object? init, object? lst)
    {
        object? acc = init;
        foreach (var x in lst.Cells()) acc = App(f, acc, x);
        return acc;
    }

    private static object? FoldRight(object? f, object? init, object? lst)
    {
        var stack = new List<object?>();
        foreach (var x in lst.Cells()) stack.Add(x);
        object? acc = init;
        for (int i = stack.Count - 1; i >= 0; i--) acc = App(f, stack[i], acc);
        return acc;
    }

    private static object? CountFn(object? pred, object? lst)
    {
        int n = 0;
        foreach (var x in lst.Cells())
            if (ReferenceEquals(App(pred, x), Const.TRUE)) n++;
        return (long)n;
    }

    private static object? DeleteFn(object?[] args)
    {
        var x = args[0];
        var eq = args.Length > 2 ? args[2] : null;
        var res = new List<object?>();
        foreach (var y in args[1].Cells())
        {
            if (eq is not null) { if (!ReferenceEquals(App(eq, x, y), Const.TRUE)) res.Add(y); }
            else if (!ReferenceEquals(y, x) && !(y is not null && y.Equals(x) && y.GetType() == x?.GetType())) res.Add(y);
        }
        return res.ToCell();
    }

    private static object? DeleteDups(object?[] args)
    {
        var eq = args.Length > 1 ? args[1] : null;
        var items = args[0].Cells().ToList();
        var result = new List<object?>();
        foreach (var x in items)
        {
            bool found = false;
            foreach (var y in result)
            {
                if (eq is null ? Equals(x, y) : ReferenceEquals(App(eq, x, y), Const.TRUE)) { found = true; break; }
            }
            if (!found) result.Add(x);
        }
        return result.ToCell();
    }

    private static object? DeleteAssoc(object? key, object? alist)
    {
        var res = new List<object?>();
        foreach (var p in alist.Cells())
        {
            if (p is Cell pc && !ReferenceEquals(pc.Car, key) && !Equals(pc.Car, key)) res.Add(p);
        }
        return res.ToCell();
    }

    private static object? AlistDelete(object?[] args)
    {
        var k = args[0];
        var eq = args.Length > 2 ? args[2] : null;
        var res = new List<object?>();
        foreach (var p in args[1].Cells())
        {
            var pc = p as Cell;
            bool match = eq is not null ? ReferenceEquals(App(eq, k, pc?.Car), Const.TRUE) : ReferenceEquals(pc?.Car, k) || Equals(pc?.Car, k);
            if (!match) res.Add(p);
        }
        return res.ToCell();
    }

    private static object? AppendMap(object?[] args)
    {
        var fn = args[0];
        var result = new List<object?>();
        var curs = args[1..].ToList();
        while (curs.All(c => c is Cell))
        {
            var cargs = new List<object?>();
            for (int i = 0; i < curs.Count; i++) { cargs.Add(((Cell)curs[i]!).Car); curs[i] = ((Cell)curs[i]!).Cdr; }
            var r = App(fn, cargs.ToArray());
            result.AddRange(r.Cells());
        }
        return result.ToCell();
    }

    private static object? AppendRev(object? a, object? b)
    {
        object? cur = b;
        foreach (var x in a.Cells()) cur = new Cell(x, cur);
        return cur;
    }

    private static object? Concatenate(object? lsts)
    {
        var res = new List<object?>();
        foreach (var sub in lsts.Cells()) res.AddRange(sub.Cells());
        return res.ToCell();
    }

    private static object? MapInOrder(object?[] args)
    {
        var fn = args[0];
        var result = new List<object?>();
        var curs = args[1..].ToList();
        while (curs.All(c => c is Cell))
        {
            var cargs = new List<object?>();
            for (int i = 0; i < curs.Count; i++) { cargs.Add(((Cell)curs[i]!).Car); curs[i] = ((Cell)curs[i]!).Cdr; }
            result.Add(App(fn, cargs.ToArray()));
        }
        return result.ToCell();
    }

    private static object? PairForEach(object? f, object? lst)
    {
        var cur = lst;
        while (cur is Cell c) { App(f, cur); cur = c.Cdr; }
        return Const.VOID;
    }

    private static object? Unzip(object? lst, int n)
    {
        var cols = new List<object?>[n];
        for (int i = 0; i < n; i++) cols[i] = new List<object?>();
        foreach (var x in lst.Cells())
        {
            if (x is Cell c)
            {
                var row = c.Cells();
                for (int i = 0; i < n && i < row.Count; i++) cols[i].Add(row[i]);
            }
        }
        if (n == 1) return cols[0].ToCell();
        return cols.Select(c => c.ToCell()).ToCell();
    }

    private static object? Curry(object?[] args)
    {
        var f = args[0];
        var pre = args[1..];
        return (Func<object?[], object?>)(more => App(f, pre.Concat(more).ToArray()));
    }

    private static object? Iterate(object? f, int n, object? x)
    {
        for (int i = 0; i < n; i++) x = App(f, x);
        return x;
    }

    private static object? Range(object?[] args)
    {
        long s = NumericHelper.ToInt(args[0]);
        long e = NumericHelper.ToInt(args[1]);
        long st = args.Length > 2 ? NumericHelper.ToInt(args[2]) : 1;
        var res = new List<object?>();
        for (long i = s; i < e; i += st) res.Add(i);
        return res.ToCell();
    }

    private static object? Interleave(object?[] args)
    {
        var res = new List<object?>();
        var curs = args.ToList();
        while (curs.Any(c => c is Cell))
        {
            for (int i = 0; i < curs.Count; i++)
            {
                if (curs[i] is Cell c) { res.Add(c.Car); curs[i] = c.Cdr; }
            }
        }
        return res.ToCell();
    }

    private static object? MakeCircularList(object?[] args)
    {
        if (args.Length == 0) return Const.NIL;
        var items = args.ToList();
        var head = new Cell(items[0], Const.NIL);
        var tail = head;
        for (int i = 1; i < items.Count; i++)
        {
            tail.Cdr = new Cell(items[i], Const.NIL);
            tail = (Cell)tail.Cdr;
        }
        tail.Cdr = head;
        return head;
    }

    private static bool IsCircularList(object? lst)
    {
        if (lst is not Cell) return false;
        var slow = lst;
        var fast = lst;
        while (fast is Cell fc && fc.Cdr is Cell fc2)
        {
            slow = ((Cell)slow!).Cdr;
            fast = fc2.Cdr;
            if (ReferenceEquals(slow, fast)) return true;
        }
        return false;
    }

    private static bool IsDottedList(object? lst)
    {
        if (lst is Nil) return false;
        if (lst is not Cell) return true;
        var seen = new HashSet<Cell>(ReferenceEqualityComparer.Instance);
        var cur = lst;
        while (cur is Cell c)
        {
            if (!seen.Add(c)) return false; // circular
            cur = c.Cdr;
        }
        return cur is not Nil;
    }

    private static bool IsProperList(object? lst)
    {
        if (lst is Nil) return true;
        if (lst is not Cell) return false;
        var seen = new HashSet<Cell>(ReferenceEqualityComparer.Instance);
        var cur = lst;
        while (cur is Cell c)
        {
            if (!seen.Add(c)) return false; // circular
            cur = c.Cdr;
        }
        return cur is Nil;
    }

    private static object? LsetUnion(object?[] args)
    {
        var eq = args[0];
        var res = new List<object?>();
        foreach (var lst in args[1..])
        {
            foreach (var x in lst.Cells())
            {
                bool found = res.Any(y => ReferenceEquals(App(eq, x, y), Const.TRUE));
                if (!found) res.Add(x);
            }
        }
        return res.ToCell();
    }

    private static object? LsetIntersection(object?[] args)
    {
        var eq = args[0];
        var first = args[1].Cells().ToList();
        var rest = args[2..].Select(l => l.Cells().ToList()).ToList();
        var res = new List<object?>();
        foreach (var x in first)
        {
            if (rest.All(l => l.Any(y => ReferenceEquals(App(eq, x, y), Const.TRUE)))) res.Add(x);
        }
        return res.ToCell();
    }

    private static object? LsetDifference(object?[] args)
    {
        var eq = args[0];
        var first = args[1].Cells().ToList();
        var rest = args[2..].Select(l => l.Cells().ToList()).ToList();
        var res = new List<object?>();
        foreach (var x in first)
        {
            if (!rest.Any(l => l.Any(y => ReferenceEquals(App(eq, x, y), Const.TRUE)))) res.Add(x);
        }
        return res.ToCell();
    }

    private static object? LsetXor(object?[] args)
    {
        var eq = args[0];
        var res = new List<object?>();
        foreach (var lst in args[1..])
        {
            foreach (var x in lst.Cells())
            {
                if (res.Any(y => ReferenceEquals(App(eq, x, y), Const.TRUE)))
                    res.RemoveAll(y => ReferenceEquals(App(eq, x, y), Const.TRUE));
                else res.Add(x);
            }
        }
        return res.ToCell();
    }

    private static object? LsetEqual(object?[] args)
    {
        var eq = args[0];
        var first = args[1].Cells().ToList();
        foreach (var lst in args[2..])
        {
            var other = lst.Cells().ToList();
            if (first.Count != other.Count) return Const.FALSE;
            foreach (var x in first)
                if (!other.Any(y => ReferenceEquals(App(eq, x, y), Const.TRUE))) return Const.FALSE;
        }
        return Const.TRUE;
    }

    private static object? Mem(object? obj, object? lst, bool identity)
    {
        var cur = lst;
        while (cur is Cell c)
        {
            if (identity
                ? (ReferenceEquals(c.Car, obj) || Equals(c.Car, obj))
                : ReferenceEquals(Miniscm.Compiler.JitRuntime.Equal2(c.Car, obj), Const.TRUE))
                return cur;
            cur = c.Cdr;
        }
        return Const.FALSE;
    }

    private static object? MakeListQueue(object?[] args)    {
        var front = args.Length > 0 ? args[0] : Const.NIL;
        return new Cell(Sym.Intern("list-queue"), new Cell(front, Const.NIL));
    }

    private static object? ListQueueFront(object? q)
    {
        if (q is not Cell lq || lq.Cdr is not Cell front) return Const.FALSE;
        return front.Car is Cell c ? c.Car : Const.NIL;
    }

    private static object? ListQueueBack(object? q)
    {
        if (q is not Cell lq || lq.Cdr is not Cell front) return Const.NIL;
        return LastPair(front.Car) is Cell c ? c.Car : Const.NIL;
    }

    private static bool ListQueueEmpty(object? q)
    {
        return q is not Cell lq || lq.Cdr is not Cell front || front.Car is Nil;
    }

    private static object? ListQueueAdd(object?[] args)
    {
        var q = args[0];
        if (q is Cell lq && lq.Cdr is Cell front)
        {
            if (front.Car is Nil) front.Car = new Cell(args[1], Const.NIL);
            else { var last = LastPair(front.Car); ((Cell)last!).Cdr = new Cell(args[1], Const.NIL); }
        }
        return Const.VOID;
    }

    private static object? ListQueueAddFront(object?[] args)
    {
        var q = args[0];
        if (q is Cell lq && lq.Cdr is Cell front) front.Car = new Cell(args[1], front.Car);
        return Const.VOID;
    }

    private static object? ListQueueRemove(object?[] args)
    {
        var q = args[0];
        if (q is Cell lq && lq.Cdr is Cell front && front.Car is Cell c)
        {
            var v = c.Car;
            front.Car = c.Cdr;
            return v;
        }
        return Const.NIL;
    }

    private static object? ListQueueToList(object? q)
    {
        return q is Cell lq && lq.Cdr is Cell front ? front.Car : Const.NIL;
    }

    private static object? ListQueueFirst(object? q)
    {
        return ListQueueFront(q);
    }
}
