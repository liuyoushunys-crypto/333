using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Miniscm.Types;
using Miniscm.Eval;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static class PrimitiveRegistry
{
    private static void _b(string name, Func<object?[], object?> fn) => Evaluator.GlobalEnv.Define(name, fn);

    public static void Init()
    {
        // ── Type predicates ──
        _b("null?", args => args[0] is Nil ? Const.TRUE : Const.FALSE);
        _b("pair?", args => args[0] is Cell ? Const.TRUE : Const.FALSE);
        _b("symbol?", args => args[0] is Sym ? Const.TRUE : Const.FALSE);
        _b("string?", args => args[0] is string or SchemeString ? Const.TRUE : Const.FALSE);
        _b("char?", args => args[0] is SchemeChar ? Const.TRUE : Const.FALSE);
        _b("vector?", args => args[0] is SchemeVector ? Const.TRUE : Const.FALSE);
        _b("bytevector?", args => args[0] is SchemeBytevector ? Const.TRUE : Const.FALSE);
        _b("number?", args => args[0] is int or long or BigInteger or double or float or decimal or Complex or SchemeFraction ? Const.TRUE : Const.FALSE);
        _b("integer?", args => args[0] is int or long or BigInteger ? Const.TRUE : Const.FALSE);
        _b("rational?", args => args[0] is SchemeFraction or int or long or BigInteger ? Const.TRUE : Const.FALSE);
        _b("real?", args => args[0] is int or long or BigInteger or SchemeFraction or double or float or decimal ? Const.TRUE : Const.FALSE);
        _b("complex?", args => args[0] is Complex or int or long or BigInteger or SchemeFraction or double or float ? Const.TRUE : Const.FALSE);
        _b("procedure?", args => args[0] is Delegate or LambdaProc or ValueTuple<string, object?> ? Const.TRUE : Const.FALSE);
        _b("boolean?", args => args[0] is Sym s && (s == Const.TRUE || s == Const.FALSE) ? Const.TRUE : Const.FALSE);
        _b("not", args => args[0] is Sym s && s == Const.FALSE ? Const.TRUE : Const.FALSE);
        _b("eof-object", args => Const.EOF);
        _b("eof-object?", args => args[0] is Eof ? Const.TRUE : Const.FALSE);
        _b("void?", args => args[0] is Void ? Const.TRUE : Const.FALSE);
        _b("promise?", args => args[0] is Promise ? Const.TRUE : Const.FALSE);
        _b("input-port?", args => IsPort(args[0], "input") ? Const.TRUE : Const.FALSE);
        _b("output-port?", args => IsPort(args[0], "output") ? Const.TRUE : Const.FALSE);
        _b("port?", args => IsPort(args[0], null) ? Const.TRUE : Const.FALSE);
        _b("box?", args => args[0] is ValueTuple<string, object?> b && b.Item1 == "box" ? Const.TRUE : Const.FALSE);
        _b("eq?", args => ReferenceEquals(args[0], args[1]) || (args[0] is not null && args[0]!.Equals(args[1])) ? Const.TRUE : Const.FALSE);
        _b("eqv?", args =>
        {
            var a = args[0];
            var b = args[1];
            if (ReferenceEquals(a, b)) return Const.TRUE;
            if (a is null || b is null) return Const.FALSE;
            if (a.GetType() == b.GetType())
            {
                if (a is int or long or BigInteger or SchemeFraction or double or Complex)
                    return a.Equals(b) ? Const.TRUE : Const.FALSE;
                if (a is string s) return s == (string)b ? Const.TRUE : Const.FALSE;
                if (a is SchemeChar sc) return sc.Codepoint == ((SchemeChar)b).Codepoint ? Const.TRUE : Const.FALSE;
            }
            return Const.FALSE;
        });
        _b("equal?", args => Eql(args[0], args[1]));

        // ── Pair/Cell operations ──
        _b("cons", args => new Cell(args[0], args[1]));
        _b("car", args => CarFn(args[0]));
        _b("cdr", args => CdrFn(args[0]));
        _b("set-car!", args => { if (args[0] is Cell c) c.Car = args[1]; return Const.VOID; });
        _b("set-cdr!", args => { if (args[0] is Cell c) c.Cdr = args[1]; return Const.VOID; });
        _b("caar", args => CarFn(CarFn(args[0])));
        _b("cadr", args => CarFn(CdrFn(args[0])));
        _b("cdar", args => CdrFn(CarFn(args[0])));
        _b("cddr", args => CdrFn(CdrFn(args[0])));
        _b("caaar", args => CarFn(CarFn(CarFn(args[0]))));
        _b("caadr", args => CarFn(CarFn(CdrFn(args[0]))));
        _b("cadar", args => CarFn(CdrFn(CarFn(args[0]))));
        _b("caddr", args => CarFn(CdrFn(CdrFn(args[0]))));
        _b("cdaar", args => CdrFn(CarFn(CarFn(args[0]))));
        _b("cdadr", args => CdrFn(CarFn(CdrFn(args[0]))));
        _b("cddar", args => CdrFn(CdrFn(CarFn(args[0]))));
        _b("cdddr", args => CdrFn(CdrFn(CdrFn(args[0]))));
        _b("list", args => args.ToCell());
        _b("length", args => args[0].CellLength());
        _b("list-ref", args => args[0].AsCell()![NumericHelper.ToInt(args[1])]);
        _b("list-tail", args =>
        {
            var n = NumericHelper.ToInt(args[1]);
            object? cur = args[0];
            for (int i = 0; i < n; i++) cur = cur is Cell c ? c.Cdr : Const.NIL;
            return cur;
        });
        _b("append", args =>
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
        });
        _b("reverse", args =>
        {
            var items = new List<object?>();
            object? cur = args[0];
            while (cur is Cell c) { items.Add(c.Car); cur = c.Cdr; }
            return CellHelper.ToCell(items.AsEnumerable().Reverse());
        });
        _b("list?", args =>
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
            return fast is Nil ? Const.TRUE : Const.FALSE;
        });
        _b("make-list", args =>
        {
            var n = NumericHelper.ToInt(args[0]);
            var fill = args.Length > 1 ? args[1] : Const.NIL;
            return Enumerable.Repeat(fill, n).ToCell();
        });
        _b("list-copy", args =>
        {
            if (args[0] is Nil) return Const.NIL;
            var items = new List<object?>();
            object? cur = args[0];
            while (cur is Cell c) { items.Add(c.Car); cur = c.Cdr; }
            return items.ToCell();
        });
        _b("list-set!", args =>
        {
            var n = NumericHelper.ToInt(args[1]);
            object? cur = args[0];
            for (int i = 0; i < n; i++) cur = cur is Cell c ? c.Cdr : Const.NIL;
            if (cur is Cell target) target.Car = args[2];
            return Const.VOID;
        });
        _b("memq", args =>
        {
            object? cur = args[1];
            while (cur is Cell c) { if (ReferenceEquals(c.Car, args[0]) || c.Car?.Equals(args[0]) == true) return cur; cur = c.Cdr; }
            return Const.FALSE;
        });
        _b("memv", args =>
        {
            object? cur = args[1];
            while (cur is Cell c) { if (c.Car?.Equals(args[0]) == true) return cur; cur = c.Cdr; }
            return Const.FALSE;
        });
        _b("member", args =>
        {
            object? cur = args[1];
            while (cur is Cell c) { if (c.Car?.Equals(args[0]) == true) return cur; cur = c.Cdr; }
            return Const.FALSE;
        });
        _b("assq", args => Assoc(args[0], args[1], true));
        _b("assv", args => Assoc(args[0], args[1], false));
        _b("assoc", args => Assoc(args[0], args[1], false));

        // ── Arithmetic ──
        _b("+", args => args.Aggregate((object?)0L, (acc, x) => NumericHelper.Add(acc!, x))!);
        _b("-", args =>
        {
            if (args.Length == 1) return NumericHelper.Negate(args[0]);
            return args.Skip(1).Aggregate((object?)args[0], (acc, x) => NumericHelper.Sub(acc!, x))!;
        });
        _b("*", args => args.Aggregate((object?)1L, (acc, x) => NumericHelper.Mul(acc!, x))!);
        _b("/", args =>
        {
            if (args.Length == 1) return NumericHelper.Recip(args[0]);
            return args.Skip(1).Aggregate((object?)args[0], (acc, x) => NumericHelper.Div(acc!, x))!;
        });
        _b("quotient", args => NumericHelper.Quotient(args[0], args[1]));
        _b("remainder", args => NumericHelper.Remainder(args[0], args[1]));
        _b("modulo", args => NumericHelper.Modulo(args[0], args[1]));
        _b("expt", args =>
        {
            var a = args[0]; var b = args[1];
            var ta = NumericHelper.Classify(a); var tb = NumericHelper.Classify(b);
            if (ta <= NumericHelper.NumType.Int && tb == NumericHelper.NumType.Int && NumericHelper.ToLong(b) >= 0)
            {
                var base_ = NumericHelper.ToBigInt(a); var exp = NumericHelper.ToInt(b);
                var r = BigInteger.Pow(base_, exp);
                return r <= long.MaxValue && r >= long.MinValue ? (long)r : r;
            }
            return Math.Pow(NumericHelper.ToDouble(a), NumericHelper.ToDouble(b));
        });
        _b("sqrt", args =>
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
        });
        _b("abs", args =>
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
        });
        _b("floor", args =>
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
        });
        _b("ceiling", args =>
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
        });
        _b("truncate", args =>
        {
            var a = args[0];
            if (NumericHelper.Classify(a) <= NumericHelper.NumType.Int) return a;
            if (a is SchemeFraction f) return NumericHelper.ToLong(f.Num / f.Den);
            return Math.Truncate(NumericHelper.ToDouble(a));
        });
        _b("round", args =>
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
        });
        _b("exp", args => Math.Exp(NumericHelper.ToDouble(args[0])));
        _b("log", args => Math.Log(NumericHelper.ToDouble(args[0])));
        _b("sin", args => Math.Sin(NumericHelper.ToDouble(args[0])));
        _b("cos", args => Math.Cos(NumericHelper.ToDouble(args[0])));
        _b("tan", args => Math.Tan(NumericHelper.ToDouble(args[0])));
        _b("asin", args => Math.Asin(NumericHelper.ToDouble(args[0])));
        _b("acos", args => Math.Acos(NumericHelper.ToDouble(args[0])));
        _b("atan", args => Math.Atan(NumericHelper.ToDouble(args[0])));
        _b("number->string", args =>
        {
            var radix = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 10;
            if (radix == 10) return Printer.Format(args[0]);
            var n = NumericHelper.ToBigInt(args[0]);
            if (n < 0) return "-" + ToRadixString(-n, radix);
            return ToRadixString(n, radix);
        });
        _b("string->number", args =>
        {
            var s = ToStr(args[0]);
            var radix = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 10;
            string prefix = radix switch { 2 => "#b", 8 => "#o", 16 => "#x", _ => "" };
            var full = prefix + s;
            return Reader.AtomParser.ParseAtom(full) is Sym ? Const.FALSE : Reader.AtomParser.ParseAtom(full);
        });
        _b("even?", args =>
        {
            var x = NumericHelper.ToBigInt(args[0]);
            return x.IsEven ? Const.TRUE : Const.FALSE;
        });
        _b("odd?", args =>
        {
            var x = NumericHelper.ToBigInt(args[0]);
            return !x.IsEven ? Const.TRUE : Const.FALSE;
        });
        _b("zero?", args => NumericHelper.IsZero(args[0]) ? Const.TRUE : Const.FALSE);
        _b("positive?", args => NumericHelper.Compare(args[0], 0L) > 0 ? Const.TRUE : Const.FALSE);
        _b("negative?", args => NumericHelper.Compare(args[0], 0L) < 0 ? Const.TRUE : Const.FALSE);
        _b("exact?", args => args[0] is int or long or BigInteger or SchemeFraction ? Const.TRUE : Const.FALSE);
        _b("inexact?", args => args[0] is double or float or Complex ? Const.TRUE : Const.FALSE);
        _b("max", args =>
        {
            return args.Aggregate((object?)args[0], (best, x) =>
                NumericHelper.Compare(best!, x) >= 0 ? best : x)!;
        });
        _b("min", args =>
        {
            return args.Aggregate((object?)args[0], (best, x) =>
                NumericHelper.Compare(best!, x) <= 0 ? best : x)!;
        });
        _b("gcd", args =>
        {
            BigInteger Gcd(BigInteger a, BigInteger b) => b == 0 ? BigInteger.Abs(a) : Gcd(b, a % b);
            var r = args.Select(NumericHelper.ToBigInt).Aggregate(Gcd);
            return r <= long.MaxValue ? (long)r : r;
        });
        _b("lcm", args =>
        {
            BigInteger Gcd(BigInteger a, BigInteger b) => b == 0 ? BigInteger.Abs(a) : Gcd(b, a % b);
            var items = args.Select(NumericHelper.ToBigInt).ToList();
            if (items.Count == 0) return 1L;
            var r = items.Aggregate((a, b) => a / Gcd(a, b) * b);
            return r <= long.MaxValue ? (long)r : r;
        });

        // ── Comparisons ──
        _b("=", args =>
        {
            if (args.Length < 2) return Const.TRUE;
            var first = args[0];
            for (int i = 1; i < args.Length; i++)
                if (NumericHelper.Compare(first, args[i]) != 0) return Const.FALSE;
            return Const.TRUE;
        });
        _b("<", args =>
        {
            if (args.Length < 2) return Const.TRUE;
            for (int i = 1; i < args.Length; i++)
                if (NumericHelper.Compare(args[i - 1], args[i]) >= 0) return Const.FALSE;
            return Const.TRUE;
        });
        _b(">", args =>
        {
            if (args.Length < 2) return Const.TRUE;
            for (int i = 1; i < args.Length; i++)
                if (NumericHelper.Compare(args[i - 1], args[i]) <= 0) return Const.FALSE;
            return Const.TRUE;
        });
        _b("<=", args =>
        {
            if (args.Length < 2) return Const.TRUE;
            for (int i = 1; i < args.Length; i++)
                if (NumericHelper.Compare(args[i - 1], args[i]) > 0) return Const.FALSE;
            return Const.TRUE;
        });
        _b(">=", args =>
        {
            if (args.Length < 2) return Const.TRUE;
            for (int i = 1; i < args.Length; i++)
                if (NumericHelper.Compare(args[i - 1], args[i]) < 0) return Const.FALSE;
            return Const.TRUE;
        });

        // ── Boolean ──
        _b("condition?", args => args[0] is SchemeException or ErrorObject ? Const.TRUE : Const.FALSE);
        _b("condition-message", args => args[0] is ErrorObject eo ? eo.Message : args[0] is SchemeException se ? se.Val?.ToString() ?? "" : "");
        _b("digit-value", args =>
        {
            var c = ToChar(args[0]);
            if (c >= '0' && c <= '9') return (long)(c - '0');
            if (c >= 'a' && c <= 'f') return (long)(c - 'a' + 10);
            if (c >= 'A' && c <= 'F') return (long)(c - 'A' + 10);
            return Const.FALSE;
        });


        // ── String operations ──
        _b("make-string", args =>
        {
            var len = NumericHelper.ToInt(args[0]);
            var cp = args.Length > 1 ? AsChar(args[1]) : (int)' ';
            return new SchemeString(Enumerable.Repeat(cp, len));
        });
        _b("string", args => new SchemeString(args.Select(AsChar)));
        _b("string->list", args =>
        {
            var s = ToStr(args[0]);
            var cells = new List<object?>();
            foreach (var rune in s.EnumerateRunes())
                cells.Add(new SchemeChar(rune.Value));
            return cells.ToCell();
        });
        _b("list->string", args =>
        {
            var chars = new List<int>();
            object? cur = args[0];
            while (cur is Cell c) { chars.Add(AsChar(c.Car)); cur = c.Cdr; }
            return new SchemeString(chars);
        });
        _b("string-length", args =>
        {
            if (args[0] is SchemeString ss) return ss.Length;
            int count = 0;
            foreach (var _ in ToStr(args[0]).EnumerateRunes()) count++;
            return count;
        });
        _b("string-ref", args =>
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
        });
        _b("string-set!", args =>
        {
            if (args[0] is SchemeString s)
            {
                s.Data[NumericHelper.ToInt(args[1])] = args[2] is SchemeChar sc ? sc.Codepoint : AsChar(args[2]);
                return Const.VOID;
            }
            throw new Exception("string-set! requires mutable SchemeString");
        });
        _b("string-append", args => new SchemeString(string.Concat(args.Select(ToStr))));
        _b("string-copy", args =>
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
        });
        _b("string->symbol", args => Sym.Intern(ToStr(args[0])));
        _b("symbol->string", args =>
        {
            if (args[0] is Sym sym) return new SchemeString(sym.Name);
            return new SchemeString(args[0].AsString());
        });
        _b("string=?", args => ToStr(args[0]) == ToStr(args[1]) ? Const.TRUE : Const.FALSE);
        _b("string<?", args => string.Compare(ToStr(args[0]), ToStr(args[1])) < 0 ? Const.TRUE : Const.FALSE);
        _b("string>?", args => string.Compare(ToStr(args[0]), ToStr(args[1])) > 0 ? Const.TRUE : Const.FALSE);
        _b("string<=?", args => string.Compare(ToStr(args[0]), ToStr(args[1])) <= 0 ? Const.TRUE : Const.FALSE);
        _b("string>=?", args => string.Compare(ToStr(args[0]), ToStr(args[1])) >= 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci=?", args => string.Equals(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) ? Const.TRUE : Const.FALSE);
        _b("string-ci<?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) < 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci>?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) > 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci<=?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) <= 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci>=?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) >= 0 ? Const.TRUE : Const.FALSE);
        _b("string-downcase", args => new SchemeString(ToStr(args[0]).ToLowerInvariant()));
        _b("string-upcase", args => new SchemeString(ToStr(args[0]).ToUpperInvariant()));
        _b("string-fill!", args =>
        {
            var cp = AsChar(args[1]);
            if (args[0] is SchemeString s) { for (int i = 0; i < s.Data.Count; i++) s.Data[i] = cp; }
            return Const.VOID;
        });
        _b("substring", args =>
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
        });

        // ── Char operations ──
        _b("char=?", args =>
        {
            for (int i = 1; i < args.Length; i++)
                if (AsChar(args[i - 1]) != AsChar(args[i])) return Const.FALSE;
            return Const.TRUE;
        });
        _b("char<?", args =>
        {
            for (int i = 1; i < args.Length; i++)
                if (AsChar(args[i - 1]) >= AsChar(args[i])) return Const.FALSE;
            return Const.TRUE;
        });
        _b("char>?", args =>
        {
            for (int i = 1; i < args.Length; i++)
                if (AsChar(args[i - 1]) <= AsChar(args[i])) return Const.FALSE;
            return Const.TRUE;
        });
        _b("char<=?", args =>
        {
            for (int i = 1; i < args.Length; i++)
                if (AsChar(args[i - 1]) > AsChar(args[i])) return Const.FALSE;
            return Const.TRUE;
        });
        _b("char>=?", args =>
        {
            for (int i = 1; i < args.Length; i++)
                if (AsChar(args[i - 1]) < AsChar(args[i])) return Const.FALSE;
            return Const.TRUE;
        });
        _b("char-ci=?", args =>
        {
            for (int i = 1; i < args.Length; i++)
            {
                var r1 = new Rune(AsChar(args[i - 1]));
                var r2 = new Rune(AsChar(args[i]));
                if (Rune.ToLowerInvariant(r1) != Rune.ToLowerInvariant(r2)) return Const.FALSE;
            }
            return Const.TRUE;
        });
        _b("char-downcase", args => new SchemeChar(Rune.ToLowerInvariant(new Rune(AsChar(args[0]))).Value));
        _b("char-upcase", args => new SchemeChar(Rune.ToUpperInvariant(new Rune(AsChar(args[0]))).Value));
        _b("char->integer", args => (long)AsChar(args[0]));
        _b("integer->char", args => new SchemeChar((int)NumericHelper.ToLong(args[0])));
        _b("char-alphabetic?", args =>
        {
            try { return Rune.IsLetter(new Rune(AsChar(args[0]))) ? Const.TRUE : Const.FALSE; }
            catch { return Const.FALSE; }
        });
        _b("char-numeric?", args =>
        {
            try { return Rune.IsDigit(new Rune(AsChar(args[0]))) ? Const.TRUE : Const.FALSE; }
            catch { return Const.FALSE; }
        });
        _b("char-whitespace?", args =>
        {
            try { return Rune.IsWhiteSpace(new Rune(AsChar(args[0]))) ? Const.TRUE : Const.FALSE; }
            catch { return Const.FALSE; }
        });
        _b("char-lower-case?", args =>
        {
            try { return Rune.IsLower(new Rune(AsChar(args[0]))) ? Const.TRUE : Const.FALSE; }
            catch { return Const.FALSE; }
        });
        _b("char-upper-case?", args =>
        {
            try { return Rune.IsUpper(new Rune(AsChar(args[0]))) ? Const.TRUE : Const.FALSE; }
            catch { return Const.FALSE; }
        });

        // ── Vector operations ──
        _b("vector", args => new SchemeVector(args));
        _b("make-vector", args =>
        {
            var n = NumericHelper.ToInt(args[0]);
            var fill = args.Length > 1 ? args[1] : Const.NIL;
            return new SchemeVector(Enumerable.Repeat(fill, n));
        });
        _b("vector-length", args => AsVector(args[0]).Length);
        _b("vector-ref", args => AsVector(args[0])[NumericHelper.ToInt(args[1])]);
        _b("vector-set!", args => { AsVector(args[0])[NumericHelper.ToInt(args[1])] = args[2]; return Const.VOID; });
        _b("vector->list", args => AsVector(args[0]).Data.ToCell());
        _b("list->vector", args => new SchemeVector(args[0].Cells()));
        _b("vector-fill!", args => { var v = AsVector(args[0]); for (int i = 0; i < v.Length; i++) v[i] = args[1]; return Const.VOID; });
        _b("vector-copy", args => new SchemeVector(AsVector(args[0]).Data));
        _b("vector-append", args =>
        {
            var all = new List<object?>();
            foreach (var vec in args)
                if (vec is SchemeVector sv) all.AddRange(sv.Data);
            return new SchemeVector(all);
        });

        // ── Bytevector operations ──
        _b("bytevector", args => new SchemeBytevector(args.Select(NumericHelper.ToInt)));
        _b("make-bytevector", args =>
        {
            var n = NumericHelper.ToInt(args[0]);
            var fill = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
            var arr = new byte[n];
            for (int i = 0; i < n; i++) arr[i] = (byte)fill;
            return new SchemeBytevector(arr);
        });
        _b("bytevector-length", args => AsBytevector(args[0]).Length);
        _b("bytevector-u8-ref", args => (long)AsBytevector(args[0])[NumericHelper.ToInt(args[1])]);
        _b("bytevector-u8-set!", args => { AsBytevector(args[0])[NumericHelper.ToInt(args[1])] = (byte)NumericHelper.ToInt(args[2]); return Const.VOID; });
        _b("bytevector->u8-list", args => AsBytevector(args[0]).Data.Select(b => (object?)(long)b).ToCell());
        _b("u8-list->bytevector", args => new SchemeBytevector(args[0].Cells().Select(NumericHelper.ToInt)));
        _b("bytevector-copy", args => new SchemeBytevector([.. AsBytevector(args[0]).Data]));

        // ── List high-order ──
        _b("map", args =>
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
        });
        _b("for-each", args =>
        {
            var fn = args[0];
            object? cur = args[1];
            while (cur is Cell c) { App(fn, c.Car); cur = c.Cdr; }
            return Const.VOID;
        });
        _b("filter", args =>
        {
            var pred = args[0];
            var results = new List<object?>();
            object? cur = args[1];
            while (cur is Cell c) { if (App(pred, c.Car) is Sym s && s != Const.FALSE) results.Add(c.Car); cur = c.Cdr; }
            return results.ToCell();
        });
        _b("fold", args =>
        {
            var fn = args[0];
            var acc = args[1];
            object? cur = args[2];
            while (cur is Cell c) { acc = App(fn, c.Car, acc); cur = c.Cdr; }
            return acc;
        });
        _b("fold-right", args =>
        {
            var fn = args[0];
            var items = new List<object?>();
            object? cur = args[2];
            while (cur is Cell c) { items.Add(c.Car); cur = c.Cdr; }
            var acc = args[1];
            for (int i = items.Count - 1; i >= 0; i--)
                acc = App(fn, items[i], acc);
            return acc;
        });
        _b("find", args =>
        {
            var pred = args[0];
            object? cur = args[1];
            while (cur is Cell c) { if (App(pred, c.Car) is Sym s && s != Const.FALSE) return c.Car; cur = c.Cdr; }
            return Const.FALSE;
        });
        _b("any", args =>
        {
            var pred = args[0];
            object? cur = args[1];
            while (cur is Cell c) { var r = App(pred, c.Car); if (r is Sym s && s != Const.FALSE) return r; cur = c.Cdr; }
            return Const.FALSE;
        });
        _b("every", args =>
        {
            var pred = args[0];
            object? cur = args[1];
            while (cur is Cell c) { var r = App(pred, c.Car); if (r is Sym s && s == Const.FALSE) return Const.FALSE; cur = c.Cdr; }
            return Const.TRUE;
        });
        _b("partition", args =>
        {
            var pred = args[0];
            var pass = new List<object?>();
            var fail = new List<object?>();
            object? cur = args[1];
            while (cur is Cell c) { if (App(pred, c.Car) is Sym s && s != Const.FALSE) pass.Add(c.Car); else fail.Add(c.Car); cur = c.Cdr; }
            return new SchemeVector([pass.ToCell(), fail.ToCell()]);
        });
        _b("take", args =>
        {
            var result = new List<object?>();
            object? cur = args[0]; int i = 0; int n = NumericHelper.ToInt(args[1]);
            while (cur is Cell c && i < n) { result.Add(c.Car); cur = c.Cdr; i++; }
            return result.ToCell();
        });
        _b("drop", args =>
        {
            object? cur = args[0]; int i = 0; int n = NumericHelper.ToInt(args[1]);
            while (cur is Cell c && i < n) { cur = c.Cdr; i++; }
            return cur;
        });
        _b("take-while", args =>
        {
            var pred = args[0];
            var result = new List<object?>();
            object? cur = args[1];
            while (cur is Cell c && App(pred, c.Car) is Sym s && s != Const.FALSE)
            { result.Add(c.Car); cur = c.Cdr; }
            return result.ToCell();
        });
        _b("drop-while", args =>
        {
            var pred = args[0];
            object? cur = args[1];
            while (cur is Cell c && App(pred, c.Car) is Sym s && s != Const.FALSE) cur = c.Cdr;
            return cur;
        });
        _b("span", args =>
        {
            var pred = args[0];
            var pass = new List<object?>();
            object? cur = args[1];
            while (cur is Cell c && App(pred, c.Car) is Sym s && s != Const.FALSE)
            { pass.Add(c.Car); cur = c.Cdr; }
            return new SchemeVector([pass.ToCell(), cur]);
        });
        _b("break", args =>
        {
            var pred = args[0];
            var before = new List<object?>();
            object? cur = args[1];
            while (cur is Cell c && App(pred, c.Car) is Sym s && s == Const.FALSE)
            { before.Add(c.Car); cur = c.Cdr; }
            return new SchemeVector([before.ToCell(), cur]);
        });
        _b("iota", args =>
        {
            var n = NumericHelper.ToInt(args[0]);
            var start = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
            return Enumerable.Range((int)start, n).Select(i => (object?)(long)i).ToCell();
        });

        // ── Arithmetic (numeric) ──
        _b("1+", args => NumericHelper.ToLong(args[0]) + 1);
        _b("-1+", args => NumericHelper.ToLong(args[0]) - 1);

        // ── Port / I/O ──
        _b("display", args =>
        {
            var obj = args[0];
            object? port = null;
            if (args.Length > 1 && args[1] is ITuple t && t.Length >= 3 && t[0] is string s0 && s0 == "port" && (t[1] is "output" || t[1] is "input"))
                port = t[2];
            if (port is StreamWriter sw) { sw.Write(Printer.ToDisplayString(obj)); sw.Flush(); }
            else if (port is StringBuilder sb) { sb.Append(Printer.ToDisplayString(obj)); }
            else Console.Write(Printer.ToDisplayString(obj));
            return Const.VOID;
        });
        _b("newline", args => { Console.WriteLine(); return Const.VOID; });
        _b("write-char", args =>
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
        });
        _b("write", args =>
        {
            var obj = args[0];
            object? port = null;
            if (args.Length > 1 && args[1] is ITuple t && t.Length >= 3 && t[0] is string s0 && s0 == "port" && (t[1] is "output" || t[1] is "input"))
                port = t[2];
            if (port is StreamWriter sw) { sw.Write(Printer.Format(obj)); sw.Flush(); }
            else if (port is StringBuilder sb) { sb.Append(Printer.Format(obj)); }
            else Console.Write(Printer.Format(obj));
            return Const.VOID;
        });
        _b("read", args =>
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
        });
        _b("read-line", args =>
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
        });
        _b("read-string", args =>
        {
            var n = NumericHelper.ToInt(args[0]);
            if (args.Length > 1 && args[1] is System.Runtime.CompilerServices.ITuple port && port.Length >= 3 && port[0] is "port" && port[1] is "input")
            {
                if (port[2] is StreamReader sr) { var buf = new char[n]; var read = sr.ReadBlock(buf, 0, n); return read > 0 ? new string(buf, 0, read) : Const.EOF; }
                if (port[2] is StringBuilder sb) { var s = sb.ToString(); var take = Math.Min(n, s.Length); if (take == 0) return Const.EOF; var r = s[..take]; sb.Remove(0, take); return r; }
                if (port[2] is StringPort sp) { var take = Math.Min(n, sp.Data.Length - sp.Pos); if (take <= 0) return Const.EOF; var r = sp.Data.Substring(sp.Pos, take); sp.Pos += take; return r; }
            }
            var buf2 = new char[n];
            var read2 = Console.In.ReadBlock(buf2, 0, n);
            return read2 > 0 ? new string(buf2, 0, read2) : Const.EOF;
        });
        _b("peek-char", args =>
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
        });
        _b("read-char", args =>
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
        });
        _b("close-input-port", args => Const.VOID);
        _b("close-output-port", args => Const.VOID);
        _b("port-position", args =>
        {
            if (args[0] is ITuple t && t.Length >= 3 && t[0] is "port" && t[1] is "input" && t[2] is StringPort sp)
                return (long)sp.Pos;
            return Const.FALSE;
        });
        _b("set-port-position!", args =>
        {
            if (args[0] is ITuple t && t.Length >= 3 && t[0] is "port" && t[1] is "input" && t[2] is StringPort sp)
            {
                sp.SetPos(NumericHelper.ToInt(args[1]));
                return Const.VOID;
            }
            return Const.FALSE;
        });
        _b("open-input-string", args => MakePort("input", new StringPort(ToStr(args[0]))));
        _b("open-output-string", args => MakePort("output", new StringBuilder()));
        _b("get-output-string", args =>
        {
            var p = args[0];
            if (p is ITuple it && it.Length >= 3 && it[0] is "port" && it[1] is "output" && it[2] is StringBuilder sb)
                return sb.ToString();
            return "";
        });
        _b("call-with-input-file", args =>
        {
            var path = ToStr(args[0]);
            var proc = args[1];
            using var sr = new StreamReader(path);
            var port = MakePort("input", sr);
            return App(proc, port);
        });
        _b("call-with-output-file", args =>
        {
            var path = ToStr(args[0]);
            var proc = args[1];
            using var sw = new StreamWriter(path);
            var port = MakePort("output", sw);
            return App(proc, port);
        });
        _b("with-input-from-file", args =>
        {
            var path = ToStr(args[0]);
            var thunk = args[1];
            using var sr = new StreamReader(path);
            var oldIn = Console.In;
            Console.SetIn(sr);
            try { return App(thunk); }
            finally { Console.SetIn(oldIn); }
        });
        _b("with-output-to-file", args =>
        {
            var path = ToStr(args[0]);
            var thunk = args[1];
            using var sw = new StreamWriter(path);
            var oldOut = Console.Out;
            Console.SetOut(sw);
            try { return App(thunk); }
            finally { Console.SetOut(oldOut); }
        });
        _b("current-input-port", args => MakePort("input", Console.In));
        _b("current-output-port", args =>
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
                }
                return MakePort("output", old);
            }
            return MakePort("output", Console.Out);
        });
        _b("current-error-port", args => MakePort("output", Console.Error));

        // ── Error / Exception ──
        _b("error", args =>
        {
            var irrList = args.Skip(1).ToList();
            throw new SchemeException(new ErrorObject(args[0], irrList.ToCell()));
        });
        _b("raise", args =>
        {
            var obj = args[0];
            if (obj is SchemeException se) throw se;
            throw new SchemeException(obj);
        });

        // ── Box operations ──
        _b("box", args => (ValueTuple<string, object?>)("box", args[0]));
        _b("unbox", args => args[0] is ValueTuple<string, object?> t && t.Item1 == "box" ? t.Item2! : throw new Exception("not a box"));
        _b("set-box!", args =>
        {
            var b = args[0]; var x = args[1];
            if (b is ValueTuple<string, object?> t && t.Item1 == "box")
            {
                var field = b.GetType().GetFields()[0];
                field.SetValue(b, ("box", x));
            }
            return Const.VOID;
        });

        // ── Misc ──
        _b("values", args => args.Length == 1 ? args[0] : new SchemeVector(args));
        _b("call-with-values", args =>
        {
            var producer = args[0];
            var consumer = args[1];
            var vals = App(producer);
            if (vals is SchemeVector sv) return App(consumer, [.. sv.Data]);
            if (vals is Cell c && c.Cdr is not Cell && c.Cdr is not Nil) return App(consumer, [c.Car, c.Cdr]);
            return App(consumer, vals);
        });
        _b("apply", args =>
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
        });
        _b("force", args =>
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
        });
        _b("call/cc", args =>
        {
            var receiver = args[0];
            object? result = null;
            var myId = ++ContCounter.Value;
            object? captured = null;
            try { result = App(receiver, new Func<object?[], object?>(_ => { captured = _[0]; throw new ContinuationEscape(captured, myId); })); }
            catch (ContinuationEscape ce) { if (ce.Id != myId) throw; result = ce.Val; }
            return result;
        });
        _b("call-with-current-continuation", args =>
        {
            var receiver = args[0];
            object? result = null;
            var myId = ++ContCounter.Value;
            object? captured = null;
            try { result = App(receiver, new Func<object?[], object?>(_ => { captured = _[0]; throw new ContinuationEscape(captured, myId); })); }
            catch (ContinuationEscape ce) { if (ce.Id != myId) throw; result = ce.Val; }
            return result;
        });
        _b("exit", args => Const.VOID);
        _b("load", args =>
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
        });
        _b("eval", args => Evaluator.Eval(args[0],
            args.Length > 1 && args[1] is Env e ? e : Evaluator.GlobalEnv));
        _b("interaction-environment", args => Evaluator.GlobalEnv);
        _b("scheme-report-environment", args => Evaluator.GlobalEnv);
        _b("null-environment", args => Evaluator.GlobalEnv);
        _b("environment", args => Evaluator.GlobalEnv);

        // ── Hash table ──
        _b("make-hash-table", args => new Dictionary<object, object?>());
        _b("hash-table-ref", args =>
        {
            var ht = (Dictionary<object, object?>)args[0]!;
            var key = args[1] ?? throw new Exception("hash-table-ref: null key");
            return ht.TryGetValue(key, out var v) ? v : throw new Exception("key not found");
        });
        _b("hash-table-set!", args =>
        {
            var ht = (Dictionary<object, object?>)args[0]!;
            ht[args[1] ?? throw new Exception("hash-table-set!: null key")] = args[2];
            return Const.VOID;
        });
        _b("hash-table-delete!", args =>
        {
            var ht = (Dictionary<object, object?>)args[0]!;
            ht.Remove(args[1] ?? throw new Exception("hash-table-delete!: null key"));
            return Const.VOID;
        });
        _b("hash-table-contains?", args =>
        {
            var ht = (Dictionary<object, object?>)args[0]!;
            return ht.ContainsKey(args[1] ?? throw new Exception("hash-table-contains?: null key")) ? Const.TRUE : Const.FALSE;
        });
        _b("hash-table-count", args =>
        {
            var ht = (Dictionary<object, object?>)args[0]!;
            return (long)ht.Count;
        });
        _b("hash-table-clear!", args =>
        {
            var ht = (Dictionary<object, object?>)args[0]!;
            ht.Clear();
            return Const.VOID;
        });

        // ── Time ──
        _b("current-second", args => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _b("current-jiffy", args => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _b("jiffies-per-second", args => 1000L);

        // ── Identity ──
        _b("identity", args => args[0]);
        _b("constantly", args =>
        {
            var x = args[0];
            return (Func<object?[], object?>)(_ => x);
        });
        _b("complement", args =>
        {
            var pred = args[0];
            return (Func<object?[], object?>)(x => App(pred, x[0]) is Sym s && s != Const.FALSE ? Const.FALSE : Const.TRUE);
        });
        _b("flip", args =>
        {
            var fn = args[0];
            return (Func<object?[], object?>)(a => App(fn, a[1], a[0]));
        });

        // ── Critical missing primitives ──
        _b("void", args => Const.VOID);
        _b("defined?", args =>
        {
            var name = (args[0] as Sym)?.Name ?? args[0]?.ToString() ?? "";
            return Evaluator.GlobalEnv.LookupSilent(name, null) is not null ? Const.TRUE : Const.FALSE;
        });
        _b("inexact->exact", args =>
        {
            var x = args[0];
            if (x is double d)
            {
                if (double.IsNaN(d) || double.IsInfinity(d))
                    throw new SchemeException("inexact->exact: not a finite number");
                return (long)d;
            }
            return x;
        });
        _b("make-promise", args => new Promise(() => args.Length > 0 ? args[0] : Const.VOID));
        _b("compose", args =>
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
        });

        // ── Bitwise operations ──
        _b("bit-and", args => args.Aggregate(-1L, (a, b) => a & NumericHelper.ToLong(b)));
        _b("bit-ior", args => args.Aggregate(0L, (a, b) => a | NumericHelper.ToLong(b)));
        _b("bit-xor", args => args.Aggregate(0L, (a, b) => a ^ NumericHelper.ToLong(b)));
        _b("bit-not", args => ~NumericHelper.ToLong(args[0]));
        _b("bit-or", args => args.Aggregate(0L, (a, b) => a | NumericHelper.ToLong(b)));
        _b("arithmetic-shift", args =>
        {
            var a = NumericHelper.ToLong(args[0]);
            var b = NumericHelper.ToInt(args[1]);
            return b >= 0 ? a << b : a >> (-b);
        });
        _b("logbit?", args => (NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]) & 1) != 0 ? Const.TRUE : Const.FALSE);
        _b("logtest", args => (NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[1])) != 0 ? Const.TRUE : Const.FALSE);
        _b("bit-set?", args => (NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]) & 1) != 0 ? Const.TRUE : Const.FALSE);
        _b("bitwise-not", args => ~NumericHelper.ToLong(args[0]));
        _b("bitwise-and", args => args.Aggregate(-1L, (a, b) => a & NumericHelper.ToLong(b)));
        _b("bitwise-ior", args => args.Aggregate(0L, (a, b) => a | NumericHelper.ToLong(b)));
        _b("bitwise-xor", args => args.Aggregate(0L, (a, b) => a ^ NumericHelper.ToLong(b)));
        _b("bitwise-if", args =>
        {
            var mask = NumericHelper.ToLong(args[0]);
            var t = NumericHelper.ToLong(args[1]);
            var e = NumericHelper.ToLong(args[2]);
            return (mask & t) | (~mask & e);
        });
        _b("bitwise-length", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            if (n == 0) return 0L;
            return (long)(Math.Floor(Math.Log(n < 0 ? -n : n, 2)) + 1);
        });
        _b("bitwise-count", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            n = n < 0 ? -n - 1 : n;
            long count = 0;
            while (n != 0) { count += n & 1; n >>= 1; }
            return count;
        });
        _b("bit-count", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            n = n < 0 ? -n - 1 : n;
            long count = 0;
            while (n != 0) { count += n & 1; n >>= 1; }
            return count;
        });
        _b("integer-length", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            if (n == 0) return 0L;
            return (long)(Math.Floor(Math.Log(n < 0 ? -n : n, 2)) + 1);
        });
        _b("first-set-bit", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            if (n == 0) return -1L;
            long i = 0;
            while ((n & 1) == 0) { n >>= 1; i++; }
            return i;
        });
        _b("bitwise-any-bit-set?", args => (NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[1])) != 0 ? Const.TRUE : Const.FALSE);
        _b("bitwise-shift", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            var cnt = NumericHelper.ToInt(args[1]);
            return cnt >= 0 ? n << cnt : n >> (-cnt);
        });
        _b("bit-shift", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            var cnt = NumericHelper.ToInt(args[1]);
            return cnt >= 0 ? n << cnt : n >> (-cnt);
        });
        _b("bitwise-arithmetic-shift", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            var cnt = NumericHelper.ToInt(args[1]);
            return cnt >= 0 ? n << cnt : n >> (-cnt);
        });
        _b("bitwise-arithmetic-shift-right", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            return n >> NumericHelper.ToInt(args[1]);
        });
        _b("bitwise-reverse-bit-field", args =>
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
        });
        _b("bitwise-rotate", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            var cnt = NumericHelper.ToInt(args[1]);
            var len = NumericHelper.ToInt(args[2]);
            if (len == 0) return n;
            cnt %= len; if (cnt < 0) cnt += len;
            var mask = (1L << len) - 1;
            n &= mask;
            return ((n << cnt) | (n >> (len - cnt))) & mask;
        });
        _b("bitwise-rotate-bit-field", args =>
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
        });
        _b("bitwise-copy-bit", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            var i = NumericHelper.ToInt(args[1]);
            var v = NumericHelper.ToLong(args[2]);
            return v != 0 ? (n | (1L << i)) : (n & ~(1L << i));
        });
        _b("copy-bit", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            var i = NumericHelper.ToInt(args[1]);
            var v = NumericHelper.ToLong(args[2]);
            return v != 0 ? (n | (1L << i)) : (n & ~(1L << i));
        });
        _b("bitwise-copy-bit-field", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            var start = NumericHelper.ToInt(args[1]);
            var end = NumericHelper.ToInt(args[2]);
            var newVal = NumericHelper.ToLong(args[3]);
            var len = end - start;
            if (len <= 0) return n;
            var mask = ((1L << len) - 1) << start;
            return (n & ~mask) | ((newVal << start) & mask);
        });
        _b("bitwise-bit-field", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            var start = NumericHelper.ToInt(args[1]);
            var end = NumericHelper.ToInt(args[2]);
            var len = end - start;
            if (len <= 0) return 0L;
            return (n >> start) & ((1L << len) - 1);
        });
        _b("bit-field", args =>
        {
            var n = NumericHelper.ToLong(args[0]);
            var start = NumericHelper.ToInt(args[1]);
            var end = NumericHelper.ToInt(args[2]);
            var len = end - start;
            if (len <= 0) return 0L;
            return (n >> start) & ((1L << len) - 1);
        });

        // ── Numeric utilities ──
        _b("numerator", args =>
        {
            if (args[0] is SchemeFraction f)
            {
                var n = f.Num;
                return n <= long.MaxValue && n >= long.MinValue ? (long)n : n;
            }
            return args[0] is int or long or BigInteger ? args[0] : NumericHelper.ToLong(args[0]);
        });
        _b("denominator", args =>
        {
            if (args[0] is SchemeFraction f)
            {
                var d = f.Den;
                return d <= long.MaxValue && d >= long.MinValue ? (long)d : d;
            }
            return 1L;
        });
        _b("rationalize", args =>
        {
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
        });
        _b("exact-integer-sqrt", args =>
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
        });

        // ── Complex operations ──
        _b("angle", args =>
        {
            var z = args[0];
            if (z is Complex c) return Math.Atan2(c.Imaginary, c.Real);
            return NumericHelper.Compare(z, 0L) >= 0 ? 0.0 : Math.PI;
        });
        _b("real-part", args =>
        {
            var z = args[0];
            if (z is Complex c) return c.Real;
            if (NumericHelper.Classify(z) <= NumericHelper.NumType.Int) return z;
            if (z is double d) return d;
            if (z is SchemeFraction f) return f.ToDouble();
            return z;
        });
        _b("imag-part", args =>
        {
            var z = args[0];
            if (z is Complex c) return c.Imaginary;
            return 0.0;
        });
        _b("make-rectangular", args => new Complex(Convert.ToDouble(args[0]), Convert.ToDouble(args[1])));
        _b("make-polar", args =>
        {
            var r = Convert.ToDouble(args[0]);
            var theta = Convert.ToDouble(args[1]);
            return new Complex(r * Math.Cos(theta), r * Math.Sin(theta));
        });
        _b("magnitude", args =>
        {
            var z = args[0];
            if (z is Complex c) return c.Magnitude;
            if (z is long l) return Math.Abs(l);
            if (z is int i) return Math.Abs((long)i);
            if (z is BigInteger bi) return bi < 0 ? -bi : bi;
            if (z is SchemeFraction f) return Math.Abs(f.ToDouble());
            return Math.Abs(NumericHelper.ToDouble(z));
        });

        // ── All 24 cxr combinations (caaaar through cddddr) ──
        var _cxrMap = new Dictionary<string, Func<object?, object?>[]>
        {
            ["caaar"] = [CarFn, CarFn, CarFn], ["caadr"] = [CarFn, CarFn, CdrFn],
            ["cadar"] = [CarFn, CdrFn, CarFn], ["caddr"] = [CarFn, CdrFn, CdrFn],
            ["cdaar"] = [CdrFn, CarFn, CarFn], ["cdadr"] = [CdrFn, CarFn, CdrFn],
            ["cddar"] = [CdrFn, CdrFn, CarFn], ["cdddr"] = [CdrFn, CdrFn, CdrFn],
            ["caaaar"] = [CarFn, CarFn, CarFn, CarFn], ["caaadr"] = [CarFn, CarFn, CarFn, CdrFn],
            ["caadar"] = [CarFn, CarFn, CdrFn, CarFn], ["caaddr"] = [CarFn, CarFn, CdrFn, CdrFn],
            ["cadaar"] = [CarFn, CdrFn, CarFn, CarFn], ["cadadr"] = [CarFn, CdrFn, CarFn, CdrFn],
            ["caddar"] = [CarFn, CdrFn, CdrFn, CarFn], ["cadddr"] = [CarFn, CdrFn, CdrFn, CdrFn],
            ["cdaaar"] = [CdrFn, CarFn, CarFn, CarFn], ["cdaadr"] = [CdrFn, CarFn, CarFn, CdrFn],
            ["cdadar"] = [CdrFn, CarFn, CdrFn, CarFn], ["cdaddr"] = [CdrFn, CarFn, CdrFn, CdrFn],
            ["cddaar"] = [CdrFn, CdrFn, CarFn, CarFn], ["cddadr"] = [CdrFn, CdrFn, CarFn, CdrFn],
            ["cdddar"] = [CdrFn, CdrFn, CdrFn, CarFn], ["cddddr"] = [CdrFn, CdrFn, CdrFn, CdrFn],
        };
        foreach (var (name, chain) in _cxrMap)
        {
            _b(name, args =>
            {
                object? x = args[0];
                for (int i = chain.Length - 1; i >= 0; i--) x = chain[i](x);
                return x;
            });
        }

        // ── Stream operations ──
        _b("jiffies-per-second", args => (long)1000000);
        _b("stream-car", args =>
        {
            var s = args[0];
            return s is Cell c ? c.Car : s;
        });
        _b("stream-cdr", args =>
        {
            var s = args[0];
            if (s is Cell c && c.Cdr is Promise p)
                return Evaluator.Eval(new Cell(Sym.Intern("force"), new Cell(c.Cdr, Const.NIL)), Evaluator.GlobalEnv);
            return s is Cell c2 ? c2.Cdr : s;
        });
        _b("stream-null?", args => args[0] is Nil ? Const.TRUE : Const.FALSE);
        _b("stream-ref", args =>
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
        });

        _b("stream-map", args =>
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
        });
        _b("stream-filter", args =>
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
        });
        _b("stream-take", args =>
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
        });

        // ── Errors / Exceptions ──
        _b("dynamic-wind", args =>
        {
            var before = args[0];
            var thunk = args[1];
            var after = args[2];
            App(before);
            try { return App(thunk); }
            finally { App(after); }
        });
        _b("with-exception-handler", args =>
        {
            var handler = args[0];
            var thunk = args[1];
            try { return App(thunk); }
            catch (SchemeException se) { return App(handler, se.Val); }
            catch (Exception ex) { return App(handler, ex.Message); }
        });
        _b("raise-continuable", args =>
        {
            throw new SchemeException(args[0]);
        });

        // ── Misc ──
        _b("exact->inexact", args =>
        {
            var x = args[0];
            if (x is int i) return (double)i;
            if (x is long l) return (double)l;
            if (x is BigInteger bi) return (double)bi;
            if (x is SchemeFraction f) return f.ToDouble();
            if (x is double d) return d;
            if (x is Complex c) return c;
            return Convert.ToDouble(x!);
        });
        _b("inexact->exact", args =>
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
        });
        _b("sum", args => args.Select(Convert.ToInt64).Sum());
        _b("error-object?", args => args[0] is ErrorObject ? Const.TRUE : Const.FALSE);
        _b("error-object-message", args => args[0] is ErrorObject eo ? eo.Message : Const.FALSE);
        _b("error-object-irritants", args => args[0] is ErrorObject eo ? eo.Irritants : Const.NIL);
        _b("string-contains?", args =>
        {
            var s = ToStr(args[0]);
            var substr = ToStr(args[1]);
            return s.Contains(substr) ? Const.TRUE : Const.FALSE;
        });
    }

    // ── Internal helpers ──

    private static object? App(object? proc, params object?[] args)
    {
        if (proc is Func<object?[], object?> fn) return fn(args);
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

    private static object? Eql(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return Const.TRUE;
        if (a is null || b is null) return Const.FALSE;
        if (a is Cell ca && b is Cell cb)
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

    private static object? CarFn(object? p) => p is Cell c ? c.Car : throw new Exception("pair required");
    private static object? CdrFn(object? p) => p is Cell c ? c.Cdr : throw new Exception("pair required");

    private static string ToStr(object? x) => x switch
    {
        string s => s,
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
