using System.Numerics;
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
        _b("sx-expand-call", args =>
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
        });
        _b("void", args => Const.VOID);
    }
}
