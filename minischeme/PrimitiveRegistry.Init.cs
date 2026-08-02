using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Miniscm.Types;
using Miniscm.Eval;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    private static void _b(string name, Func<object?[], object?> fn) => Evaluator.GlobalEnv.Define(name, fn);

    public static void Init()
    {
        // ── Type predicates ──
        _b("boolean?", args => args[0] is Sym s && (s == Const.TRUE || s == Const.FALSE) ? Const.TRUE : Const.FALSE);
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
        _b("not", args => args[0] is Sym s && s == Const.FALSE ? Const.TRUE : Const.FALSE);
        _b("null?", args => args[0] is Nil ? Const.TRUE : Const.FALSE);
        _b("number?", args => args[0] is int or long or BigInteger or double or float or decimal or Complex or SchemeFraction ? Const.TRUE : Const.FALSE);
        _b("output-port?", args => IsPort(args[0], "output") ? Const.TRUE : Const.FALSE);
        _b("pair?", args => args[0] is Cell ? Const.TRUE : Const.FALSE);
        _b("port?", args => IsPort(args[0], null) ? Const.TRUE : Const.FALSE);
        _b("procedure?", args => args[0] is Delegate or LambdaProc or ValueTuple<string, object?> ? Const.TRUE : Const.FALSE);
        _b("promise?", args => args[0] is Promise ? Const.TRUE : Const.FALSE);
        _b("rational?", args => args[0] is SchemeFraction or int or long or BigInteger ? Const.TRUE : Const.FALSE);
        _b("real?", args => args[0] is int or long or BigInteger or SchemeFraction or double or float or decimal ? Const.TRUE : Const.FALSE);
        _b("string?", args => args[0] is string or SchemeString ? Const.TRUE : Const.FALSE);
        _b("symbol?", args => args[0] is Sym ? Const.TRUE : Const.FALSE);
        _b("syntax->datum", args => args[0]);
        _b("syntax?", args => args[0] is Sym ? Const.TRUE : Const.FALSE);
        _b("vector?", args => args[0] is SchemeVector ? Const.TRUE : Const.FALSE);
        _b("void?", args => args[0] is Void ? Const.TRUE : Const.FALSE);

        // ── Equality ──
        _b("eq?", args => ReferenceEquals(args[0], args[1]) || (args[0] is not null && args[0]!.Equals(args[1])) ? Const.TRUE : Const.FALSE);
        _b("equal?", args => Eql(args[0], args[1]));
        _b("eqv?", PEqvQ);

        // ── Pairs and lists ──
        _b("append", PAppend);
        _b("assoc", args => Assoc(args[0], args[1], false));
        _b("assq", args => Assoc(args[0], args[1], true));
        _b("assv", args => Assoc(args[0], args[1], false));
        _b("caar", args => CarFn(CarFn(args[0])));
        _b("cadr", args => CarFn(CdrFn(args[0])));
        _b("car", args => CarFn(args[0]));
        _b("cdar", args => CdrFn(CarFn(args[0])));
        _b("cddr", args => CdrFn(CdrFn(args[0])));
        _b("cdr", args => CdrFn(args[0]));
        _b("cons", args => new Cell(args[0], args[1]));
        _b("length", args => args[0].CellLength());
        _b("list", args => args.ToCell());
        _b("list-copy", PListCopy);
        _b("list-ref", args => args[0].AsCell()![NumericHelper.ToInt(args[1])]);
        _b("list-set!", PListSetBang);
        _b("list-tail", PListTail);
        _b("list?", PListQ);
        _b("make-list", PMakeList);
        _b("member", PMember);
        _b("memq", PMemq);
        _b("memv", PMemv);
        _b("reverse", PReverse);
        _b("set-car!", args => { if (args[0] is Cell c) c.Car = args[1]; return Const.VOID; });
        _b("set-cdr!", args => { if (args[0] is Cell c) c.Cdr = args[1]; return Const.VOID; });

        // ── Arithmetic ──
        _b("*", args => args.Aggregate((object?)1L, (acc, x) => NumericHelper.Mul(acc!, x))!);
        _b("+", args => args.Aggregate((object?)0L, (acc, x) => NumericHelper.Add(acc!, x))!);
        _b("-", PMinus);
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
        _b("number->string", PNumberString);
        _b("numerator", PNumerator);
        _b("odd?", POddQ);
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
        _b("<", PLt);
        _b("<=", PLe);
        _b("=", PEq);
        _b(">", PGt);
        _b(">=", PGe);
        _b("condition-message", args => args[0] is ErrorObject eo ? eo.Message : args[0] is SchemeException se ? se.Val?.ToString() ?? "" : "");
        _b("condition?", args => args[0] is SchemeException or ErrorObject ? Const.TRUE : Const.FALSE);
        _b("digit-value", PDigitValue);

        // ── Strings ──
        _b("list->string", PListString);
        _b("make-string", PMakeString);
        _b("string", args => new SchemeString(args.Select(AsChar)));
        _b("string->list", PStringList);
        _b("string->symbol", args => Sym.Intern(ToStr(args[0])));
        _b("string-append", args => new SchemeString(string.Concat(args.Select(ToStr))));
        _b("string-ci<=?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) <= 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci<?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) < 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci=?", args => string.Equals(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) ? Const.TRUE : Const.FALSE);
        _b("string-ci>=?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) >= 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci>?", args => string.Compare(ToStr(args[0]), ToStr(args[1]), StringComparison.OrdinalIgnoreCase) > 0 ? Const.TRUE : Const.FALSE);
        _b("string-contains?", PStringContainsQ);
        _b("string-copy", PStringCopy);
        _b("string-downcase", args => new SchemeString(ToStr(args[0]).ToLowerInvariant()));
        _b("string-fill!", PStringFillBang);
        _b("string-length", PStringLength);
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
        _b("vector->list", args => AsVector(args[0]).Data.ToCell());
        _b("vector-append", PVectorAppend);
        _b("vector-copy", args => new SchemeVector(AsVector(args[0]).Data));
        _b("vector-fill!", args => { var v = AsVector(args[0]); for (int i = 0; i < v.Length; i++) v[i] = args[1]; return Const.VOID; });
        _b("vector-length", args => AsVector(args[0]).Length);
        _b("vector-ref", args => AsVector(args[0])[NumericHelper.ToInt(args[1])]);
        _b("vector-set!", args => { AsVector(args[0])[NumericHelper.ToInt(args[1])] = args[2]; return Const.VOID; });

        // ── Bytevectors ──
        _b("bytevector", args => new SchemeBytevector(args.Select(NumericHelper.ToInt)));
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
        _b("filter", PFilter);
        _b("find", PFind);
        _b("fold", PFold);
        _b("fold-right", PFoldRight);
        _b("for-each", PForEach);
        _b("iota", PIota);
        _b("map", PMap);
        _b("partition", PPartition);
        _b("span", PSpan);
        _b("take", PTake);
        _b("take-while", PTakeWhile);

        // ── I/O ports ──
        _b("call-with-input-file", PCallWithInputFile);
        _b("call-with-output-file", PCallWithOutputFile);
        _b("close-input-port", args => Const.VOID);
        _b("close-output-port", args => Const.VOID);
        _b("current-error-port", args => MakePort("output", Console.Error));
        _b("current-input-port", args => MakePort("input", Console.In));
        _b("current-output-port", PCurrentOutputPort);
        _b("display", PDisplay);
        _b("get-output-string", PGetOutputString);
        _b("newline", args => { Console.WriteLine(); return Const.VOID; });
        _b("open-input-string", args => MakePort("input", new StringPort(ToStr(args[0]))));
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
        _b("write", PWrite);
        _b("write-char", PWriteChar);

        // ── Exceptions ──
        _b("error", PError);
        _b("error-object-irritants", args => args[0] is ErrorObject eo ? eo.Irritants : Const.NIL);
        _b("error-object-message", args => args[0] is ErrorObject eo ? eo.Message : Const.FALSE);
        _b("error-object?", args => args[0] is ErrorObject ? Const.TRUE : Const.FALSE);
        _b("raise", PRaise);
        _b("raise-continuable", PRaiseContinuable);
        _b("with-exception-handler", PWithExceptionHandler);

        // ── Boxes ──
        _b("box", args => (ValueTuple<string, object?>)("box", args[0]));
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
        _b("eval", args => Evaluator.Eval(args[0],args.Length > 1 && args[1] is Env e ? e : Evaluator.GlobalEnv));
        _b("exit", args => Const.VOID);
        _b("interaction-environment", args => Evaluator.GlobalEnv);
        _b("load", PLoad);
        _b("null-environment", args => Evaluator.GlobalEnv);
        _b("scheme-report-environment", args => Evaluator.GlobalEnv);
        _b("sx-def-env", args => Evaluator.CurrentMacroDefEnv ?? Evaluator.GlobalEnv);
        _b("sx-defined?", PSxDefinedQ);
        _b("sx-defmacro", PSxDefmacro);
        _b("sx-expand-env", args => Evaluator.CurrentExpandEnv ?? Evaluator.GlobalEnv);

        // ── Hash tables ──
        _b("hash-table-clear!", PHashTableClearBang);
        _b("hash-table-contains?", PHashTableContainsQ);
        _b("hash-table-count", PHashTableCount);
        _b("hash-table-delete!", PHashTableDeleteBang);
        _b("hash-table-ref", PHashTableRef);
        _b("hash-table-set!", PHashTableSetBang);
        _b("make-hash-table", args => new Dictionary<object, object?>());

        // ── Time ──
        _b("current-jiffy", args => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _b("current-second", args => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _b("jiffies-per-second", args => 1000L);
        _b("jiffies-per-second", args => (long)1000000);

        // ── Misc ──
        _b("complement", PComplement);
        _b("constantly", PConstantly);
        _b("defined?", PDefinedQ);
        _b("flip", PFlip);
        _b("identity", args => args[0]);
        _b("make-promise", args => new Promise(() => args.Length > 0 ? args[0] : Const.VOID));
        _b("sum", args => args.Select(Convert.ToInt64).Sum());
        _b("void", args => Const.VOID);

        // ── Bitwise ──
        _b("arithmetic-shift", PArithmeticShift);
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
        _b("copy-bit", PCopyBit);
        _b("first-set-bit", PFirstSetBit);
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
