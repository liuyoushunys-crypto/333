using System.Numerics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Miniscm.Types;
using Miniscm.Eval;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    static readonly Dictionary<string, SchemeString> MutableStringViews =
        new(ReferenceEqualityComparer.Instance);





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
}
