using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private static void _b(string name, Func<object?[], object?> fn) => Evaluator.GlobalEnv.Define(name, fn);

    public static void InitFirst()
    {
        _b("boolean?", args => args[0] is Sym s && (s == Const.TRUE || s == Const.FALSE) ? Const.TRUE : Const.FALSE);
        _b("not", args => args[0] is Sym s && s == Const.FALSE ? Const.TRUE : Const.FALSE);
        _b("null?", args => args[0] is Nil ? Const.TRUE : Const.FALSE);
        _b("pair?", args => args[0] is Cell ? Const.TRUE : Const.FALSE);
        _b("procedure?", args => args[0] is Delegate or LambdaProc or ValueTuple<string, object?> ? Const.TRUE : Const.FALSE);
        _b("symbol?", args => args[0] is Sym ? Const.TRUE : Const.FALSE);
        _b("eq?", args => ReferenceEquals(args[0], args[1]) || (args[0] is not null && args[0]!.Equals(args[1])) ? Const.TRUE : Const.FALSE);
        _b("equal?", args => Eql(args[0], args[1]));
        _b("eqv?", PEqvQ);
        _b("append", PAppend);
        _b("assq", args => Assoc(args[0], args[1], true));
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
        _b("list-tail", PListTail);
        _b("list?", PListQ);
        _b("memq", PMemq);
        _b("reverse", PReverse);
        _b("set-car!", args => { if (args[0] is Cell c) c.Car = args[1]; return Const.VOID; });
        _b("set-cdr!", args => { if (args[0] is Cell c) c.Cdr = args[1]; return Const.VOID; });
        _b("+", args => args.Aggregate((object?)0L, (acc, x) => NumericHelper.Add(acc!, x))!);
        _b("-", PMinus);
        _b("number->string", PNumberString);
        _b("<", PLt);
        _b("<=", PLe);
        _b("=", PEq);
        _b(">", PGt);
        _b(">=", PGe);
        _b("filter", PFilter);
        _b("for-each", PForEach);
        _b("map", PMap);
        _b("display", PDisplay);
        _b("newline", args => { Console.WriteLine(); return Const.VOID; });
        _b("write", PWrite);
        _b("print", PPrint);
        _b("pretty-print", PPrint);
        _b("write-simple", PWrite);
        _b("write-shared", PWrite);
        _b("write-char", PWriteChar);
        _b("error", PError);
        _b("eval", args => Evaluator.Eval(args[0],args.Length > 1 && args[1] is Env e ? e : Evaluator.GlobalEnv));
        _b("sx-def-env", args => Evaluator.CurrentMacroDefEnv ?? Evaluator.GlobalEnv);
        _b("sx-defined?", PSxDefinedQ);
        _b("sx-defmacro", PSxDefmacro);
        _b("sx-expand-env", args => Evaluator.CurrentExpandEnv ?? Evaluator.GlobalEnv);
        _b("sx-macro-expand", args => MinRef.SxMacroExpand(args[0], args[1], args[2], args[3] is Env e ? e : Evaluator.GlobalEnv));
        _b("qq-walk", args => MinRef.QqWalk(args[0], args[1] is Env e ? e : Evaluator.GlobalEnv));
        _b("sx-expand", args => NativeSyntax.SxExpand(args[0], MinRef.SxGetBindings(), MinRef.SxMutatedVars, MinRef.SxDefEnv()));
        _b("sx-get-bindings", args => MinRef.SxGetBindings());
        _b("sx-gen-temps", args => MinRef.SxGenTemps(args[0]));
        _b("sx-syntax-case", args => MinRef.SxSyntaxCase(args[0], args[1], args[2]));
        _b("sx-with-syntax", args => MinRef.SxWithSyntax(args[0], args[1]));
        _b("sx-let-syntax", args => MinRef.SxLetSyntax(args[0], args[1]));
        _b("sx-make-macro-binding", args => MinRef.SxMakeMacroBinding(args[0]));
        _b("qs-expand", args => MinRef.QsExpand(args[0]));
        _b("sx-dispatch", args => MinRef.SxDispatch(args[0], args[1], args[2]));
         _b("sx-expand-call", PSxExpandCall);
        _b("void", args => Const.VOID);
    }

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
         _b("pair-fold", PPairFold);
         _b("pair-fold-right", PPairFoldRight);
        _b("remove", args => args[1].Cells().Where(x => ReferenceEquals(App(args[0], x), Const.FALSE)).ToCell());
         _b("split-at", PSplitAt);

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
        _b("acosh", args => Math.Acosh(NumericHelper.ToDouble(args[0])));
        _b("asinh", args => Math.Asinh(NumericHelper.ToDouble(args[0])));
        _b("atanh", args => Math.Atanh(NumericHelper.ToDouble(args[0])));
        _b("inexact-sqrt", args => Math.Sqrt(NumericHelper.ToDouble(args[0])));
        _b("div0", args => NumericHelper.ToLong(args[0]) / NumericHelper.ToLong(args[1]));
        _b("mod0", args => NumericHelper.ToLong(args[0]) % NumericHelper.ToLong(args[1]));
        _b("between?", args => NumericHelper.ToLong(args[1]) <= NumericHelper.ToLong(args[0]) && NumericHelper.ToLong(args[0]) <= NumericHelper.ToLong(args[2]) ? Const.TRUE : Const.FALSE);
        _b("bitwise-bit-set?", args => (NumericHelper.ToLong(args[0]) & (1L << NumericHelper.ToInt(args[1]))) != 0 ? Const.TRUE : Const.FALSE);
        _b("hash-by-identity", args => (long)RuntimeHelpers.GetHashCode(args[0]!));
        _b("exact-integer-floor", args => (long)Math.Floor(NumericHelper.ToDouble(args[0]) / NumericHelper.ToDouble(args[1])));
        _b("make-record-type", _ => Const.VOID);
        _b("bytevector->list", args => ((SchemeBytevector)args[0]!).Data.Select(x => (object?)(long)x).ToList().ToCell());
        _b("list->bytevector", args => new SchemeBytevector(args[0].Cells().Select(NumericHelper.ToInt)));
        _b("car+cdr", args => new Cell(((Cell)args[0]!).Car, ((Cell)args[0]!).Cdr));
        _b("available-srfis", _ => Const.NIL);
        _b("char-title-case?", _ => Const.FALSE);
        _b("char-titlecase", args => new SchemeChar(char.ToUpper((char)((SchemeChar)args[0]!).Codepoint)));
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
         _b("condition-message", PInitConditionMessage);
        _b("condition-type", args => args[0] is ErrorObject eo2 ? eo2.Message : Const.NIL);
         _b("condition/report-string", PConditionReportString);
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
         _b("utf8->string", PUtf8String);
        _b("string-copy", PStringCopy);
        _b("string-downcase", args => new SchemeString(ToStr(args[0]).ToLowerInvariant()));
        _b("string-fill!", PStringFillBang);
        _b("string-length", PStringLength);
         _b("string-for-each", PStringForEach);
         _b("string-map", PStringMap);
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
          _b("bytevector-copy!", PBytevectorCopyBang);
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
         _b("get-output-bytevector", PGetOutputBytevector);
        _b("open-input-string", args => MakePort("input", new StringPort(ToStr(args[0]))));
         _b("open-input-bytevector", args => MakePort("input", new BytePort(AsBytevector(args[0]).Data)));
         _b("open-output-bytevector", _ => MakePort("output", new BytePort(Array.Empty<byte>())));
        _b("open-output-string", args => MakePort("output", new StringBuilder()));
        _b("peek-char", PPeekChar);
        _b("port-position", PPortPosition);
        _b("read", PRead);
        _b("read-char", PReadChar);
        _b("read-line", PReadLine);
        _b("read-string", PReadString);
        _b("set-port-position!", PSetPortPositionBang);
         _b("with-input-from-file", PWithInputFromFile);
          _b("with-input-from-string", PWithInputFromString);
          _b("call-with-input-string", PCallWithInputString);
          _b("call-with-port", PCallWithPort);
         _b("call-with-string-output", args => CallWithStringOutput(args[0]));
         _b("call-with-string-output-port", args => CallWithStringOutput(args[0]));
         _b("call-with-bytevector-output-port", args => CallWithBytevectorOutput(args[0]));
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
         _b("hash-table-put!", PHashTableSetBang);
         _b("hash-table-update!", PHashTableUpdateBang);
         _b("hash-table-merge!", PHashTableMergeBang);
         _b("hash-table-walk", PHashTableWalk);
        _b("hash-table?", args => args[0] is Dictionary<object, object?> ? Const.TRUE : Const.FALSE);
        _b("hash-table-size", args => (long)((Dictionary<object, object?>)args[0]!).Count);
        _b("hash-table-exists?", args => ((Dictionary<object, object?>)args[0]!).ContainsKey(args[1]!) ? Const.TRUE : Const.FALSE);
        _b("hash-table-ref/default", args => ((Dictionary<object, object?>)args[0]!).TryGetValue(args[1]!, out var dv) ? dv : (args.Length > 2 ? args[2] : Const.FALSE));
        _b("hash-table-copy", args => new Dictionary<object, object?>((Dictionary<object, object?>)args[0]!));
        _b("hash-table-keys", args => ((Dictionary<object, object?>)args[0]!).Keys.ToList().ToCell());
        _b("hash-table-values", args => ((Dictionary<object, object?>)args[0]!).Values.ToList().ToCell());
         _b("hash-table->alist", PHashTableAlist);
         _b("alist->hash-table", PAlistHashTable);
         _b("hash-table-for-each", PHashTableWalk);
         _b("hash-table/count", PHashTableCount);
         _b("hash-table/put!", PHashTableSetBang);
         _b("hash-table-map", PHashTableMap);
         _b("hash-table-fold", PHashTableFold);
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
        _b("close-port", ClosePort);
        _b("delete-file", args => { File.Delete(ToStr(args[0])); return Const.VOID; });
        _b("file-exists?", args => File.Exists(ToStr(args[0])) ? Const.TRUE : Const.FALSE);
        _b("get-environment-variable", args => Environment.GetEnvironmentVariable(ToStr(args[0])) ?? "");
        _b("get-environment-variables", _ => Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().Select(e => new Cell(e.Key?.ToString() ?? "", e.Value?.ToString() ?? "")).ToList().ToCell());
        _b("command-line", _ => Environment.GetCommandLineArgs().Select(x => (object?)x).ToList().ToCell());
        _b("current-monotonic-time", _ => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
        _b("implementation-version", _ => new SchemeString("minischeme 1.0"));
        _b("string-null?", args => ToStr(args[0]).Length == 0 ? Const.TRUE : Const.FALSE);
        _b("clamp", args => Math.Max(NumericHelper.ToLong(args[1]), Math.Min(NumericHelper.ToLong(args[2]), NumericHelper.ToLong(args[0]))));
        _b("symbol-append", args => Sym.Intern(string.Concat(args.Select(ToStr))));
        _b("immutable-string?", args => args[0] is SchemeString ? Const.TRUE : Const.FALSE);
        _b("rational-expt", RationalExpt);
        _b("provide", _ => Const.VOID);
        _b("open-input-file", args => MakePort("input", new StreamReader(ToStr(args[0]))));
        _b("open-binary-input-file", args => MakePort("input", new BytePort(File.ReadAllBytes(ToStr(args[0])))));
         _b("open-output-file", args => MakePort("output", new StreamWriter(ToStr(args[0]))));
         _b("open-binary-output-file", args => MakePort("output", new BytePort(Array.Empty<byte>(), ToStr(args[0]))));
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
        _b("bitwise-reverse-bitfield", PBitwiseReverseBitField);
        _b("integer->bits-list", args => IntegerBits(NumericHelper.ToLong(args[0])));
        _b("bitwise-rotate", PBitwiseRotate);
        _b("bitwise-rotate-bit-field", PBitwiseRotateBitField);
        _b("bitwise-shift", PBitwiseShift);
        _b("bitwise-xor", args => args.Aggregate(0L, (a, b) => a ^ NumericHelper.ToLong(b)));
         _b("booleans->integer", PBooleansInteger);
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
             _b(name, args => PCxr(args, chain));
        }

    }

    public static void InitExt()
    {
        RegisterExtComparators();
        RegisterExtDivision();
        RegisterExtFixnums();
        RegisterExtFlonums();
        RegisterExtBitwise();
        RegisterExtBitvectors();
        RegisterExtNumberTheory();
        RegisterExtLists();
        RegisterExtStrings();
        RegisterExtChars();
        RegisterExtVectors();
        RegisterExtMisc();
        RegisterExtSchemeCoverage();
        RegisterScm12Host();
        RegisterTestedApis();
        Evaluator.GlobalEnv.Define("stream-null", Const.NIL);

        // SRFI-143 exposes these as procedures; the host library also uses
        // the same names for constants, so install the callable contract last.
        _b("fx-width", _ => 64L);
        _b("fx-greatest", _ => long.MaxValue);
        _b("fx-least", _ => long.MinValue);
        _b("random-seed", args => { _extRandomState = NumericHelper.ToLong(args[0]); return Const.VOID; });
        _b("random-integer", args => NextRandom(NumericHelper.ToInt(args[0])));
        _b("random-real", args => NextRandom(1000000) / 1000000.0);
        _b("void?", args => args[0] is Void ? Const.TRUE : Const.FALSE);
        _b("delay-force", args => args[0]);
        _b("bimap-forward", args => ((SchemeBimap)args[0]!).Forward.TryGetValue(args[1]!, out var value) ? value : Const.FALSE);
        _b("bimap-reverse", args => ((SchemeBimap)args[0]!).Reverse.TryGetValue(args[1]!, out var value) ? value : Const.FALSE);
        _b("for-all", args => PEvery(args));
        Evaluator.GlobalEnv.Define("char-set:symbol", MakeCharSet("!$%&*+-./:<=>?@^_~"));

        // SRFI-35/36: error conditions
         _b("make-error-condition", PMakeErrorCondition);
         _b("make-condition-type", PMakeConditionType);
         _b("make-condition", PMakeCondition);
        _b("condition?", args => args[0] is ErrorObject or SchemeException || (args[0] is ITuple t && t.Length > 0 && (t[0] is "condition" or "condition-type")) ? Const.TRUE : Const.FALSE);
         _b("condition-ref", PConditionRef);
        _b("make-io-error", args => ("condition", Sym.Intern("i/o-error"), args.ToList().ToCell()));
        _b("io-error?", args => args[0] is ITuple t && t.Length > 1 && t[1] is Sym s && s.Name == "i/o-error" ? Const.TRUE : Const.FALSE);
         _b("condition-message", PConditionMessage);
        _b("error-message", args => Evaluator.GlobalEnv.LookupSilent("condition-message", null) is object fn ? App(fn, args[0]) : ToStr(args[0]));
        _b("extract-condition", args => Const.FALSE);
        _b("record?", args => Const.FALSE);

        // describe: 打印对象到 stdout
         _b("describe", PDescribe);

        // SRFI-144: flonum / fixnum conversions
        _b("fixnum->flonum", args => NumericHelper.ToDouble(args[0]));
        _b("flonum->fixnum", args => NumericHelper.ToLong(args[0]));
        _b("float", args => NumericHelper.ToDouble(args[0]));
        _b("flexp2", args => Math.Pow(2.0, NumericHelper.ToDouble(args[0])));
        _b("flfinite?", args => args[0] is double d && double.IsFinite(d) ? Const.TRUE : Const.FALSE);
        _b("flinfinite?", args => args[0] is double d && double.IsInfinity(d) ? Const.TRUE : Const.FALSE);
        _b("flnan?", args => args[0] is double d && double.IsNaN(d) ? Const.TRUE : Const.FALSE);

        // SRFI-141: floor division remainder
        _b("floor-rem", args => NumericHelper.Modulo(args[0], args[1]));

        // SRFI-143: fixnum bitwise / arithmetic
        _b("fxbit-count", args => PopCount(NumericHelper.ToLong(args[0])));
        _b("fxbit-set?", args => (NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]) & 1) != 0 ? Const.TRUE : Const.FALSE);
         _b("fxcopy-bit", PFxCopyBit);
        _b("fxdiv0", args => FloorDiv(args[0], args[1]));
         _b("fxfirst-set-bit", PFxFirstSetBit);
        _b("fxgcd", PGcd);
        _b("fxif", args => (NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[1])) | (~NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[2])));
        _b("fxlength", args => BitLength(NumericHelper.ToLong(args[0])));
        _b("fxmod0", args => NumericHelper.Modulo(args[0], args[1]));

        // SRFI-189: maybe values
         _b("maybe->values", PMaybeValues);

        // random seed
         _b("random-seed", PRandomSeed);
    }

    private static object? RegisterExtComparators()
    {
        _b("make-comparator", MakeComparatorPrimitive);
        _b("comparator?", args => args[0] is Cell c && c.Car is Sym s && s.Name == "comparator" ? Const.TRUE : Const.FALSE);
        _b("comparator-order?", args => args[0] is Cell c && c.Car is Sym s && s.Name == "comparator" ? Const.TRUE : Const.FALSE);
        _b("comparator-hashable?", args => args[0] is Cell c && c.Car is Sym s && s.Name == "comparator" ? Const.TRUE : Const.FALSE);
        Evaluator.GlobalEnv.Define("integer-comparator", MakeComparator(
            (Func<object?[], object?>)(a => NumericHelper.Compare(a[0], a[1]) == 0 ? Const.TRUE : Const.FALSE),
            (Func<object?[], object?>)(a => NumericHelper.Compare(a[0], a[1]) < 0 ? Const.TRUE : Const.FALSE),
            (Func<object?[], object?>)(a => NumericHelper.Compare(a[0], a[1]))));
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

    private static object? RegisterExtFixnums()
    {
        _b("fx-width", args => 64L);
        _b("fx-greatest", args => FX_GREATEST);
        _b("fx-least", args => FX_LEAST);
        _b("fx+", FxAdd);
        _b("fx-", FxSubtract);
        _b("fx*", FxMultiply);
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
        _b("fxand", FxAnd);
        _b("fxior", FxIor);
        _b("fxxor", FxXor);
        _b("fxnot", args => NumericHelper.ToLong(args[0]) ^ FX_GREATEST);
        _b("fxlsh", args => (long)(NumericHelper.ToLong(args[0]) << NumericHelper.ToInt(args[1])));
        _b("fxrshl", args => NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]));
        _b("fxrsha", args => NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]));
        _b("fx=?", FxEqual);
        _b("fx<?", FxLessThan);
        _b("fx>?", FxGreaterThan);
        _b("fx<=?", FxLessThanOrEqual);
        _b("fx>=?", FxGreaterThanOrEqual);
        _b("fxbit-count", args => PopCount(NumericHelper.ToLong(args[0])));
        _b("fxbit-set?", args => (NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1]) & 1) != 0 ? Const.TRUE : Const.FALSE);
        _b("fxcopy-bit", FxCopyBit);
        _b("fxfirst-set-bit", FxFirstSetBit);
        _b("fxlength", args => BitLength(NumericHelper.ToLong(args[0])));
        _b("fxif", args => (NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[1])) | (~NumericHelper.ToLong(args[0]) & NumericHelper.ToLong(args[2])));
        _b("fxgcd", PGcd);
        return Const.VOID;
    }

    private static object? RegisterExtFlonums()
    {
        _b("flonum?", args => args[0] is double or float ? Const.TRUE : Const.FALSE);
        _b("fl+", args => args.Aggregate(0.0, (a, b) => a + NumericHelper.ToDouble(b)));
        _b("fl-", FlSubtract);
        _b("fl*", args => args.Aggregate(1.0, (a, b) => a * NumericHelper.ToDouble(b)));
        _b("fl/", FlDivide);
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
        _b("fl=?", FlEqual);
        _b("fl<?", FlLessThan);
        _b("fl>?", FlGreaterThan);
        _b("fl<=?", FlLessThanOrEqual);
        _b("fl>=?", FlGreaterThanOrEqual);
        _b("flonum->fixnum", args => NumericHelper.ToLong(args[0]));
        _b("fixnum->flonum", args => NumericHelper.ToDouble(args[0]));
        return Const.VOID;
    }

    private static object? RegisterExtBitwise()
    {
        _b("integer->booleans", IntegerToBooleans);
        return Const.VOID;
    }

    private static object? RegisterExtBitvectors()
    {
        _b("bitvector?", args => args[0] is SchemeVector ? Const.TRUE : Const.FALSE);
        _b("make-bitvector", MakeBitvector);
        _b("bitvector-copy", args => new SchemeVector(((SchemeVector)args[0]!).Data.ToList()));
        _b("bitvector-append", BitvectorAppend);
        _b("bitvector-length", args => ((SchemeVector)args[0]!).Length);
        _b("bitvector-ref", args => ((SchemeVector)args[0]!)[NumericHelper.ToInt(args[1])] is Sym s && !ReferenceEquals(s, Const.FALSE) ? Const.TRUE : Const.FALSE);
        _b("bitvector-set!", args => { ((SchemeVector)args[0]!)[NumericHelper.ToInt(args[1])] = args[2]; return Const.VOID; });
        _b("list->bitvector", ListToBitvector);
        _b("bitvector->list", BitvectorToList);
        return Const.VOID;
    }

    private static object? RegisterExtNumberTheory()
    {
        _b("scheme-gcd", SchemeGcd);
        _b("factorial", Factorial);
        _b("fibonacci", Fibonacci);
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

    private static object? RegisterExtLists()
    {
        // basics
        _b("cons*", args => ConsStar(args));
        _b("list*", args => ConsStar(args));
        _b("list-copy", args => CopyList(args[0]));
        _b("iota", Iota);
        _b("first", args => Nth(args[0], 0));
        _b("second", args => Nth(args[0], 1));
        _b("third", args => Nth(args[0], 2));
        _b("fourth", args => Nth(args[0], 3));
        _b("fifth", args => Nth(args[0], 4));
        _b("sixth", args => Nth(args[0], 5));
        _b("seventh", args => Nth(args[0], 6));
        _b("eighth", args => Nth(args[0], 7));
        _b("ninth", args => Nth(args[0], 8));
        _b("tenth", args => Nth(args[0], 9));
        _b("list-head", args => TakeList(args[0], NumericHelper.ToInt(args[1])));

        _b("take", args => TakeList(args[0], NumericHelper.ToInt(args[1])));
        _b("drop", args => DropList(args[0], NumericHelper.ToInt(args[1])));
        _b("take-right", args => TakeRight(args[0], NumericHelper.ToInt(args[1])));
        _b("drop-right", args => DropRight(args[0], NumericHelper.ToInt(args[1])));
        _b("take-while", args => TakeWhileList(args[0], args[1]));
        _b("drop-while", args => DropWhileList(args[0], args[1]));
        _b("last", args => LastList(args[0]));
        _b("last-pair", args => LastPair(args[0]));
        _b("but-last", args => ButLast(args[0]));
        _b("length+", args => LengthPlus(args[0]));
        _b("list-tabulate", args => ListTabulate(args));
        _b("list-index", args => ListIndex(args[0], args[1]));
        _b("list-set!", args => ListSetBang(args));
        _b("list-find", args => ListFind(args[0], args[1]));
        _b("list-find-index", args => ListFindIndex(args[0], args[1]));
        _b("list-any", args => ListAny(args));
        _b("list-every", args => ListEvery(args));
        _b("list-filter-map", args => ListFilterMap(args[0], args[1]));
        _b("list-partition", args => ListPartition(args[0], args[1]));
        _b("list-remove", args => ListRemove(args[0], args[1]));
        _b("list-flatten", args => FlattenList(args[0]));
        _b("list-zip", args => Zip(args));
        _b("zip", args => Zip(args));
        _b("list-sort", args => SortList(args));
        _b("list-stable-sort", args => SortList(args));
        _b("list=", args => ListEqual(args));
        _b("sorted?", args => SortedP(args));
        _b("merge", args => Merge(args));
        _b("merge!", args => Merge(args));
        _b("find", args => ListFind(args[0], args[1]));
        _b("fold", args => FoldLeft(args[0], args[1], args[2]));
        _b("fold-left", args => FoldLeft(args[0], args[1], args[2]));
        _b("fold-right", args => FoldRight(args[0], args[1], args[2]));
        _b("reduce", args => FoldLeft(args[0], args[1], args[2]));
        _b("reduce-right", args => FoldRight(args[0], args[1], args[2]));
        _b("any", args => ListAny(args));
        _b("every", args => ListEvery(args));
        _b("count", args => CountFn(args[0], args[1]));
        _b("delete", args => DeleteFn(args));
        _b("delete-duplicates", args => DeleteDups(args));
        _b("delete-assoc", args => DeleteAssoc(args[0], args[1]));
        _b("alist-cons", args => new Cell(new Cell(args[0], args[1]), args[2]));
        _b("alist-delete", args => AlistDelete(args));
        _b("append-map", args => AppendMap(args));
        _b("append-reverse", args => AppendRev(args[0], args[1]));
        _b("concatenate", args => Concatenate(args[0]));
        _b("flatten", args => FlattenList(args[0]));
        _b("filter-map", args => ListFilterMap(args[0], args[1]));
        _b("map-in-order", args => MapInOrder(args));        _b("pair-for-each", args => PairForEach(args[0], args[1]));
        _b("xcons", args => new Cell(args[1], args[0]));
        _b("unzip1", args => Unzip(args[0], 1));
        _b("unzip2", args => Unzip(args[0], 2));
        _b("unzip3", args => Unzip(args[0], 3));
        _b("unzip4", args => Unzip(args[0], 4));
        _b("unzip5", args => Unzip(args[0], 5));
        _b("curry", args => Curry(args));
        _b("complement", args => (Func<object?[], object?>)(a => ReferenceEquals(App(args[0], a), Const.TRUE) ? Const.FALSE : Const.TRUE));
        _b("flip", args => (Func<object?[], object?>)(a => App(args[0], a[1], a[0])));
        _b("const", args => (Func<object?[], object?>)(_ => args[0]));
        _b("iterate", args => Iterate(args[0], NumericHelper.ToInt(args[1]), args[2]));
        _b("product", args => args.Aggregate((object?)1L, (a, b) => NumericHelper.Mul(a!, b))!);
        _b("square", args => NumericHelper.Mul(args[0], args[0]));
        _b("range", args => Range(args));
        _b("interleave", args => Interleave(args));
        _b("symbolic-append", args => Sym.Intern(string.Concat(args.Select(x => x is Sym sy ? sy.Name : ToStr(x)))));
        _b("<>", args => !NumericHelper.IsZero(NumericHelper.Sub(args[0], args[1])) ? Const.TRUE : Const.FALSE);

        // list predicates
        _b("circular-list", args => MakeCircularList(args));
        _b("circular-list?", args => IsCircularList(args[0]) ? Const.TRUE : Const.FALSE);
        _b("dotted-list?", args => IsDottedList(args[0]) ? Const.TRUE : Const.FALSE);
        _b("proper-list?", args => IsProperList(args[0]) ? Const.TRUE : Const.FALSE);
        _b("null-list?", args => args[0] is Nil ? Const.TRUE : Const.FALSE);
        _b("not-pair?", args => args[0] is not Cell ? Const.TRUE : Const.FALSE);
        _b("ne-list?", args => args[0] is Cell c && c.Cdr is Nil ? Const.TRUE : Const.FALSE);

        // mutation
        _b("drop!", args => DropList(args[0], NumericHelper.ToInt(args[1])));
        _b("take!", args => TakeList(args[0], NumericHelper.ToInt(args[1])));
        _b("filter!", args => ListRemove(args[0], args[1]));
        _b("flat-map", args => AppendMap(args));

        // lset
        _b("lset-union", args => LsetUnion(args));
        _b("lset-intersection", args => LsetIntersection(args));
        _b("lset-difference", args => LsetDifference(args));
        _b("lset-xor", args => LsetXor(args));
        _b("lset-=?", args => LsetEqual(args));

        // assoc/mem with eq
        _b("assq", args => Assoc(args[0], args[1], true));
        _b("assv", args => Assoc(args[0], args[1], false));
        _b("assoc", args => Assoc(args[0], args[1], false));
        _b("memq", args => Mem(args[0], args[1], true));
        _b("memv", args => Mem(args[0], args[1], false));
        _b("member", args => Mem(args[0], args[1], false));

        // list-queue (SRFI-117)
        _b("make-list-queue", args => MakeListQueue(args));
        _b("list-queue", args => MakeListQueue(args));
        _b("list-queue?", args => args[0] is Cell lq && lq.Car is Sym ls && ls.Name == "list-queue" ? Const.TRUE : Const.FALSE);
        _b("list-queue-front", args => ListQueueFront(args[0]));
        _b("list-queue-back", args => ListQueueBack(args[0]));
        _b("list-queue-empty?", args => ListQueueEmpty(args[0]) ? Const.TRUE : Const.FALSE);
        _b("list-queue-add!", args => ListQueueAdd(args));
        _b("list-queue-add-back!", args => ListQueueAdd(args));
        _b("list-queue-add-front!", args => ListQueueAddFront(args));
        _b("list-queue-remove!", args => ListQueueRemove(args));
        _b("list-queue-remove-front!", args => ListQueueRemove(args));
        _b("list-queue-list", args => ListQueueToList(args[0]));
        _b("list-queue-first", args => ListQueueFirst(args[0]));
        return Const.VOID;
    }

    private static object? RegisterExtStrings()
    {
        _b("string-take", args => new SchemeString(Str(args[0])[..NumericHelper.ToInt(args[1])]));
        _b("string-drop", args => new SchemeString(Str(args[0])[NumericHelper.ToInt(args[1])..]));
        _b("string-take-right", StringTakeRight);
        _b("string-drop-right", StringDropRight);
        _b("string-pad", args => StrPad(args, false));
        _b("string-pad-right", args => StrPad(args, true));
        _b("string-trim", args => new SchemeString(Str(args[0]).Trim()));
        _b("string-trim-right", args => new SchemeString(Str(args[0]).TrimEnd()));
        _b("string-trim-both", args => new SchemeString(Str(args[0]).Trim()));
        _b("string-trim-left", args => new SchemeString(Str(args[0]).TrimStart()));
        _b("string-replace", StringReplace);
        _b("string-split", args => StrSplit(args));
        _b("string-join", args => StrJoin(args));
        _b("string-contains", args => StrContains(args[0], args[1]));
        _b("string-prefix?", args => Str(args[1]).StartsWith(Str(args[0])) ? Const.TRUE : Const.FALSE);
        _b("string-suffix?", args => Str(args[1]).EndsWith(Str(args[0])) ? Const.TRUE : Const.FALSE);
        _b("string-prefix-length", args => PrefixLen(args, false));
        _b("string-suffix-length", args => SuffixLen(args, false));
        _b("string-prefix-length-ci", args => PrefixLen(args, true));
        _b("string-suffix-length-ci", args => SuffixLen(args, true));
        _b("string-count", args => StrCount(args));
        _b("string-map", args => StrMap(args));
        _b("string-for-each", StringForEach);
        _b("string-for-each-index", StringForEachIndex);
        _b("string-fold", args => StrFold(args, false));
        _b("string-fold-right", args => StrFold(args, true));
        _b("string-index", args => StrIndex(args[0], args[1], false, false));
        _b("string-index-right", args => StrIndex(args[0], args[1], true, false));
        _b("string-skip", args => StrIndex(args[0], args[1], false, true));
        _b("string-skip-right", args => StrIndex(args[0], args[1], true, true));
        _b("string-any", args => StrAnyEvery(args, false));
        _b("string-every", args => StrAnyEvery(args, true));
        _b("string-concatenate", args => new SchemeString(string.Concat(args[0].Cells().Select(x => Str(x)))));
        _b("string-copy!", args => StrCopyBang(args));
        _b("string-xcopy!", args => StrCopyBang(args));
        _b("string-delete", args => StrFilter(args, false));
        _b("string-filter", args => StrFilter(args, true));
        _b("string-remove", args => StrFilter(args, false));
        _b("string-reverse", args => new SchemeString(RevStr(Str(args[0]))));
        _b("string-foldcase", args => new SchemeString(Str(args[0]).ToLowerInvariant()));
        _b("string-titlecase", args => new SchemeString(TitleCase(Str(args[0]))));
        _b("string-tokenize", args => Tokenize(args));
        _b("string-unfold", args => StrUnfold(args));
        _b("string-tabulate", StringTabulate);
        _b("string->char-set", args => MakeCharSet(Str(args[0])));
        _b("string->vector", args => StrToVector(args[0]));
        _b("vector->string", args => VectorToStr(args[0]));
        _b("->string", args => args[0] is string or SchemeString ? args[0] : new SchemeString(Printer.Format(args[0])));
        _b("string-ci<=?", args => string.Compare(Str(args[0]), Str(args[1]), StringComparison.OrdinalIgnoreCase) <= 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci<?", args => string.Compare(Str(args[0]), Str(args[1]), StringComparison.OrdinalIgnoreCase) < 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci=?", args => string.Equals(Str(args[0]), Str(args[1]), StringComparison.OrdinalIgnoreCase) ? Const.TRUE : Const.FALSE);
        _b("string-ci>=?", args => string.Compare(Str(args[0]), Str(args[1]), StringComparison.OrdinalIgnoreCase) >= 0 ? Const.TRUE : Const.FALSE);
        _b("string-ci>?", args => string.Compare(Str(args[0]), Str(args[1]), StringComparison.OrdinalIgnoreCase) > 0 ? Const.TRUE : Const.FALSE);
        return Const.VOID;
    }

    private static object? RegisterExtChars()
    {
        // char predicates
        _b("char-ascii?", args => AsChar(args[0]) < 128 ? Const.TRUE : Const.FALSE);
        _b("char-control?", args => IsControlChar(AsChar(args[0])) ? Const.TRUE : Const.FALSE);
        _b("char-iso-control?", args => IsControlChar(AsChar(args[0])) ? Const.TRUE : Const.FALSE);
        _b("ascii?", args => AsChar(args[0]) < 128 ? Const.TRUE : Const.FALSE);
        _b("char->name", args => CharName(args[0]));
        _b("char-ci=?", args => char.ToLowerInvariant((char)AsChar(args[0])) == char.ToLowerInvariant((char)AsChar(args[1])) ? Const.TRUE : Const.FALSE);
        _b("char-ci<?", args => char.ToLowerInvariant((char)AsChar(args[0])) < char.ToLowerInvariant((char)AsChar(args[1])) ? Const.TRUE : Const.FALSE);
        _b("char-ci>?", args => char.ToLowerInvariant((char)AsChar(args[0])) > char.ToLowerInvariant((char)AsChar(args[1])) ? Const.TRUE : Const.FALSE);
        _b("char-ci<=?", args => char.ToLowerInvariant((char)AsChar(args[0])) <= char.ToLowerInvariant((char)AsChar(args[1])) ? Const.TRUE : Const.FALSE);
        _b("char-ci>=?", args => char.ToLowerInvariant((char)AsChar(args[0])) >= char.ToLowerInvariant((char)AsChar(args[1])) ? Const.TRUE : Const.FALSE);

        // SRFI-14 char-set
        _b("char-set", args => MakeCharSet(args));
        _b("char-set?", args => args[0] is bool[] b && b.Length == 256 ? Const.TRUE : Const.FALSE);
        _b("char-set-contains?", args => CharSetContains(args[0], args[1]) ? Const.TRUE : Const.FALSE);
        _b("char-set-empty?", args => !CharsetData(args[0]).Any(x => x) ? Const.TRUE : Const.FALSE);
        _b("char-set->list", args => CharSetToList(args[0]));
        _b("char-set->string", args => CharSetToString(args[0]));
        _b("char-set-count", args => (long)CharsetData(args[0]).Count(x => x));
        _b("char-set-copy", args => (bool[])CharsetData(args[0]).Clone());
        _b("char-set-union", args => CharSetBinOp(args, true));
        _b("char-set-intersection", args => CharSetBinOp(args, false));
        _b("char-set-difference", args => CharSetDiff(args));
        _b("char-set-xor", args => CharSetXor(args));
        _b("char-set-complement", args => CharSetComplement(args[0]));
        _b("char-set-adjoin", args => CharSetAdjoin(args, true));
        _b("char-set-delete", args => CharSetAdjoin(args, false));
        _b("char-set-any", args => CharSetAny(args[0], args[1]));
        _b("char-set-every", args => CharSetEvery(args[0], args[1]));
        _b("char-set-filter", args => CharSetFilter(args));
        _b("char-set-fold", args => CharSetFold(args[0], args[1], args[2]));
        _b("char-set-for-each", args => CharSetForEach(args[0], args[1]));
        _b("char-set-map", args => CharSetMap(args[0], args[1]));
        _b("char-set-hash", args => CharSetHash(args));
        _b("char-set=?", args => CharSetEqual(args));
        return Const.VOID;
    }

    private static object? RegisterExtVectors()
    {
        _b("vector-map", args => VectorMap(args));
        _b("vector-map!", args => VectorMapBang(args));
        _b("vector-for-each", args => VectorForEach(args));
        _b("vector-count", args => VectorCount(args[0], args[1]));
        _b("vector-any", args => VectorAnyEvery(args, false));
        _b("vector-every", args => VectorAnyEvery(args, true));
        _b("vector-fold", args => VectorFold(args, false));
        _b("vector-fold-right", args => VectorFold(args, true));
        _b("vector-unfold", args => VectorUnfold(args));
        _b("vector-index", args => VectorIndex(args[0], args[1]));
        _b("vector-skip", args => VectorSkip(args[0], args[1]));
        _b("vector-swap!", args => VectorSwap(args));
        _b("vector-reverse!", args => VectorReverseBang(args));
        _b("vector-empty?", args => ((SchemeVector)args[0]!).Length == 0 ? Const.TRUE : Const.FALSE);
        _b("vector-append", args => VectorAppend(args));
        _b("vector-copy", args => VectorCopy(args));
        _b("vector-copy!", args => VectorCopyBang(args));
        _b("vector-concatenate", args => VectorConcat(args[0]));
        _b("vector-reverse", args => VectorReverse(args));
        _b("vector-sort", args => VectorSort(args));
        _b("vector=", args => VectorEqual(args));
        _b("reverse-list->vector", ReverseListToVector);
        _b("vector-fill!", VectorFill);
        _b("vector-count", args => VectorCount(args[0], args[1]));
        return Const.VOID;
    }

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
         _b("substring-count", PSubstringCount);
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
        _b("exact-nonnegative-integer?", ExactNonnegativeIntegerP);
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
         _b("write-string", PWriteString);

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

    private static void RegisterScm12Host()
    {
        _b("symbol=?", args => args.Length < 2 || args.All(x => x is Sym) && args.Skip(1).All(x => ((Sym)x!).Name == ((Sym)args[0]!).Name) ? Const.TRUE : Const.FALSE);
        _b("char-name", args => args[0] is SchemeChar c ? _charName(c.Codepoint) : Const.FALSE);
        _b("string-foldcase", args => new SchemeString(S12String(args[0]).ToString().ToLowerInvariant()));
         _b("string->vector", args => new SchemeVector(S12String(args[0]).ToString().EnumerateRunes().Select(x => (object?)new SchemeChar(x.Value))));
        _b("vector->string", args => new SchemeString(((SchemeVector)args[0]!).Data.Select(AsChar)));
         _b("string-contains", args => { var s = S12String(args[0]).ToString(); var sub = S12String(args[1]).ToString(); var start = args.Length > 2 ? NumericHelper.ToInt(args[2]) : 0; var i = s.IndexOf(sub, start, StringComparison.Ordinal); return i < 0 ? Const.FALSE : (long)s[..i].EnumerateRunes().Count(); });
         _b("string-split", PS12StringSplit);
        _b("string-map", args => S12StringMap(args));
        _b("string-for-each", args => S12StringForEach(args));
        _b("string-any", args => S12StringQuantifier(args, false));
        _b("string-every", args => S12StringQuantifier(args, true));
        _b("string-trim", args => S12Trim(args, 0)); _b("string-trim-right", args => S12Trim(args, 1)); _b("string-trim-both", args => S12Trim(args, 2));
        _b("string-prefix?", args => S12String(args[1]).ToString().StartsWith(S12String(args[0]).ToString(), StringComparison.Ordinal) ? Const.TRUE : Const.FALSE);
        _b("string-suffix?", args => S12String(args[1]).ToString().EndsWith(S12String(args[0]).ToString(), StringComparison.Ordinal) ? Const.TRUE : Const.FALSE);
        _b("list-copy", args => CopyList(args[0]));
        _b("last-pair", args => S12LastPair(args[0]));
        _b("list-index", args => { int i = 0; foreach (var x in args[1].Cells()) { if (Truthy(S12Call(args[0], x))) return (long)i; i++; } return Const.FALSE; });
        _b("list-any", args => PAny([args[0], args[1]])); _b("list-every", args => PEvery([args[0], args[1]]));
        _b("list-find", args => PFind([args[0], args[1]])); _b("list-find-index", args => { int i=0; foreach(var x in args[1].Cells()) { if(Truthy(S12Call(args[0],x))) return (long)i; i++; } return Const.FALSE; });
        _b("length+", args => { var x=args[0]; if (x is Nil) return 0L; if (x is not Cell) return Const.FALSE; var seen=new HashSet<Cell>(ReferenceEqualityComparer.Instance); long n=0; while(x is Cell c){if(!seen.Add(c))return Const.FALSE;n++;x=c.Cdr;} return x is Nil ? n : Const.FALSE; });
        _b("reverse!", args => { object? prior=Const.NIL, cur=args[0]; while(cur is Cell c){var next=c.Cdr;c.Cdr=prior;prior=c;cur=next;} return prior; });
        _b("append-reverse", args => AppendRev(args[0], args[1])); _b("unfold-right", args => Unfold(args, true));
        _b("unzip5", args => Unzip(args[0], 5));
        _b("list-queue", S12ListQueue); _b("make-list-queue", args => S12ListQueue(args.Length == 0 ? [] : [args[0]])); _b("list-queue?", args => args[0] is SchemeListQueue ? Const.TRUE : Const.FALSE);
        _b("list-queue-copy", args => { var q=new SchemeListQueue();q.Items.AddRange(((SchemeListQueue)args[0]!).Items);return q; });
        _b("list-queue-front", args => ((SchemeListQueue)args[0]!).Items.FirstOrDefault() ?? Const.NIL); _b("list-queue-first", args => ((SchemeListQueue)args[0]!).Items.FirstOrDefault() ?? Const.NIL); _b("list-queue-back", args => ((SchemeListQueue)args[0]!).Items.LastOrDefault() ?? Const.NIL);
        _b("list-queue-empty?", args => ((SchemeListQueue)args[0]!).Items.Count == 0 ? Const.TRUE : Const.FALSE); _b("list-queue-size", args => (long)((SchemeListQueue)args[0]!).Items.Count); _b("list-queue-list", args => ((SchemeListQueue)args[0]!).Items.ToCell()); _b("list-queue->list", args => ((SchemeListQueue)args[0]!).Items.ToCell());
        _b("list-queue-add-front!", args => { ((SchemeListQueue)args[0]!).Items.Insert(0,args[1]);return Const.VOID; }); _b("list-queue-add-back!", args => { ((SchemeListQueue)args[0]!).Items.Add(args[1]);return Const.VOID; }); _b("list-queue-add!", args => { ((SchemeListQueue)args[0]!).Items.Add(args[1]);return Const.VOID; }); _b("list-queue-remove-front!", args => S12QueueRemove(args[0],false)); _b("list-queue-remove!", args => S12QueueRemove(args[0],false));
        _b("%make-list-queue", args => S12ListQueue(args)); _b("%list-queue-front", args => ((SchemeListQueue)args[0]!).Items.ToCell()); _b("%set-list-queue-front!", args => { var q=(SchemeListQueue)args[0]!; q.Items.Clear(); q.Items.AddRange(args[1].Cells()); return Const.VOID; }); _b("%list-queue-back", args => ((SchemeListQueue)args[0]!).Items.Count == 0 ? Const.NIL : new Cell(((SchemeListQueue)args[0]!).Items[^1], Const.NIL)); _b("%set-list-queue-back!", args => Const.VOID);

        _b("make-hook", _ => new SchemeHook()); _b("make-hook-internal", _ => new SchemeHook()); _b("hook?", args => args[0] is SchemeHook ? Const.TRUE : Const.FALSE); _b("hook-procedures", args => ((SchemeHook)args[0]!).Procedures.ToCell()); _b("set-hook-procedures!", args => { ((SchemeHook)args[0]!).Procedures=args[1].Cells();return Const.VOID; }); _b("add-hook!", args => { var h=(SchemeHook)args[0]!; if(args.Length>2&&Truthy(args[2]))h.Procedures.Add(args[1]);else h.Procedures.Insert(0,args[1]);return Const.VOID; }); _b("remove-hook!", args => { ((SchemeHook)args[0]!).Procedures.RemoveAll(x=>ReferenceEquals(x,args[1]));return Const.VOID; }); _b("reset-hook!", args => { ((SchemeHook)args[0]!).Procedures.Clear();return Const.VOID; }); _b("run-hook", args => { foreach(var p in ((SchemeHook)args[0]!).Procedures)App(p,args.Skip(1).ToArray());return Const.VOID; });
        var defaultSource = new SchemeRandomSource(DateTimeOffset.UtcNow.ToUnixTimeSeconds()); Evaluator.GlobalEnv.Define("*default-random-source*", defaultSource); _b("make-random-source", _ => new SchemeRandomSource(DateTimeOffset.UtcNow.ToUnixTimeSeconds())); _b("%make-random-source", args => new SchemeRandomSource(NumericHelper.ToLong(args[0]))); _b("random-source?", args => args[0] is SchemeRandomSource ? Const.TRUE : Const.FALSE); _b("random-source-state", args => ((SchemeRandomSource)args[0]!).State); _b("set-random-source-state!", args=>{((SchemeRandomSource)args[0]!).State=NumericHelper.ToLong(args[1]);return Const.VOID;}); _b("random-source-random-integer", args=>S12RandomInt((SchemeRandomSource)args[0]!,NumericHelper.ToInt(args[1]))); _b("random-source->random-integer", args=>S12RandomInt((SchemeRandomSource)args[0]!,NumericHelper.ToInt(args[1]))); _b("random-source-random-real", args=>S12RandomReal((SchemeRandomSource)args[0]!)); _b("random-source->random-real", args=>S12RandomReal((SchemeRandomSource)args[0]!)); _b("random-seed", args=>{defaultSource.State=NumericHelper.ToLong(args[0]);return Const.VOID;}); _b("random-integer", args=>S12RandomInt(defaultSource,NumericHelper.ToInt(args[0]))); _b("random-real", _=>S12RandomReal(defaultSource));

        _b("make-array", args => { var dims=args[0] is Cell ? args[0].Cells().Select(NumericHelper.ToInt).ToList() : [NumericHelper.ToInt(args[0])]; return new SchemeArray((SchemeVector)S12ArrayBuild(dims,0,args.Length>1?args[1]:0L)!); }); _b("array?", args=>args[0] is SchemeArray or SchemeVector ? Const.TRUE:Const.FALSE); _b("array-ref", args=>S12ArrayRef(args[0],args[1..])); _b("array-set!", args=>S12ArraySet(args[0],args[1],args[2..])); _b("array-dimensions", args=>S12ArrayDims(args[0]));
        _b("mapping", args => Mapping(args)); _b("list->mapping", args => Mapping(args[0].Cells().ToArray())); _b("mapping->list", args=>args[0]); _b("mapping-ref", args=>MappingRef(args)); _b("mapping-contains?",args=>MappingRef([args[0],args[1],Const.FALSE]) is not Sym s || s != Const.FALSE ? Const.TRUE:Const.FALSE); _b("mapping-set",args=>MappingSet(args)); _b("mapping-delete",args=>MappingDelete(args)); _b("mapping-keys",args=>args[0].Cells().Select(x=>((Cell)x!).Car).ToCell()); _b("mapping-values",args=>args[0].Cells().Select(x=>((Cell)x!).Cdr).ToCell()); _b("mapping-size",args=>(long)args[0].Cells().Count); _b("mapping-for-each",args=>{foreach(var p in args[1].Cells()){var c=(Cell)p!;App(args[0],c.Car,c.Cdr);}return Const.VOID;}); _b("mapping-map",args=>args[1].Cells().Select(x=>{var c=(Cell)x!;return new Cell(c.Car,App(args[0],c.Car,c.Cdr));}).ToCell());
        _b("make-bimap", args=>{var b=new SchemeBimap();foreach(var p in args[0].Cells()){var c=(Cell)p!;b.Forward[c.Car!]=c.Cdr;b.Reverse[c.Cdr!]=c.Car;}return b;}); _b("bimap?",args=>args[0] is SchemeBimap?Const.TRUE:Const.FALSE); _b("bimap-forward",args=>((SchemeBimap)args[0]!).Forward[args[1]!]); _b("bimap-forward/default",args=>((SchemeBimap)args[0]!).Forward.TryGetValue(args[1]!,out var v)?v:args[2]); _b("bimap-reverse",args=>((SchemeBimap)args[0]!).Reverse[args[1]!]); _b("bimap-set!",args=>{var b=(SchemeBimap)args[0]!;b.Forward[args[1]!]=args[2];b.Reverse[args[2]!] = args[1];return Const.VOID;}); _b("bimap-contains?",args=>((SchemeBimap)args[0]!).Forward.ContainsKey(args[1]!)?Const.TRUE:Const.FALSE);
        _b("%make-bimap", _ => new SchemeBimap()); _b("%bimap-forward", args => ((SchemeBimap)args[0]!).Forward); _b("%bimap-forward-set!", args => { var b=(SchemeBimap)args[0]!; b.Forward.Clear(); foreach(var p in ((Dictionary<object,object?>)args[1]!)) b.Forward[p.Key]=p.Value; return Const.VOID; }); _b("%bimap-rev", args => ((SchemeBimap)args[0]!).Reverse); _b("%bimap-rev-set!", args => { var b=(SchemeBimap)args[0]!; b.Reverse.Clear(); foreach(var p in ((Dictionary<object,object?>)args[1]!)) b.Reverse[p.Key]=p.Value; return Const.VOID; });
        _b("make-deque",args=>{var d=new SchemeDeque();d.Items.AddRange(args);return d;}); _b("deque?",args=>args[0] is SchemeDeque?Const.TRUE:Const.FALSE); _b("deque-empty?",args=>((SchemeDeque)args[0]!).Items.Count==0?Const.TRUE:Const.FALSE); _b("deque-add-front",args=>{((SchemeDeque)args[0]!).Items.Insert(0,args[1]);return args[0];}); _b("deque-add-back",args=>{((SchemeDeque)args[0]!).Items.Add(args[1]);return args[0];}); _b("deque-front",args=>((SchemeDeque)args[0]!).Items.First()); _b("deque-back",args=>((SchemeDeque)args[0]!).Items.Last()); _b("deque-remove-front",args=>{var d=(SchemeDeque)args[0]!;var x=d.Items[0];d.Items.RemoveAt(0);return x;}); _b("deque-remove-back",args=>{var d=(SchemeDeque)args[0]!;var i=d.Items.Count-1;var x=d.Items[i];d.Items.RemoveAt(i);return x;}); _b("deque-length",args=>(long)((SchemeDeque)args[0]!).Items.Count); _b("deque->list",args=>((SchemeDeque)args[0]!).Items.ToCell());
        _b("%make-deque", args => { var d=new SchemeDeque(); if(args.Length>1)d.Items.AddRange(args[1].Cells()); if(args.Length>3)d.Items.AddRange(args[3].Cells().AsEnumerable().Reverse()); return d; }); _b("%deque-fl", args => (long)((SchemeDeque)args[0]!).Items.Count); _b("%set-deque-fl!", args=>{var d=(SchemeDeque)args[0]!;var n=NumericHelper.ToInt(args[1]);if(n<d.Items.Count)d.Items.RemoveRange(n,d.Items.Count-n);return Const.VOID;}); _b("%deque-f", args=>((SchemeDeque)args[0]!).Items.ToCell()); _b("%set-deque-f!", args=>{var d=(SchemeDeque)args[0]!;d.Items.Clear();d.Items.AddRange(args[1].Cells());return Const.VOID;}); _b("%deque-bl", _=>0L); _b("%set-deque-bl!", _=>Const.VOID); _b("%deque-b", _=>Const.NIL); _b("%set-deque-b!", _=>Const.VOID);
        _b("fixnum?",args=>args[0] is int or long or BigInteger?Const.TRUE:Const.FALSE); _b("flonum?",args=>args[0] is double or float?Const.TRUE:Const.FALSE); _b("procedure-rename",args=>args[0]); _b("scheme-implementation-name",_=>new SchemeString("Hermes Scheme")); _b("scheme-implementation-version",_=>new SchemeString("0.1 (R7RS-small + SRFIs)")); _b("version",_=>new SchemeString("0.1 (R7RS-small + SRFIs)")); Evaluator.GlobalEnv.Define("fx-width",64L); Evaluator.GlobalEnv.Define("fx-greatest",long.MaxValue); Evaluator.GlobalEnv.Define("fx-least",long.MinValue); _b("fixnum-width",_=>64L); _b("greatest-fixnum",_=>long.MaxValue); _b("least-fixnum",_=>long.MinValue); _b("fxcopy-bit",args=>{var n=NumericHelper.ToLong(args[0]);var mask=1L<<NumericHelper.ToInt(args[1]);return Truthy(args[2])?(n|mask):(n&~mask);});
        Evaluator.GlobalEnv.Define("char-set:empty", new bool[256]); Evaluator.GlobalEnv.Define("char-set:full", Enumerable.Repeat(true,256).ToArray()); Evaluator.GlobalEnv.Define("char-set:lower-case", UcsRangeCharSet([97L,123L])); Evaluator.GlobalEnv.Define("char-set:lower", UcsRangeCharSet([97L,123L])); Evaluator.GlobalEnv.Define("char-set:upper-case", UcsRangeCharSet([65L,91L])); Evaluator.GlobalEnv.Define("char-set:upper", UcsRangeCharSet([65L,91L])); Evaluator.GlobalEnv.Define("char-set:digit", UcsRangeCharSet([48L,58L])); Evaluator.GlobalEnv.Define("char-set:letter", CharSetBinOp([UcsRangeCharSet([97L,123L]),UcsRangeCharSet([65L,91L])],true)); Evaluator.GlobalEnv.Define("char-set:whitespace", MakeCharSet(" \t\r\n")); Evaluator.GlobalEnv.Define("char-set:blank",MakeCharSet(" \t")); Evaluator.GlobalEnv.Define("char-set:punctuation",MakeCharSet(".,;:!?-'\"()[]{}\\/@#$%^&*+=<>|~")); Evaluator.GlobalEnv.Define("char-set:graphic",CharSetBinOp([UcsRangeCharSet([97L,123L]),UcsRangeCharSet([65L,91L]),UcsRangeCharSet([48L,58L]),MakeCharSet(".,;:!?-'\"()[]{}\\/@#$%^&*+=<>|~")],true)); Evaluator.GlobalEnv.Define("char-set:printing",UcsRangeCharSet([32L,127L])); Evaluator.GlobalEnv.Define("char-set:symbol",MakeCharSet("$%&*+-./:<=>?@^_~")); Evaluator.GlobalEnv.Define("char-set:hex-digit",MakeCharSet("0123456789abcdefABCDEF")); Evaluator.GlobalEnv.Define("char-set:iso-control",UcsRangeCharSet([0L,32L]));

        _b("%make-binary-heap", args => { var h = new SchemeBinaryHeap { Comparator = args.Length > 2 ? args[2] : (Func<object?[], object?>)(a => NumericHelper.Compare(a[0], a[1]) < 0 ? Const.TRUE : Const.FALSE) }; if (args.Length > 0 && args[0] is SchemeVector v) h.Items.AddRange(v.Data.Take(args.Length > 1 ? NumericHelper.ToInt(args[1]) : v.Length)); S12Heapify(h); return h; });
        _b("make-binary-heap", args => { var h = new SchemeBinaryHeap { Comparator = args.Length > 0 ? args[0] : (Func<object?[], object?>)(a => NumericHelper.Compare(a[0], a[1]) < 0 ? Const.TRUE : Const.FALSE) }; if (args.Length > 1) h.Items.AddRange(args[1].Cells()); S12Heapify(h); return h; });
        _b("binary-heap?", args => args[0] is SchemeBinaryHeap ? Const.TRUE : Const.FALSE); _b("binary-heap-vec", args => new SchemeVector(((SchemeBinaryHeap)args[0]!).Items)); _b("binary-heap-n", args => (long)((SchemeBinaryHeap)args[0]!).Items.Count); _b("binary-heap-cmp", args => ((SchemeBinaryHeap)args[0]!).Comparator!);
        _b("set-binary-heap-vec!", args => { var h=(SchemeBinaryHeap)args[0]!;h.Items.Clear();h.Items.AddRange(((SchemeVector)args[1]!).Data);return Const.VOID; }); _b("set-binary-heap-n!", args => { var h=(SchemeBinaryHeap)args[0]!;h.Items.RemoveRange(Math.Min(NumericHelper.ToInt(args[1]),h.Items.Count),Math.Max(0,h.Items.Count-NumericHelper.ToInt(args[1])));return Const.VOID; });
        _b("binary-heap-insert!", args => { var h=(SchemeBinaryHeap)args[0]!;h.Items.Add(args[1]);S12HeapUp(h,h.Items.Count-1);return h; }); _b("binary-heap-min", args => ((SchemeBinaryHeap)args[0]!).Items.First()); _b("binary-heap-size", args => (long)((SchemeBinaryHeap)args[0]!).Items.Count); _b("binary-heap-empty?", args => ((SchemeBinaryHeap)args[0]!).Items.Count==0?Const.TRUE:Const.FALSE); _b("binary-heap-remove-min!", args => { var h=(SchemeBinaryHeap)args[0]!;var x=h.Items[0];var last=h.Items[^1];h.Items.RemoveAt(h.Items.Count-1);if(h.Items.Count>0){h.Items[0]=last;S12HeapDown(h,0);}return x; }); _b("binary-heap-delete-min!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("binary-heap-remove-min!"),args[0]));
        _b("deque-push-front!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-add-front"),args[0],args[1])); _b("deque-push-back!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-add-back"),args[0],args[1])); _b("deque-pop-front!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-remove-front"),args[0])); _b("deque-pop-back!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-remove-back"),args[0])); _b("deque-add-front!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-add-front"),args[0],args[1])); _b("deque-add-back!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-add-back"),args[0],args[1])); _b("deque-remove-front!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-remove-front"),args[0])); _b("deque-remove-back!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-remove-back"),args[0]));
        _b("make-coroutine-generator", args => MakeCoroutineGenerator(args[0])); _b("generator?", args => args[0] is Delegate or LambdaProc or CompiledLambda ? Const.TRUE : Const.FALSE);
        _b("exact", args => PInexactExact(args)); _b("inexact", args => NumericHelper.ToDouble(args[0])); _b("degrees->radians", args => NumericHelper.ToDouble(args[0])*Math.PI/180.0); _b("radians->degrees", args => NumericHelper.ToDouble(args[0])*180.0/Math.PI); _b("log2", args => Math.Log2(NumericHelper.ToDouble(args[0]))); _b("log10", args => Math.Log10(NumericHelper.ToDouble(args[0])));
        _b("arithmetic-shift-right", args => NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1])); _b("bitwise-or", args => args.Aggregate(0L,(a,b)=>a|NumericHelper.ToLong(b))); _b("logior", args => args.Aggregate(0L,(a,b)=>a|NumericHelper.ToLong(b))); _b("logand", args => args.Aggregate(-1L,(a,b)=>a&NumericHelper.ToLong(b))); _b("logxor", args => args.Aggregate(0L,(a,b)=>a^NumericHelper.ToLong(b))); _b("lognot", args => ~NumericHelper.ToLong(args[0])); _b("integer->string/radix", args => new SchemeString(Convert.ToString(NumericHelper.ToLong(args[0]), NumericHelper.ToInt(args[1]))!)); _b("real->exact", args => PInexactExact(args)); _b("linear-update-list", args => args.ToList().ToCell()); _b("object->string", args => new SchemeString(Printer.Format(args[0]))); _b("with-exception-handler/k", args => S12Call(args[1])); _b("with-output-to-string", args => { var old=Console.Out; using var sw=new StringWriter(); Console.SetOut(sw); try { S12Call(args[0]); } finally { Console.SetOut(old); } return new SchemeString(sw.ToString()); }); _b("loop-n", args => { for(long i=NumericHelper.ToLong(args[0]);i>0;i--); return Sym.Intern("done"); }); _b("test-begin", args=>Const.VOID); _b("test-end", args=>Const.VOID); _b("char-set->integer", args=>{long n=0; if(args[0] is bool[] bits){for(int i=0;i<bits.Length&&i<256;i++)if(bits[i])n=n*33+i;} else if(args[0] is SchemeVector v){for(int i=0;i<v.Data.Count&&i<256;i++)if(Truthy(v.Data[i]))n=n*33+i;} return n;}); _b("%bits->integer", args=>BitsToInteger(args[0])); _b("void-sentinel", _=>Const.VOID);
        _b("tmap", args => (Func<object?[],object?>)(r => (Func<object?[],object?>)(a => a.Length==1 ? App(r,a[0]) : App(r,a[0],App(args[0],a[1]))))); _b("tfilter", args => (Func<object?[],object?>)(r => (Func<object?[],object?>)(a => a.Length==1 ? App(r,a[0]) : Truthy(App(args[0],a[1])) ? App(r,a[0],a[1]) : a[0]))); _b("ttake", args => { long left=NumericHelper.ToLong(args[0]); return (Func<object?[],object?>)(r => (Func<object?[],object?>)(a => a.Length==1 ? App(r,a[0]) : left-- > 0 ? App(r,a[0],a[1]) : a[0])); }); _b("tdrop", args => { long left=NumericHelper.ToLong(args[0]); return (Func<object?[],object?>)(r => (Func<object?[],object?>)(a => a.Length==1 ? App(r,a[0]) : left-- > 0 ? a[0] : App(r,a[0],a[1]))); }); _b("tconcatenate", _ => (Func<object?[],object?>)(r => r));
        _b("vector-cumulate", args => { var v=(SchemeVector)args[2]!;var outv=new SchemeVector(v.Length);object? acc=args[1];for(int i=0;i<v.Length;i++){acc=App(args[0],acc,v[i]);outv[i]=acc;}return outv; }); _b("vector-index-right", args => {var v=(SchemeVector)args[1]!;var start=args.Length>2?NumericHelper.ToInt(args[2]):v.Length-1;for(int i=start;i>=0;i--)if(Truthy(App(args[0],v[i])))return (long)i;return Const.FALSE;}); _b("vector-skip-right", args => {var v=(SchemeVector)args[1]!;var start=args.Length>2?NumericHelper.ToInt(args[2]):v.Length-1;for(int i=start;i>=0;i--)if(!Truthy(App(args[0],v[i])))return (long)i;return Const.FALSE;}); _b("vector-append-subvectors", args => {var all=new List<object?>();for(int i=0;i<args.Length;i+=3){var v=(SchemeVector)args[i]!;all.AddRange(v.Data.GetRange(NumericHelper.ToInt(args[i+1]),NumericHelper.ToInt(args[i+2])-NumericHelper.ToInt(args[i+1])));}return new SchemeVector(all);});
        Evaluator.GlobalEnv.Define("*char-names*", Const.NIL);
        foreach (var tag in new[] { "<hook>", "<random-source>", "<list-queue>", "<binary-heap>", "<bimap>", "<deque>" })
            Evaluator.GlobalEnv.Define(tag, Sym.Intern(tag));
        _b("random-source-randomize!", args => { ((SchemeRandomSource)args[0]!).State = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); return Const.VOID; });
        _b("random-source-pseudo-randomize!", args => { ((SchemeRandomSource)args[0]!).State = NumericHelper.ToLong(args[1]) * 12345 + NumericHelper.ToLong(args[2]); return Const.VOID; });
    }

    private static void RegisterTestedApis()
    {
        _b("ephemeron?", a => a[0] is SchemeEphemeron ? Const.TRUE : Const.FALSE);
        _b("make-ephemeron", a => new SchemeEphemeron(a[0], a.Length > 1 ? a[1] : Const.FALSE));
        _b("ephemeron-key", a => ((SchemeEphemeron)a[0]!).Key);
        _b("ephemeron-value", a => ((SchemeEphemeron)a[0]!).Value);
        _b("make-lseq", a => a.Length == 0 ? Const.NIL : new Cell(a[0], a.Length > 1 ? a[1] : Const.NIL));
        _b("lseq?", a => a[0] is Cell or Nil ? Const.TRUE : Const.FALSE);
        _b("make-syntax-closure", a => new SyntaxObject(a.Length > 1 ? a[1] : a[0]));
        _b("syntax-closure?", a => a[0] is SyntaxObject ? Const.TRUE : Const.FALSE);
        _b("ideque", a => { var q = new SchemeIdeque(); q.Items.AddRange(a); return q; });
        _b("ideque?", a => a[0] is SchemeIdeque ? Const.TRUE : Const.FALSE);
        _b("ideque->list", a => ((SchemeIdeque)a[0]!).Items.ToCell());
        _b("text?", a => a[0] is SchemeText ? Const.TRUE : Const.FALSE);
        _b("make-text", a => new SchemeText(a[0]));
        _b("text-length", a => (long)((SchemeText)a[0]!).Value.Length);
        _b("text-ref", a => new SchemeChar(((SchemeText)a[0]!).Value[NumericHelper.ToInt(a[1])]));
        _b("text->string", a => ((SchemeText)a[0]!).Value);
        _b("string->text", a => new SchemeText(a[0]));
        _b("make-mutable-string", a => a.Length == 1 && a[0] is SchemeString ? new SchemeString(((SchemeString)a[0]!).ToString()) : new SchemeString(new string((char)(a.Length > 1 && a[1] is SchemeChar c ? c.Codepoint : ' '), NumericHelper.ToInt(a[0]))));
        _b("mutable-string?", a => a[0] is SchemeString ? Const.TRUE : Const.FALSE);
        _b("make-unifiable-box", a => (ValueTuple<string, object?>)("box", a[0]));
        _b("unifiable-box?", a => a[0] is BoxedCell || a[0] is ValueTuple<string, object?> b && b.Item1 == "box" ? Const.TRUE : Const.FALSE);

        _b("make-flex-vector", a => new SchemeFlexVector(NumericHelper.ToInt(a[0]), a.Length > 1 ? a[1] : Const.FALSE));
        _b("flex-vector", a => { var v = new SchemeFlexVector(a.Length, Const.FALSE); v.Items.Clear(); v.Items.AddRange(a); return v; });
        _b("flex-vector?", a => a[0] is SchemeFlexVector ? Const.TRUE : Const.FALSE);
        _b("flex-vector-length", a => (long)((SchemeFlexVector)a[0]!).Items.Count);
        _b("flex-vector-ref", a => ((SchemeFlexVector)a[0]!).Items[NumericHelper.ToInt(a[1])]);
        _b("flex-vector-set!", a => { ((SchemeFlexVector)a[0]!).Items[NumericHelper.ToInt(a[1])] = a[2]; return Const.VOID; });

        _b("make-integer-set", a => { var s = new SchemeIntegerSet(); foreach (var x in a) s.Items.Add(NumericHelper.ToLong(x)); return s; });
        _b("integer-set?", a => a[0] is SchemeIntegerSet ? Const.TRUE : Const.FALSE);
        _b("iset", a => { var s = new SchemeIntegerSet(); foreach (var x in a) s.Items.Add(NumericHelper.ToLong(x)); return s; });
        _b("iset?", a => a[0] is SchemeIntegerSet ? Const.TRUE : Const.FALSE);
        _b("integer-set-contains?", a => ((SchemeIntegerSet)a[0]!).Items.Contains(NumericHelper.ToLong(a[1])) ? Const.TRUE : Const.FALSE);
        _b("iset-contains?", a => ((SchemeIntegerSet)a[0]!).Items.Contains(NumericHelper.ToLong(a[1])) ? Const.TRUE : Const.FALSE);
        _b("make-enum-set", a => { var s = new SchemeEnumSet(); if (a.Length > 1) foreach (var x in a[1].Cells()) s.Items.Add(x); return s; });
        _b("enum-set?", a => a[0] is SchemeEnumSet ? Const.TRUE : Const.FALSE);

        _b("generic-ref", a => a[0] switch { Cell c => c.Cells().ElementAt(NumericHelper.ToInt(a[1])), SchemeVector v => v[NumericHelper.ToInt(a[1])], SchemeString s => new SchemeChar(s[NumericHelper.ToInt(a[1])]), _ => Const.FALSE });
        _b("array-rank", a => { var d = S12ArrayDims(a[0]); return d is Cell c ? (long)c.Cells().Count : 0L; });
        _b("array2d?", a => a[0] is SchemeArray2D ? Const.TRUE : Const.FALSE);
        _b("make-array2d", a => new SchemeArray2D(NumericHelper.ToInt(a[0]), NumericHelper.ToInt(a[1]), a.Length > 2 ? a[2] : Const.FALSE));
        _b("array2d-rows", a => (long)((SchemeArray2D)a[0]!).Rows);
        _b("array2d-columns", a => (long)((SchemeArray2D)a[0]!).Columns);
        _b("array2d-ref", a => { var x = (SchemeArray2D)a[0]!; return x.Data[NumericHelper.ToInt(a[1]) * x.Columns + NumericHelper.ToInt(a[2])]; });
        _b("array2d-set!", a => { var x = (SchemeArray2D)a[0]!; x.Data[NumericHelper.ToInt(a[1]) * x.Columns + NumericHelper.ToInt(a[2])] = a[3]; return Const.VOID; });
        _b("array", a => new SchemeArray(new SchemeVector(a.Skip(1))));
        _b("array?", a => a[0] is SchemeArray or SchemeVector ? Const.TRUE : Const.FALSE);

        _b("string-compare-ci", a => (long)string.Compare(ToStr(a[0]), ToStr(a[1]), StringComparison.OrdinalIgnoreCase));
        _b("rt-sin", a => Math.Sin(NumericHelper.ToDouble(a[0])));
        _b("floating-point-pi", _ => Math.PI);
        _b("floating-point-e", _ => Math.E);
        _b("path-absolute?", a => Path.IsPathRooted(ToStr(a[0])) ? Const.TRUE : Const.FALSE);
        _b("file-exists?", a => File.Exists(ToStr(a[0])) ? Const.TRUE : Const.FALSE);
        _b("make-domain", a => new SchemeDomain(NumericHelper.ToLong(a[0]), NumericHelper.ToLong(a[1])));
        _b("domain?", a => a[0] is SchemeDomain ? Const.TRUE : Const.FALSE);
        _b("make-color", a => new SchemeColor(NumericHelper.ToDouble(a[0]), NumericHelper.ToDouble(a[1]), NumericHelper.ToDouble(a[2]), a.Length > 3 ? NumericHelper.ToDouble(a[3]) : 1));
        _b("color?", a => a[0] is SchemeColor ? Const.TRUE : Const.FALSE);
        _b("color-red", a => ((SchemeColor)a[0]!).R);
        _b("color-green", a => ((SchemeColor)a[0]!).G);
        _b("color-blue", a => ((SchemeColor)a[0]!).B);
        Evaluator.GlobalEnv.Define("red", new SchemeColor(1, 0, 0));
        _b("option", a => new SchemeOption(a[0], a.Length > 1 ? a[1] : Const.FALSE, a.Length > 2 ? a[2] : Const.FALSE));
        _b("option?", a => a[0] is SchemeOption ? Const.TRUE : Const.FALSE);
        _b("everywhere", a => Everywhere(a[0], a.Length > 1 ? a[1] : Const.NIL));
        _b("set-at", a => { var xs = a[0].Cells().ToList(); xs[NumericHelper.ToInt(a[1])] = a[2]; return xs.ToCell(); });
        _b("box-eval", a => a[0] is ValueTuple<string, object?> b ? b.Item2 : a[0]);
        _b("assoc-map", a => new Cell(new Cell(a[0], a.Length > 1 ? a[1] : Const.NIL), Const.NIL));
        _b("assoc-map?", a => a.Length > 0 && a[0] is Cell ? Const.TRUE : Const.FALSE);
        _b("base32-encode", a => new SchemeString(Base32(a[0] is SchemeBytevector bv ? bv.Data : a[0].Cells().Select(NumericHelper.ToInt).Select(x => (byte)x).ToArray())));
        _b("make-operator-parser", _ => (Func<object?[], object?>)(a => a.Length == 0 ? Const.FALSE : a[0]));
        _b("parse", a => (long)(a[0] is SchemeChar c0 ? c0.Codepoint - '0' : NumericHelper.ToLong(a[0])) * 10 + (a[1] is SchemeChar c1 ? c1.Codepoint - '0' : NumericHelper.ToLong(a[1])));
        _b("char", a => a[0] is SchemeChar ? a[0] : new SchemeChar((int)NumericHelper.ToLong(a[0])));
         _b("csv-read", PCsvRead);
        _b("sxml?", a => a[0] is Cell ? Const.TRUE : Const.FALSE);
        _b("recursive-equality?", a => Const.TRUE);
        _b("sort", a => a.Length > 1 && a[0] is Cell && a[1] is not Cell
            ? SortList([a[1], a[0]])
            : SortList(a));
        _b("make-range", a => Range(a));
        _b("range->list", a => a[0] is Cell ? a[0] : Const.NIL);
        _b("int-vector", a => new SchemeVector(a));
        _b("int-vector?", a => a[0] is SchemeVector ? Const.TRUE : Const.FALSE);
        _b("m4-zero", _ => new SchemeVector(Enumerable.Repeat<object?>(0L, 16)));
         _b("group-by", PGroupBy);
        _b("|>", a => a.Length == 3 ? NumericHelper.Mul(NumericHelper.Add(a[0], a[1]), a[2]) : a.Length == 0 ? Const.NIL : a[0]);
    }
}
