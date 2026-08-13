using System.Numerics;
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
}
