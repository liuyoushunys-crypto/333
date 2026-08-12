using System.Numerics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Miniscm.Types;
using Miniscm.Eval;
using Miniscm.Compiler;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    private static void _b(string name, Func<object?[], object?> fn) => Evaluator.GlobalEnv.Define(name, fn);

    public static void Init()
    {
        // ── Type predicates ──
        _b("bound-identifier=?", args => EqSymbols(args[0], args[1]) ? Const.TRUE : Const.FALSE);
        _b("box?", args => args[0] is ValueTuple<string, object?> b && b.Item1 == "box" ? Const.TRUE : Const.FALSE);
        _b("bytevector?", args => args[0] is SchemeBytevector ? Const.TRUE : Const.FALSE);
        _b("char?", args => args[0] is SchemeChar ? Const.TRUE : Const.FALSE);
        _b("complex?", args => args[0] is Complex or int or long or BigInteger or SchemeFraction or double or float ? Const.TRUE : Const.FALSE);
        _b("datum->syntax", args => args.Length > 1 ? args[1] : args[0]);
        _b("eof-object", args => Const.EOF);
        _b("eof-object?", args => args[0] is Eof ? Const.TRUE : Const.FALSE);
        _b("free-identifier=?", args => EqSymbols(args[0], args[1]) ? Const.TRUE : Const.FALSE);
        _b("input-port?", args => IsPort(args[0], "input") ? Const.TRUE : Const.FALSE);
        _b("integer?", args => IsInteger(args[0]) ? Const.TRUE : Const.FALSE);
        _b("number?", args => args[0] is int or long or BigInteger or double or float or decimal or Complex or SchemeFraction ? Const.TRUE : Const.FALSE);
        _b("output-port?", args => IsPort(args[0], "output") ? Const.TRUE : Const.FALSE);
        _b("port?", args => IsPort(args[0], null) ? Const.TRUE : Const.FALSE);
        _b("promise?", args => args[0] is Promise ? Const.TRUE : Const.FALSE);
        _b("rational?", args => args[0] is SchemeFraction or int or long or BigInteger ? Const.TRUE : Const.FALSE);
        _b("real?", args => (args[0] is int or long or BigInteger or SchemeFraction or double or float or decimal || (args[0] is Complex rc && rc.Imaginary == 0)) ? Const.TRUE : Const.FALSE);
        _b("string?", args => args[0] is string or SchemeString ? Const.TRUE : Const.FALSE);
        _b("syntax->datum", args => args[0]);
        _b("syntax?", args => args[0] is Sym ? Const.TRUE : Const.FALSE);
        _b("vector?", args => args[0] is SchemeVector ? Const.TRUE : Const.FALSE);
        _b("void?", args => args[0] is Void ? Const.TRUE : Const.FALSE);

        // ── Equality ──

        // ── Pairs and lists ──
        _b("assoc", args => Assoc(args[0], args[1], false));
        _b("assv", args => Assoc(args[0], args[1], false));
        _b("break-list", args => BreakList(args[0], args[1]));
        _b("caddr", args => ((Cell)args[0]!).Cdr is Cell c1 ? c1.Cdr is Cell c1a ? c1a.Car : Const.NIL : Const.NIL);
        _b("cadddr", args => ((Cell)args[0]!).Cdr is Cell c2 && c2.Cdr is Cell c3 ? c3.Cdr is Cell c3a ? c3a.Car : Const.NIL : Const.NIL);
        _b("last", args => LastPair(args[0]) is Cell c ? c.Car : Const.FALSE);
        _b("list-set!", PListSetBang);
        _b("make-list", PMakeList);
        _b("member", PMember);
        _b("memv", PMemv);
        _b("pair-fold", args =>
        {
            object? acc = args[1];
            var cur = args[2];
            while (cur is Cell c) { acc = App(args[0], cur, acc); cur = c.Cdr; }
            return acc;
        });
        _b("pair-fold-right", args =>
        {
            var pairs = new List<object?>();
            var cur = args[2];
            while (cur is Cell c) { pairs.Add(cur); cur = c.Cdr; }
            object? acc = args[1];
            for (int i = pairs.Count - 1; i >= 0; i--)
                acc = App(args[0], pairs[i], acc);
            return acc;
        });
        _b("remove", args => args[1].Cells().Where(x => ReferenceEquals(App(args[0], x), Const.FALSE)).ToCell());
        _b("split-at", args =>
        {
            var first = new List<object?>();
            var cur = args[0];
            int n = NumericHelper.ToInt(args[1]);
            while (cur is Cell c && n-- > 0) { first.Add(c.Car); cur = c.Cdr; }
            return new Cell(first.ToCell(), new Cell(cur, Const.NIL));
        });

        // ── Arithmetic ──
        _b("*", args => args.Aggregate((object?)1L, (acc, x) => NumericHelper.Mul(acc!, x))!);
        _b("-1+", args => NumericHelper.ToLong(args[0]) - 1);
        _b("/", PDiv);
        _b("1+", args => NumericHelper.ToLong(args[0]) + 1);
        _b("abs", PAbs);
        _b("acos", args => Math.Acos(NumericHelper.ToDouble(args[0])));
        _b("asin", args => Math.Asin(NumericHelper.ToDouble(args[0])));
        _b("atan", args => Math.Atan(NumericHelper.ToDouble(args[0])));
        _b("ceiling", PCeiling);
        _b("cos", args => Math.Cos(NumericHelper.ToDouble(args[0])));
        _b("denominator", PDenominator);
        _b("even?", PEvenQ);
        _b("exact->inexact", PExactInexact);
        _b("exact-integer-sqrt", PExactIntegerSqrt);
        _b("exact?", args => args[0] is int or long or BigInteger or SchemeFraction ? Const.TRUE : Const.FALSE);
        _b("exp", args => Math.Exp(NumericHelper.ToDouble(args[0])));
        _b("expt", PExpt);
        _b("floor", PFloor);
        _b("gcd", PGcd);
        _b("inexact->exact", PInexactExact);
        _b("inexact?", args => args[0] is double or float or Complex ? Const.TRUE : Const.FALSE);
        _b("lcm", PLcm);
        _b("log", args => Math.Log(NumericHelper.ToDouble(args[0])));
        _b("max", PMax);
        _b("min", PMin);
        _b("modulo", args => NumericHelper.Modulo(args[0], args[1]));
        _b("negative?", args => NumericHelper.Compare(args[0], 0L) < 0 ? Const.TRUE : Const.FALSE);
        _b("numerator", PNumerator);
        _b("odd?", POddQ);
        Evaluator.GlobalEnv.Define("pi", Math.PI);
        _b("positive?", args => NumericHelper.Compare(args[0], 0L) > 0 ? Const.TRUE : Const.FALSE);
        _b("quotient", args => NumericHelper.Quotient(args[0], args[1]));
        _b("rationalize", PRationalize);
        _b("remainder", args => NumericHelper.Remainder(args[0], args[1]));
        _b("round", PRound);
        _b("sin", args => Math.Sin(NumericHelper.ToDouble(args[0])));
        _b("sqrt", PSqrt);
        _b("string->number", PStringNumber);
        _b("tan", args => Math.Tan(NumericHelper.ToDouble(args[0])));
        _b("truncate", PTruncate);
        _b("zero?", args => NumericHelper.IsZero(args[0]) ? Const.TRUE : Const.FALSE);

        // ── Comparisons ──
        _b("condition?", args => args[0] is SchemeException or ErrorObject ? Const.TRUE : Const.FALSE);
        _b("condition-message", args =>
        {
            if (args[0] is ErrorObject eo) return eo.Message is Sym em ? em.Name : eo.Message;
            if (args[0] is SchemeException se) return se.Val?.ToString() ?? "";
            return ToStr(args[0]);
        });
        _b("condition-type", args => args[0] is ErrorObject eo2 ? eo2.Message : Const.NIL);
        _b("condition/report-string", args =>
        {
            if (args[0] is ErrorObject eo3) return new SchemeString(eo3.Message is Sym em3 ? em3.Name : ToStr(eo3.Message));
            return new SchemeString("unknown condition");
        });
        _b("digit-value", PDigitValue);

        // ── Strings ──
        _b("list->string", PListString);
        _b("make-string", PMakeString);
        _b("string", args => new SchemeString(args.Select(AsChar)));
        _b("string->list", PStringList);
        _b("string->symbol", args => Sym.Intern(ToStr(args[0])));
        _b("string->utf8", args => new SchemeBytevector(Encoding.UTF8.GetBytes(ToStr(args[0]))));
        _b("string-append", args => new SchemeString(string.Concat(args.Select(ToStr))));
        _b("string-ci<=?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) <= 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci<?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) < 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci=?", args => string.Equals(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) ? Const.TRUE : Const.FALSE);
        _b("string-ci>=?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) >= 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci>?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) > 0 ? Const.TRUE : Const.FALSE);
        _b("string-contains?", PStringContainsQ);
        _b("utf8->string", args =>
        {
            byte[] data = args[0] is SchemeBytevector bv ? bv.Data : Encoding.UTF8.GetBytes(ToStr(args[0]));
            int start = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
            int end = args.Length > 2 ? NumericHelper.ToInt(args[2]) : data.Length;
            if (start < 0) start = 0;
            if (end > data.Length) end = data.Length;
            return new SchemeString(Encoding.UTF8.GetString(data, start, end - start));
        });
        _b("string-copy", PStringCopy);
        _b("string-downcase", args => new SchemeString(ToStr(args[0]).ToLowerInvariant()));
        _b("string-fill!", PStringFillBang);
        _b("string-length", PStringLength);
        _b("string-for-each", args =>
        {
            var fn = args[0];
            var s = ToStr(args[1]);
            foreach (var rune in s.EnumerateRunes())
                App(fn, new SchemeChar(rune.Value));
            return Const.VOID;
        });
        _b("string-map", args =>
        {
            var fn = args[0];
            var s = ToStr(args[1]);
            var sb = new StringBuilder();
            foreach (var rune in s.EnumerateRunes())
                sb.Append(char.ConvertFromUtf32(AsChar(App(fn, new SchemeChar(rune.Value)))));
            return new SchemeString(sb.ToString());
        });
        _b("string-ref", PStringRef);
        _b("string-set!", PStringSetBang);
        _b("string-upcase", args => new SchemeString(ToStr(args[0]).ToUpperInvariant()));
        _b("string<=?", args => string.Compare(ToStr(args[0]), ToStr(args[1])) <= 0 ? Const.TRUE : Const.FALSE);
        _b("string<?", args => string.Compare(ToStr(args[0]), ToStr(args[1])) < 0 ? Const.TRUE : Const.FALSE);
        _b("string=?", args => ToStr(args[0]) == ToStr(args[1]) ? Const.TRUE : Const.FALSE);
        _b("string>=?", args => string.Compare(ToStr(args[0]), ToStr(args[1])) >= 0 ? Const.TRUE : Const.FALSE);
        _b("string>?", args => string.Compare(ToStr(args[0]), ToStr(args[1])) > 0 ? Const.TRUE : Const.FALSE);
        _b("substring", PSubstring);
        _b("symbol->string", PSymbolString);

        // ── Chars ──
        _b("char->integer", args => (long)AsChar(args[0]));
        _b("char-alphabetic?", PCharAlphabeticQ);
        _b("char-ci=?", PCharCiEq);
        _b("char-downcase", args => new SchemeChar(Rune.ToLowerInvariant(new Rune(AsChar(args[0]))).Value));
        _b("char-foldcase", args => new SchemeChar(Rune.ToLowerInvariant(new Rune(AsChar(args[0]))).Value));
        _b("char-lower-case?", PCharLowerCaseQ);
        _b("char-numeric?", PCharNumericQ);
        _b("char-upcase", args => new SchemeChar(Rune.ToUpperInvariant(new Rune(AsChar(args[0]))).Value));
        _b("char-upper-case?", PCharUpperCaseQ);
        _b("char-whitespace?", PCharWhitespaceQ);
        _b("char<=?", PCharLe);
        _b("char<?", PCharLt);
        _b("char=?", PCharEq);
        _b("char>=?", PCharGe);
        _b("char>?", PCharGt);
        _b("integer->char", args => new SchemeChar((int)NumericHelper.ToLong(args[0])));

        // ── Vectors ──
        _b("list->vector", args => new SchemeVector(args[0].Cells()));
        _b("make-vector", PMakeVector);
        _b("vector", args => new SchemeVector(args));
        _b("vector->list", args => args[0] is SchemeBytevector bvl ? bvl.Data.Select(b => (object?)(long)b).ToCell() : AsVector(args[0]).Data.ToCell());
        _b("vector-append", PVectorAppend);
        _b("vector-copy", args => new SchemeVector(AsVector(args[0]).Data));
        _b("vector-fill!", args => { var v = AsVector(args[0]); for (int i = 0; i < v.Length; i++) v[i] = args[1]; return Const.VOID; });
        _b("vector-length", args => args[0] is SchemeBytevector bvl2 ? bvl2.Length : AsVector(args[0]).Length);
        _b("vector-ref", args => args[0] is SchemeBytevector bvr ? (object?)(long)bvr[NumericHelper.ToInt(args[1])] : AsVector(args[0])[NumericHelper.ToInt(args[1])]);
        _b("vector-set!", args => { AsVector(args[0])[NumericHelper.ToInt(args[1])] = args[2]; return Const.VOID; });

        // ── Bytevectors ──
        _b("bytevector", args => new SchemeBytevector(args.Select(NumericHelper.ToInt)));
        _b("bytevector-append", args => new SchemeBytevector(args.SelectMany(a => AsBytevector(a).Data).ToArray()));
        _b("bytevector->u8-list", args => AsBytevector(args[0]).Data.Select(b => (object?)(long)b).ToCell());
        _b("bytevector-copy", args => new SchemeBytevector([.. AsBytevector(args[0]).Data]));
        _b("bytevector-length", args => AsBytevector(args[0]).Length);
        _b("bytevector-u8-ref", args => (long)AsBytevector(args[0])[NumericHelper.ToInt(args[1])]);
        _b("bytevector-u8-set!", args => { AsBytevector(args[0])[NumericHelper.ToInt(args[1])] = (byte)NumericHelper.ToInt(args[2]); return Const.VOID; });
        _b("make-bytevector", PMakeBytevector);
        _b("u8-list->bytevector", args => new SchemeBytevector(args[0].Cells().Select(NumericHelper.ToInt)));

        // ── Higher-order ──
        _b("any", PAny);
        _b("apply", PApply);
        _b("break", PBreak);
        _b("compose", PCompose);
        _b("drop", PDrop);
        _b("drop-while", PDropWhile);
        _b("every", PEvery);
        _b("find", PFind);
        _b("fold", PFold);
        _b("fold-right", PFoldRight);
        _b("iota", PIota);
        _b("partition", PPartition);
        _b("span", PSpan);
        _b("take", PTake);
        _b("take-while", PTakeWhile);

        // ── SRFI-158: Generators ──
        _b("generator", PGenerator);
        _b("generator?", args => args[0] is Delegate or LambdaProc or CompiledLambda ? Const.TRUE : Const.FALSE);
        _b("make-generator", PMakeGenerator);
        _b("list->generator", PListGenerator);
        _b("vector->generator", PVectorGenerator);
        _b("string->generator", PStringGenerator);
        _b("generator->list", PGeneratorToList);
        _b("generator->vector", PGeneratorToVector);
        _b("generator->string", PGeneratorToString);
        _b("generator-map", PGeneratorMap);
        _b("generator-filter", PGeneratorFilter);
        _b("generator-take", PGeneratorTake);
        _b("generator-count", PGeneratorCount);
        _b("generator-find", PGeneratorFind);
        _b("generator-for-each", PGeneratorForEach);
        _b("make-iota-generator", PMakeIotaGenerator);
        _b("make-range-generator", PMakeRangeGenerator);

        // ── I/O ports ──
        _b("call-with-input-file", PCallWithInputFile);
        _b("call-with-output-file", PCallWithOutputFile);
        _b("close-input-port", args => Const.VOID);
        _b("close-output-port", args => Const.VOID);
        _b("current-error-port", args => MakePort("output", Console.Error));
        _b("current-input-port", args => MakePort("input", Console.In));
        _b("current-output-port", PCurrentOutputPort);
        _b("get-output-string", PGetOutputString);
        _b("open-input-string", args => MakePort("input", new StringPort(ToStr(args[0]))));
        _b("open-input-bytevector", args => MakePort("input", new BytePort(AsBytevector(args[0]).Data)));
        _b("open-output-string", args => MakePort("output", new StringBuilder()));
        _b("peek-char", PPeekChar);
        _b("port-position", PPortPosition);
        _b("read", PRead);
        _b("read-char", PReadChar);
        _b("read-line", PReadLine);
        _b("read-string", PReadString);
        _b("set-port-position!", PSetPortPositionBang);
        _b("with-input-from-file", PWithInputFromFile);
        _b("with-output-to-file", PWithOutputToFile);

        // ── Exceptions ──
        _b("error-object-irritants", args => args[0] is ErrorObject eo ? eo.Irritants : Const.NIL);
        _b("error-object-message", args => args[0] is ErrorObject eo ? eo.Message : Const.FALSE);
        _b("error-object?", args => args[0] is ErrorObject ? Const.TRUE : Const.FALSE);
        _b("raise", PRaise);
        _b("raise-continuable", PRaiseContinuable);
        _b("with-exception-handler", PWithExceptionHandler);

        // ── Boxes ──
        _b("box", args => (ValueTuple<string, object?>)("box", args[0]));
        _b("make-box", args => (ValueTuple<string, object?>)("box", args[0]));
        _b("set-box!", PSetBoxBang);
        _b("unbox", args => args[0] is ValueTuple<string, object?> t && t.Item1 == "box" ? t.Item2! : throw new Exception("not a box"));

        // ── Control flow ──
        _b("call-with-current-continuation", PCallWithCurrentContinuation);
        _b("call-with-values", PCallWithValues);
        _b("call/cc", PCallCc);
        _b("dynamic-wind", PDynamicWind);
        _b("force", PForce);
        _b("values", args => args.Length == 1 ? args[0] : new SchemeVector(args));

        // ── Environment ──
        _b("environment", args => Evaluator.GlobalEnv);
        _b("exit", args => Const.VOID);
        _b("interaction-environment", args => Evaluator.GlobalEnv);
        _b("load", PLoad);
        _b("null-environment", args => Evaluator.GlobalEnv);
        _b("scheme-report-environment", args => Evaluator.GlobalEnv);
        // ── Hash tables ──
        _b("hash-table-clear!", PHashTableClearBang);
        _b("hash-table-contains?", PHashTableContainsQ);
        _b("hash-table-count", PHashTableCount);
        _b("hash-table-delete!", PHashTableDeleteBang);
        _b("hash-table-ref", PHashTableRef);
        _b("hash-table-set!", PHashTableSetBang);
        _b("hash-table?", args => args[0] is Dictionary<object, object?> ? Const.TRUE : Const.FALSE);
        _b("hash-table-size", args => (long)((Dictionary<object, object?>)args[0]!).Count);
        _b("hash-table-exists?", args => ((Dictionary<object, object?>)args[0]!).ContainsKey(args[1]!) ? Const.TRUE : Const.FALSE);
        _b("hash-table-ref/default", args => ((Dictionary<object, object?>)args[0]!).TryGetValue(args[1]!, out var dv) ? dv : (args.Length > 2 ? args[2] : Const.FALSE));
        _b("hash-table-copy", args => new Dictionary<object, object?>((Dictionary<object, object?>)args[0]!));
        _b("hash-table-keys", args => ((Dictionary<object, object?>)args[0]!).Keys.ToList().ToCell());
        _b("hash-table-values", args => ((Dictionary<object, object?>)args[0]!).Values.ToList().ToCell());
        _b("hash-table->alist", args =>
        {
            var items = new List<object?>();
            foreach (var kv in (Dictionary<object, object?>)args[0]!)
                items.Add(new Cell(kv.Key, kv.Value));
            return items.ToCell();
        });
        _b("alist->hash-table", args =>
        {
            var ht = new Dictionary<object, object?>();
            var cur = args[0];
            while (cur is Cell c)
            {
                if (c.Car is Cell pair) ht[pair.Car!] = pair.Cdr;
                cur = c.Cdr;
            }
            return ht;
        });
        _b("hash-table-for-each", args =>
        {
            var fn = args[0];
            foreach (var kv in (Dictionary<object, object?>)args[1]!)
                JitRuntime.Invoke(fn, [kv.Key, kv.Value], Evaluator.GlobalEnv);
            return Const.VOID;
        });
        _b("hash-table-map", args =>
        {
            var fn = args[0];
            var items = new List<object?>();
            foreach (var kv in (Dictionary<object, object?>)args[1]!)
                items.Add(JitRuntime.Invoke(fn, [kv.Key, kv.Value], Evaluator.GlobalEnv));
            return items.ToCell();
        });
        _b("hash-table-fold", args =>
        {
            var fn = args[0];
            object? acc = args[1];
            foreach (var kv in (Dictionary<object, object?>)args[2]!)
                acc = JitRuntime.Invoke(fn, [acc, kv.Key, kv.Value], Evaluator.GlobalEnv);
            return acc;
        });
        _b("make-hash-table", args => new Dictionary<object, object?>());
        _b("make-eq-hash-table", args => new Dictionary<object, object?>());
        _b("make-equal-hash-table", args => new Dictionary<object, object?>());
        _b("make-eqv-hash-table", args => new Dictionary<object, object?>());
        _b("make-strong-hash-table", args => new Dictionary<object, object?>());

        // ── Time ──
        _b("current-jiffy", args => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _b("current-second", args => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _b("jiffies-per-second", args => 1000L);
        _b("jiffies-per-second", args => (long)1000000);

        // ── Ports and files ──
        _b("binary-port?", args => args[0] is ITuple it && it.Length > 2 && it[0] is "port" && it[2] is BytePort ? Const.TRUE : Const.FALSE);
        _b("input-port-open?", args => IsPort(args[0], "input") ? Const.TRUE : Const.FALSE);
        _b("output-port-open?", args => IsPort(args[0], "output") ? Const.TRUE : Const.FALSE);
        _b("close-port", args =>
        {
            if (args[0] is ITuple it && it.Length > 2 && it[0] is "port" && it[2] is IDisposable d) d.Dispose();
            return Const.VOID;
        });
        _b("delete-file", args => { File.Delete(ToStr(args[0])); return Const.VOID; });
        _b("file-exists?", args => File.Exists(ToStr(args[0])) ? Const.TRUE : Const.FALSE);
        _b("open-input-file", args => MakePort("input", new StreamReader(ToStr(args[0]))));
        _b("open-binary-input-file", args => MakePort("input", new BytePort(File.ReadAllBytes(ToStr(args[0])))));
        _b("open-output-file", args => MakePort("output", new StreamWriter(ToStr(args[0]))));
        _b("port-open?", args => IsPort(args[0], null) ? Const.TRUE : Const.FALSE);
        _b("input-port-open?", args => IsPort(args[0], "input") ? Const.TRUE : Const.FALSE);
        _b("output-port-open?", args => IsPort(args[0], "output") ? Const.TRUE : Const.FALSE);
        _b("rename-file", args => { File.Move(ToStr(args[0]), ToStr(args[1])); return Const.VOID; });

        // ── Conditions ──
        _b("make-compound-condition", args => new ErrorObject(Sym.Intern("compound"), args.Length > 0 ? args.ToList().ToCell() : Const.NIL));

        // ── Misc ──
        _b("alist-copy", AlistCopy);
        _b("complement", PComplement);
        _b("constantly", PConstantly);
        _b("defined?", PDefinedQ);
        _b("environment?", args => args[0] is Env ? Const.TRUE : Const.FALSE);
        _b("features", args => new Cell(Sym.Intern("r7rs"), new Cell(Sym.Intern("miniscm"), Const.NIL)));
        _b("flip", PFlip);
        _b("format", PFormat);
        _b("gensym", args => Sym.Intern($"g{++_gensymCtr}"));
        _b("gensym2", args => Sym.Intern($"__g{++_gensymCtr}"));
        _b("helper", args => Const.VOID);
        _b("identifier?", args => args[0] is Sym or SyntaxObject ? Const.TRUE : Const.FALSE);
        _b("identity", args => args[0]);
        _b("make-coroutine-generator", args => MakeCoroutineGenerator(args[0]));
        _b("make-parameter", args => MakeParameter(args[0], args.Length > 1 ? args[1] : null));
        _b("make-promise", args => new Promise(() => args.Length > 0 ? args[0] : Const.VOID));
        _b("make-weak-box", args => new Cell(Sym.Intern("weak"), args.Length > 0 ? args[0] : Const.NIL));
        _b("sink", args => Const.VOID);
        _b("sum", args => args.Select(Convert.ToInt64).Sum());
        _b("weak-box?", args => args[0] is Cell wc && wc.Car is Sym ws && ws.Name == "weak" ? Const.TRUE : Const.FALSE);
        _b("weak-box-ref", args => args[0] is Cell wc && wc.Cdr is Cell wd ? wd.Car : Const.NIL);
        _b("weak-box-set!", args => { if (args[0] is Cell wc) wc.Cdr = new Cell(args[1], Const.NIL); return Const.VOID; });

        // ── Bitwise ──
        _b("arithmetic-shift", PArithmeticShift);
        _b("bits->integer", args => BitsToInteger(args[0]));
        _b("bits->list", args => IntegerToBitsList(NumericHelper.ToInt(args[0]), args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0));
        _b("bit-and", args => args.Aggregate(-1L, (a, b) => a & NumericHelper.ToLong(b)));
        _b("bit-count", PBitCount);
        _b("bit-field", PBitField);
        _b("bit-ior", args => args.Aggregate(0L, (a, b) => a | NumericHelper.ToLong(b)));
        _b("bit-not", args => ~NumericHelper.ToLong(args[0]));
        _b("bit-or", args => args.Aggregate(0L, (a, b) => a | NumericHelper.ToLong(b)));
        _b("bit-set?", args => (NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]) & 1) != 0 ? Const.TRUE : Const.FALSE);
        _b("bit-shift", PBitShift);
        _b("bit-xor", args => args.Aggregate(0L, (a, b) => a ^ NumericHelper.ToLong(b)));
        _b("bitwise-and", args => args.Aggregate(-1L, (a, b) => a & NumericHelper.ToLong(b)));
        _b("bitwise-any-bit-set?", args => (NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[1])) != 0 ? Const.TRUE : Const.FALSE);
        _b("bitwise-arithmetic-shift", PBitwiseArithmeticShift);
        _b("bitwise-arithmetic-shift-right", PBitwiseArithmeticShiftRight);
        _b("bitwise-bit-field", PBitwiseBitField);
        _b("bitwise-copy-bit", PBitwiseCopyBit);
        _b("bitwise-copy-bit-field", PBitwiseCopyBitField);
        _b("bitwise-count", PBitwiseCount);
        _b("bitwise-if", PBitwiseIf);
        _b("bitwise-ior", args => args.Aggregate(0L, (a, b) => a | NumericHelper.ToLong(b)));
        _b("bitwise-length", PBitwiseLength);
        _b("bitwise-not", args => ~NumericHelper.ToLong(args[0]));
        _b("bitwise-reverse-bit-field", PBitwiseReverseBitField);
        _b("bitwise-rotate", PBitwiseRotate);
        _b("bitwise-rotate-bit-field", PBitwiseRotateBitField);
        _b("bitwise-shift", PBitwiseShift);
        _b("bitwise-xor", args => args.Aggregate(0L, (a, b) => a ^ NumericHelper.ToLong(b)));
        _b("booleans->integer", args =>
        {
            long r = 0;
            for (int i = 0; i < args.Length; i++)
                if (ReferenceEquals(args[i], Const.TRUE)) r |= 1L << i;
            return r;
        });
        _b("copy-bit", PCopyBit);
        _b("first-set-bit", PFirstSetBit);
        _b("integer->bits", args => IntegerToBitsList(NumericHelper.ToInt(args[0]), args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0));
        _b("integer->list", args => IntegerToBitsList(NumericHelper.ToInt(args[0]), 0));
        _b("list->bits", args => BitsToInteger(args[0]));
        _b("list->integer", args => BitsToInteger(args[0]));
        _b("integer-length", PIntegerLength);
        _b("logbit?", args => (NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]) & 1) != 0 ? Const.TRUE : Const.FALSE);
        _b("logtest", args => (NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[1])) != 0 ? Const.TRUE : Const.FALSE);

        // ── Complex ──
        _b("angle", PAngle);
        _b("imag-part", PImagPart);
        _b("magnitude", PMagnitude);
        _b("make-polar", PMakePolar);
        _b("make-rectangular", args => new Complex(Convert.ToDouble(args[0]), Convert.ToDouble(args[1])));
        _b("real-part", PRealPart);

        // ── Streams ──
        _b("stream-car", PStreamCar);
        _b("stream-cdr", PStreamCdr);
        _b("stream-filter", PStreamFilter);
        _b("stream-map", PStreamMap);
        _b("stream-null?", args => args[0] is Nil ? Const.TRUE : Const.FALSE);
        _b("stream-ref", PStreamRef);
        _b("stream-take", PStreamTake);

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

    }

}
