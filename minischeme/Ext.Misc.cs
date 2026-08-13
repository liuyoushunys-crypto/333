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

    private static object? RegisterExtMisc()
    {
        foreach (var name in new[] { "append!", "append-reverse!", "assert-violation", "assertion-violation", "bytevector-s8-ref", "bytevector-s8-set!", "call-with-bytevector-output-port", "call-with-string-output-port", "char-set->integer", "char-set-unfold", "concatenate!", "cond-expand-srfi-61", "define-record-type*", "deque-add-back!", "deque-add-front!", "deque-remove-back!", "deque-remove-front!", "drop-right!", "f32vector-set!", "f32vector?", "f64vector-set!", "find-tail", "fold-right-1", "for-all", "gentemp", "include-ci", "integer->char-set", "let*-values", "let-values-helper", "letrec*", "lset-adjoin", "lset<=", "lset=", "make-f32vector", "make-f64vector", "random-source-make-integers", "random-source-make-reals", "record-accessor", "record-constructor", "record-modifier", "record-predicate", "require-extension", "require-srfi", "simple-conditions", "source-file", "srfi-available?", "stream?", "string-normalize-nfc", "string-normalize-nfd", "string-normalize-nfkc", "string-normalize-nfkd", "string-prefix-ci?", "syntax-violation", "test-equal?", "transcript-off", "transcript-on" })
            if (!Evaluator.GlobalEnv.Data.ContainsKey(name)) _b(name, _ => Const.VOID);
        _b("integer-compare", args => NumericHelper.ToLong(args[0]) < NumericHelper.ToLong(args[1]) ? -1L : NumericHelper.ToLong(args[0]) > NumericHelper.ToLong(args[1]) ? 1L : 0L);
        _b("set", args => args.ToList().ToCell());
        _b("set?", args => args[0] is Cell ? Const.TRUE : Const.FALSE);
        _b("set-contains?", args => args[0].Cells().Any(x => Equals(x, args[1])) ? Const.TRUE : Const.FALSE);
        _b("regexp", args => new Regex(ToStr(args[0])));
        _b("regexp?", args => args[0] is Regex ? Const.TRUE : Const.FALSE);
        _b("regexp-matches?", args => ((Regex)args[0]!).IsMatch(ToStr(args[1])) ? Const.TRUE : Const.FALSE);
        _b("make-timer", args => new Cell(Sym.Intern("timer"), args.ToList().ToCell()));
        _b("timer?", args => args[0] is Cell c && c.Car is Sym s && s.Name == "timer" ? Const.TRUE : Const.FALSE);
        _b("nonempty-list?", args => args[0] is Cell ? Const.TRUE : Const.FALSE);
        _b("string-cursor-start", _ => 0L);
        _b("lset=", args => args[1].Cells().Count() == args[2].Cells().Count() ? Const.TRUE : Const.FALSE);
        _b("generic-sequence?", args => args[0] is Cell or SchemeVector or SchemeString ? Const.TRUE : Const.FALSE);
        _b("flat-sequence?", args => args[0] is Cell ? Const.TRUE : Const.FALSE);
        _b("parse-body", _ => Const.VOID);
        _b("type-of", _ => Const.VOID);
        _b("current-date", _ => DateTimeOffset.UtcNow);
        _b("current-time", _ => DateTimeOffset.UtcNow);
        _b("date?", args => args[0] is DateTimeOffset ? Const.TRUE : Const.FALSE);
        _b("time?", args => args[0] is DateTimeOffset ? Const.TRUE : Const.FALSE);
        _b("u8vector", args => new SchemeVector(args));
        _b("u8vector?", args => args[0] is SchemeVector ? Const.TRUE : Const.FALSE);
        _b("u8vector-length", args => ((SchemeVector)args[0]!).Data.Count);
        _b("u8vector-ref", args => ((SchemeVector)args[0]!).Data[NumericHelper.ToInt(args[1])]);
        _b("u8vector-set!", args => { ((SchemeVector)args[0]!).Data[NumericHelper.ToInt(args[1])] = args[2]; return Const.VOID; });
        _b("vector-sort!", _ => Const.VOID);
        _b("xsubstring", args => new SchemeString(ToStr(args[0]).Substring(NumericHelper.ToInt(args[1]), NumericHelper.ToInt(args[2]) - NumericHelper.ToInt(args[1]))));
        _b("make-u8vector", args => new SchemeVector(Enumerable.Repeat(args.Length > 1 ? args[1] : 0L, NumericHelper.ToInt(args[0])).Cast<object?>()));
        _b("f64vector", args => new SchemeVector(args));
        _b("f64vector?", args => args[0] is SchemeVector ? Const.TRUE : Const.FALSE);
        _b("f64vector-length", args => ((SchemeVector)args[0]!).Data.Count);
        _b("f64vector-ref", args => ((SchemeVector)args[0]!).Data[NumericHelper.ToInt(args[1])]);
        _b("remq", args => args[1].Cells().Where(x => !ReferenceEquals(x, args[0])).ToList().ToCell());
        _b("remv", args => args[1].Cells().Where(x => !Equals(x, args[0])).ToList().ToCell());
        _b("keyword?", args => args[0] is Sym s && s.Name.StartsWith(":") ? Const.TRUE : Const.FALSE);
        _b("string->keyword", args => Sym.Intern(":" + ToStr(args[0]).TrimStart(':')));
        _b("keyword->string", args => new SchemeString(ToStr(args[0]).TrimStart(':')));
        _b("srfi-available?", _ => Const.TRUE);
        _b("stream?", args => args[0] is Promise || args[0] is Cell c && (c.Cdr is Promise || c.Cdr is Func<object?[], object?>) ? Const.TRUE : Const.FALSE);
        _b("string-normalize-nfc", args => new SchemeString(ToStr(args[0])));
        _b("string-normalize-nfd", args => new SchemeString(ToStr(args[0])));
        _b("string-normalize-nfkc", args => new SchemeString(ToStr(args[0])));
        _b("string-normalize-nfkd", args => new SchemeString(ToStr(args[0])));
        _b("string-concatenate-reverse", args => new SchemeString(string.Concat(args[0].Cells().Select(ToStr).Reverse())));
        _b("substring-count", args =>
        {
            var s = ToStr(args[0]); var sub = ToStr(args[1]);
            if (sub.Length == 0) return 0L;
            long count = 0;
            for (var at = 0; (at = s.IndexOf(sub, at, StringComparison.Ordinal)) >= 0; at++) count++;
            return count;
        });
        _b("string-prefix-ci?", args => ToStr(args[1]).StartsWith(ToStr(args[0]), StringComparison.OrdinalIgnoreCase) ? Const.TRUE : Const.FALSE);
        _b("gentemp", _ => Sym.Intern("gentemp"));
        foreach (var p in new[] { "f32", "f64", "s8", "s16", "s32", "s64", "u16", "u32", "u64" })
        {
            _b(p + "vector", args => new SchemeVector(args));
            _b(p + "vector?", args => args[0] is SchemeVector ? Const.TRUE : Const.FALSE);
            _b(p + "vector-length", args => ((SchemeVector)args[0]!).Data.Count);
            _b(p + "vector-ref", args => ((SchemeVector)args[0]!).Data[NumericHelper.ToInt(args[1])]);
            _b(p + "vector-set!", args => { ((SchemeVector)args[0]!).Data[NumericHelper.ToInt(args[1])] = args[2]; return Const.VOID; });
            _b("make-" + p + "vector", args => new SchemeVector(Enumerable.Repeat(args.Length > 1 ? args[1] : 0L, NumericHelper.ToInt(args[0])).Cast<object?>()));
        }
        _b("json-read-string", args => JsonToScheme(System.Text.Json.JsonDocument.Parse(ToStr(args[0])).RootElement));
        _b("json-write-string", args => new SchemeString(JsonSerializer.Serialize(SchemeToJson(args[0]))));
        // numeric aliases & predicates
        _b("add1", args => NumericHelper.Add(args[0], 1L));
        _b("sub1", args => NumericHelper.Sub(args[0], 1L));
        _b("sub1*", args => NumericHelper.Sub(args[0], 1L));
        _b("number=?", args => NumEqual(args));
        _b("boolean=?", args => BoolEqual(args));
        _b("boolean->string", args => ReferenceEquals(args[0], Const.TRUE) ? new SchemeString("#t") : new SchemeString("#f"));
        _b("nan?", args => NumericHelper.ToDouble(args[0]) != NumericHelper.ToDouble(args[0]) ? Const.TRUE : Const.FALSE);
        _b("finite?", args => FiniteP(args[0]) ? Const.TRUE : Const.FALSE);
        _b("infinite?", args => args[0] is double d && double.IsInfinity(d) ? Const.TRUE : Const.FALSE);
        _b("exact-nonnegative-integer?", args =>
            args[0] is long l && l >= 0 || args[0] is int i && i >= 0 || args[0] is BigInteger bi && bi >= 0 ? Const.TRUE : Const.FALSE);
        _b("exact-rational?", args => args[0] is SchemeFraction or int or long or BigInteger ? Const.TRUE : Const.FALSE);
        _b("scheme-lcm", args => PLcm(args));
        _b("atom?", args => args[0] is not Cell ? Const.TRUE : Const.FALSE);
        _b("default-object?", args => args[0] is Void ? Const.TRUE : Const.FALSE);
        _b("symbol=?", args => SymbolEqual(args));
        _b("array?", args => args[0] is SchemeVector ? Const.TRUE : Const.FALSE);
        _b("name", args => args[0] is Sym sy ? sy.Name : new SchemeString(Printer.Format(args[0])));
        _b("pp", args => { Console.WriteLine(Printer.Format(args[0])); return Const.VOID; });
        _b("cartesian-product", args => CartesianProduct(args));
        _b("unfold", args => Unfold(args, false));
        _b("unfold-right", args => Unfold(args, true));
        _b("bitwise-merge", args => (NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[1])) | (~NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[2])));

        // conditions
        _b("error?", args => IsErrorType(args[0]) ? Const.TRUE : Const.FALSE);
        _b("file-error?", args => IsFileError(args[0]) ? Const.TRUE : Const.FALSE);
        _b("read-error?", args => IsReadError(args[0]) ? Const.TRUE : Const.FALSE);
        _b("condition-has-type?", args => HasConditionType(args[0], args[1]) ? Const.TRUE : Const.FALSE);
        _b("condition-type?", args => IsConditionType(args[0]) ? Const.TRUE : Const.FALSE);
        _b("condition/report-string", args => new SchemeString(ReportString(args[0])));

        // maybe / just / nothing
        _b("maybe?", args => MaybeP(args[0]) ? Const.TRUE : Const.FALSE);
        _b("just", args => new Cell(args[0], Const.NIL));
        _b("maybe", args => args[0]);
        _b("nothing", args => Const.FALSE);
        _b("just?", args => args[0] is Cell jc && jc.Cdr is Nil ? Const.TRUE : Const.FALSE);
        _b("nothing?", args => args[0] is Nil || ReferenceEquals(args[0], Const.FALSE) ? Const.TRUE : Const.FALSE);

        _b("maybe-ref", args => args[0] is Cell mc ? mc.Car : (args.Length > 1 ? args[1] : Const.FALSE));

        // bytevector <-> string
        _b("bytevector->string", args => new SchemeString(args[0] is SchemeBytevector bv ? Encoding.UTF8.GetString(bv.Data) : ToStr(args[0])));
        _b("string->bytevector", args => new SchemeBytevector(Encoding.UTF8.GetBytes(ToStr(args[0]))));

        // ports
        _b("textual-port?", args => IsPort(args[0], null) ? Const.TRUE : Const.FALSE);
        _b("char-ready?", args => CharReady(args));
        _b("u8-ready?", args => CharReady(args));
        _b("peek-u8", args => ReadU8(args, true));
        _b("read-u8", args => ReadU8(args, false));
         _b("write-u8", args => WriteU8(args));
         _b("read-bytevector", args => ReadBytevector(args, false));
         _b("read-bytevector!", args => ReadBytevector(args, true));
         _b("write-bytevector", args => WriteBytevector(args));
         _b("bytevector-s8-ref", args => (long)(sbyte)AsBytevector(args[0])[NumericHelper.ToInt(args[1])]);
         _b("bytevector-s8-set!", args => { AsBytevector(args[0])[NumericHelper.ToInt(args[1])] = unchecked((byte)NumericHelper.ToInt(args[2])); return Const.VOID; });
         _b("flush-output-port", _ => { Console.Out.Flush(); return Const.VOID; });
         _b("call-with-output-string", args => CallWithStringOutput(args[0]));

        // json
        _b("json-read", args => JsonRead(args));
        _b("json-write", args => JsonWrite(args));

        // mapping
        _b("mapping", args => Mapping(args));
        _b("mapping?", args => MappingP(args[0]) ? Const.TRUE : Const.FALSE);

        // generators
        _b("generator-append", args => GeneratorAppend(args));
        _b("generator-drop", args => GeneratorDrop(args));
        _b("generator-fold", args => GeneratorFold(args));

        // streams (SRFI-41): stream = Cell(car, thunk) with lazy cdr
        _b("stream-car", args => args[0] is Cell sc ? sc.Car : Const.NIL);
        _b("stream-cdr", args => StreamNext(args[0]));
        _b("stream-null?", args => args[0] is Nil ? Const.TRUE : Const.FALSE);
        _b("stream-ref", args => StreamRef(args[0], NumericHelper.ToInt(args[1])));
        _b("stream-map", args => StreamMap(args[0], args[1]));
        _b("stream-filter", args => StreamFilter(args[0], args[1]));
        _b("stream-take", args => StreamTake(args[0], NumericHelper.ToInt(args[1])));
        _b("stream->list", args => StreamToList(args[0]));
        _b("list->stream", args => ListToStream(args[0]));

        // streams
        _b("nat-stream", args => NatStream(args));
        _b("naturals", args => NatStream(args));
        _b("sieve", args => Sieve(args[0]));
        Evaluator.GlobalEnv.Define("primes", Primes());

        // random
        _b("random-integer", args => NextRandom(NumericHelper.ToInt(args[0])));
        _b("random-real", args => NextRandom(1000000) / 1000000.0);
        _b("random-seed", args => { SeedRandom(NumericHelper.ToLong(args[0])); return Const.VOID; });

        // write-string
        _b("write-string", args =>
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
        });

        return Const.VOID;
    }

    private static object? RegisterExtSchemeCoverage()
    {
        _b("reciprocal", args => NumericHelper.Div(1L, args[0]));
        _b("exact-integer?", args => args[0] is int or long or BigInteger ? Const.TRUE : Const.FALSE);
        _b("num-den", args => new Cell(PNumerator([args[0]]), PDenominator([args[0]])));
        _b("sort", args => args[0] is Sym or Delegate or LambdaProc or CompiledLambda or Func<object?[], object?>
            ? SortList([args[0], args[1]])
            : SortList([args[1], args[0]]));
        _b("tree->list", args => TreeToList(args[0]));
        _b("ucs-range->char-set", args => UcsRangeCharSet(args));
        _b("char-set:empty", _ => new bool[256]);
        _b("char-set:full", _ => Enumerable.Repeat(true, 256).ToArray());
        _b("char-set:lower-case", _ => UcsRangeCharSet([97L, 123L]));
        _b("char-set:lower", _ => UcsRangeCharSet([97L, 123L]));
        _b("char-set:upper-case", _ => UcsRangeCharSet([65L, 91L]));
        _b("char-set:upper", _ => UcsRangeCharSet([65L, 91L]));
        _b("char-set:digit", _ => UcsRangeCharSet([48L, 58L]));
        _b("char-set:letter", _ => CharSetBinOp([UcsRangeCharSet([97L, 123L]), UcsRangeCharSet([65L, 91L])], true));
        _b("char-set:whitespace", _ => MakeCharSet(" \t\r\n"));
        _b("char-set:blank", _ => MakeCharSet(" \t"));
        _b("char-set:iso-control", _ => UcsRangeCharSet([0L, 32L]));
        _b("char-set:punctuation", _ => MakeCharSet(".,;:!?-'\"()[]{}\\/@#$%^&*+=<>|~"));
        _b("char-set:graphic", _ => CharSetBinOp([
            CharSetBinOp([UcsRangeCharSet([97L, 123L]), UcsRangeCharSet([65L, 91L])], true),
            UcsRangeCharSet([48L, 58L]),
            MakeCharSet(".,;:!?-'\"()[]{}\\/@#$%^&*+=<>|~")
        ], true));
        _b("char-set:printing", _ => UcsRangeCharSet([32L, 127L]));
        _b("char-set:symbol", _ => MakeCharSet("$%&*+-./:<=>?@^_~"));
        _b("char-set:hex-digit", _ => MakeCharSet("0123456789abcdefABCDEF"));
        _b("json-encode", args => new SchemeString(JsonSerializer.Serialize(SchemeToJson(args[0]))));
        _b("list-transduce", args => Transduce(args[0], args[1], args[2], args[3], "list"));
        _b("vector-transduce", args => Transduce(args[0], args[1], args[2], args[3], "vector"));
        _b("string-transduce", args => Transduce(args[0], args[1], args[2], args[3], "string"));
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
