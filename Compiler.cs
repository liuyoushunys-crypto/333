using System.Linq.Expressions;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Miniscm.Eval;
using Miniscm.Reader;
using Miniscm.Types;
using Void = Miniscm.Types.Void;
namespace Miniscm.Compiler;

class CacheEntry
{
    public int Version { get; set; }
    public string? Hash { get; set; }
    public string? Name { get; set; }
    public List<string>? Body { get; set; }
}
public static class Compiler
{
    static readonly HashSet<string> SkipJitNames =
    [
        "flip",
        "complement",
        "const",
        "identity",
        "check",
        "test",
        "t-eq"
        // "qq-append-lists",
        // "qq-build-list",
        // "qq-process-el",
        // "qq-reverse",
        // "qq-reverse-helper",
        // "qq-tail-unquote?",
        // "qq-tail-unsplice?",
        // "qq-unquote?",
        // "qq-unsplice?",
        // "qq-walk",
        // "qq-walk-list",
        // "qq-walk-list-helper",
        // "qq-walk-vector",
        // "qq-walk-vector-helper",
        // "sx-ellipsis-vars",
        // "sx-match",
        // "sx-expand",
        // "sx-pattern-vars",
        // "sx-pattern-vars-loop",
        // "sx-dispatch",
        // "sx-match-pair",
        // "sx-match-sym",
        // "sx-match-ellipsis",
        // "sx-match-ellipsis-finish",
        // "sx-match-ellipsis-loop",
        // "sx-expand-pair",
        // "sx-expand-ellipsis",
        // "sx-expand-ellipsis-novar",
        // "sx-expand-ellipsis-var",
        // "sx-sub-bindings",
        // "sx-sub-bindings-cons",
        // "sx-accum-ellipsis",
        // "sx-ellipsis-vars-helper",
        // "sx-find-list-count",
        // "sx-repeat",
        // "sx-repeat-helper",
        // "sx-lookup",
        // "sx-merge-bindings",
        // "sx-rule-tmpl",
        // "sx-rev-append",
        // "sx-reverse",
        // "sx-set-bindings!",
        // "sx-get-bindings",
        // "sx-with-bindings",
        // "sx-with-syntax",
        // "sx-make-macro-binding",
    ];
    static bool ShouldJit(LambdaProc lp)
    {
        var name = lp.Name;
        if (name is null) return false;
        if (SkipJitNames.Contains(name)) return false;
        return true;
    }
    static string SafeFileName(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name)
        {
            if (ch == '?' || ch == '!' || ch == '<' || ch == '>' || ch == '=' ||
                ch == '*' || ch == '|' || ch == ':' || ch == '"' || ch == '/' || ch == '\\')
                sb.Append($"_{(int)ch:x2}");
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }

    // 内容 hash: 用 body 源码的 SHA256 前 16 位, 保证不同内容不同文件, 同名不覆盖
    static string BodyHash(string bodySrc)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(bodySrc));
        return Convert.ToHexString(bytes)[..16];
    }

    // Bump to invalidate cached expansions whose semantics changed
    // (e.g. quasiquote was previously expanded at compile time with a
    // wrong env, producing stale literals).
    const int CacheVersion = 2;

    // quasiquote depends on the runtime env (unquote), so it cannot be
    // pre-expanded at JIT compile time. If a lambda body contains one,
    // skip JIT compilation and let the interpreter expand it.
    static bool HasQuasiquote(object? expr)
    {
        while (expr is SyntaxObject so) expr = so.Expr;
        if (expr is Cell c)
        {
            if (c.Car is Sym s && s.Name == "quasiquote") return true;
            return HasQuasiquote(c.Car) || HasQuasiquote(c.Cdr);
        }
        return false;
    }
    public static CompiledLambda? CompileLambdaProc(LambdaProc lp)
    {
        if (!ShouldJit(lp)) return null;
        try
        {
            // Step 1: Macro-expand body (with cache)
            var bodyForms = new List<object?>();
            var cur = lp.Body;
            if (lp.Name is not null)
            {
                var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), ".mscm_cache");
                var bodySrc = Printer.Format(lp.Body);
                // 用内容 hash 命名缓存文件, 避免同名函数(不同内容)互相覆盖
                var cacheFile = Path.Combine(cacheDir, SafeFileName(lp.Name) + "_" + BodyHash(bodySrc) + ".json");
                if (File.Exists(cacheFile))
                {
                    try
                    {
                        var json = File.ReadAllText(cacheFile);
                        var entry = JsonSerializer.Deserialize<CacheEntry>(json);
                        if (entry?.Version == CacheVersion && entry.Hash == bodySrc && entry.Body is not null)
                        {
                            foreach (var s in entry.Body)
                                bodyForms.Add(Parser.Read(s));
                            if (bodyForms.Count > 0)
                                goto afterExpand;
                        }
                    }
                    catch { }
                    bodyForms.Clear();
                }
                while (cur is Cell c)
                {
                    var expanded = Evaluator.MacroExpand(c.Car, lp.ClosureEnv);
                    if (HasQuasiquote(expanded))
                        return null; // needs runtime expansion; skip JIT
                    bodyForms.Add(expanded);
                    cur = c.Cdr;
                }
                try
                {
                    Directory.CreateDirectory(cacheDir);
                    var entry = new CacheEntry
                    {
                        Version = CacheVersion,
                        Hash = bodySrc,
                        Name = lp.Name,
                        Body = bodyForms.Select(f => Printer.Format(f)).ToList()
                    };
                    var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(cacheFile, json);
                }
                catch { }
            }
            else
            {
                while (cur is Cell c)
                {
                    var expanded = Evaluator.MacroExpand(c.Car, lp.ClosureEnv);
                    bodyForms.Add(expanded);
                    cur = c.Cdr;
                }
            }
            afterExpand:;
            // Step 2: Convert to AST
            var bodyAsts = bodyForms.Select(ToAst).ToList();
            var cleanedParams = lp.Params.Select(CleanParamName).ToList();
            var lexicalVars = new HashSet<string>(cleanedParams);
            // Step 3: Closure check
            foreach (var astNode in bodyAsts)
                if (HasNestedClosure(astNode, lexicalVars))
                    return null;
            // Step 3b: Check for self-recursion inside nested lambdas
            // This pattern (created by `let` expansion) causes stack overflow
            // because each self-recursion re-creates and re-invokes the inner lambda
            // through the interpreter, growing the C# call stack.
            if (lp.Name is not null && HasSelfRecursionInNestedLambda(bodyAsts, lp.Name))
                return null;
            // Step 4: Constant folding
            var foldedBody = bodyAsts.Select(FoldConstants).ToList();
            // Step 5: Compile to expression tree
            var compiler = new AstExprCompiler(lp.Name, cleanedParams, lp.IsSimple, lexicalVars);
            var bodyExprs = compiler.CompileStmtSeq(foldedBody, true);
            if (bodyExprs.Count == 0)
                bodyExprs = [Expression.Goto(compiler.BreakLabel, ObjConst(Const.VOID))];
            var loop = Expression.Loop(
                Expression.Block(bodyExprs),
                compiler.BreakLabel,
                compiler.ContinueLabel
            );
            // Build final lambda: (env, args) => { params...; loop; }
            var allVars = new List<ParameterExpression>(compiler.ParamVars);
            allVars.AddRange(compiler.AdditionalVars);
            var lambdaBody = Expression.Block(
                allVars,
                compiler.AssignStmts.Concat([loop])
            );
            var lambda = Expression.Lambda<Func<Env, object?[], object?>>(
                lambdaBody, compiler.EnvParam, compiler.ArgsParam);
            var func = lambda.Compile();
            return new CompiledLambda(func, lp.Params, lp.ClosureEnv, lp.IsSimple);
        }
        catch (Exception ex)
        {
            var dbg = Environment.GetEnvironmentVariable("MSCM_JIT_DEBUG");
            if (dbg is not null)
                Console.Error.WriteLine($"JIT compile error for {lp.Name}: {ex}");
            return null;
        }
    }
    internal static string CleanParamName(string p) =>
        p.StartsWith("rest:") ? p[5..] : p;
    internal static Expression ConstVal(object? v) => Expression.Constant(v);
    internal static Expression ObjConst(object? v) => Expression.Constant(v, typeof(object));
    // ── Scheme → AST ──
    internal static AstNode ToAst(object? expr)
    {
        if (expr is Sym s)
        {
            if (s == Const.TRUE || s == Const.FALSE)
                return new LiteralAst(s);
            return new VarAst(s.Name);
        }
        if (expr is Cell cell)
        {
            var op = cell.Car;
            var args = cell.Cdr;
            if (op == Sym.QUOTE)
                return new LiteralAst(args is Cell ac ? ac.Car : Const.NIL);
            if (op == Sym.IF)
            {
                var argsC = args as Cell;
                var test = ToAst(argsC is not null ? argsC.Car : Const.NIL);
                var thenExpr = argsC?.Cdr is Cell tc ? ToAst(tc.Car) : new LiteralAst(Const.VOID);
                AstNode elseExpr = new LiteralAst(Const.VOID);
                var rest = argsC?.Cdr is Cell cd ? cd.Cdr : Const.NIL;
                if (rest is Cell r)
                    elseExpr = ToAst(r.Car);
                return new IfAst(test, thenExpr, elseExpr);
            }
            if (op == Sym.LAMBDA)
            {
                var (parsedParams, hasRest) = ParseParamList(
                    args is Cell a1 ? a1.Car : Const.NIL);
                var bodyExprs = ParseBody(
                    args is Cell a2 ? a2.Cdr : Const.NIL);
                return new LambdaAst(parsedParams, bodyExprs, !hasRest, RawBody: args is Cell raw ? raw.Cdr : Const.NIL);
            }
            if (op == Sym.BEGIN)
                return new BeginAst(ParseBody(args));
            if (op == Sym.DEFINE)
            {
                if (args is not Cell da) return new LiteralAst(Const.VOID);
                var pat = da.Car;
                if (pat is Cell patCell)
                {
                    var name = patCell.Car.AsString();
                    var (parsedParams, hasRest) = ParseParamList(patCell.Cdr);
                    var bodyExprs = ParseBody(da.Cdr);
                    return new DefineAst(name, new LambdaAst(parsedParams, bodyExprs, !hasRest, RawBody: da.Cdr));
                }
                var valExpr = da.Cdr is Cell d2 ? d2.Car : Const.NIL;
                return new DefineAst(pat.AsString(), ToAst(valExpr));
            }
            if (op == Sym.SETBANG)
            {
                var sa = args as Cell;
                return new SetBangAst(sa?.Car.AsString() ?? "",
                    ToAst(sa?.Cdr is Cell sc ? sc.Car : Const.NIL));
            }
            // ── C# built-in special forms for JIT compilation ──
            // These are C# special forms (not macros), but ToAst needs to
            // translate them to if/lambda forms that the JIT compiler understands.
            if (op == Sym.AND)
                return ToAst(ExpandAnd(args as Cell));
            if (op == Sym.OR)
                return ToAst(ExpandOr(args as Cell));
            if (op == Sym.COND)
                return ToAst(ExpandCond(args as Cell));
            if (op == Sym.LET)
                return ToAst(ExpandLet(args as Cell, false));
            if (op == Sym.LET_STAR)
                return ToAst(ExpandLetStar(args as Cell));
            if (op == Sym.LETREC)
                return ToAst(ExpandLetRec(args as Cell));
            var procAst = ToAst(op);
            var argAsts = new List<AstNode>();
            var cur = args;
            while (cur is Cell cc) { argAsts.Add(ToAst(cc.Car)); cur = cc.Cdr; }
            return new AppAst(procAst, argAsts);
        }
        return new LiteralAst(expr);
    }

    // ── Expand C# built-in special forms to if/lambda ──
    // These produce Scheme forms that ToAst can handle directly.

    internal static object? ExpandAnd(Cell? args)
    {
        if (args is null || args is Nil) return Const.TRUE;
        if (args.Cdr is Nil) return args.Car;
        // (and test1 rest ...) => (if test1 (and rest ...) #f)
        var restAnd = ExpandAnd(args.Cdr as Cell);
        return new Cell(Sym.IF, new Cell(args.Car, new Cell(restAnd, new Cell(Const.FALSE, Const.NIL))));
    }

    internal static object? ExpandOr(Cell? args)
    {
        if (args is null || args is Nil) return Const.FALSE;
        if (args.Cdr is Nil) return args.Car;
        // (or test1 rest ...) => (let ((temp test1)) (if temp temp (or rest ...)))
        // Use temp variable to avoid double evaluation
        var temp = "__or_temp";
        var restOr = ExpandOr(args.Cdr as Cell);
        return new Cell(Sym.LET, new Cell(
            new Cell(new Cell(temp, new Cell(args.Car, Const.NIL)), Const.NIL),
            new Cell(Sym.IF, new Cell(temp, new Cell(temp, new Cell(restOr, Const.NIL))))));
    }

    internal static object? ExpandCond(Cell? args)
    {
        if (args is null || args is Nil) return Const.VOID;
        var clause = args.Car as Cell;
        if (clause is null) return Const.VOID;
        var test = clause.Car;
        var rest = clause.Cdr as Cell;

        // (else result ...)
        if (test is Sym elseSym && elseSym.Name == "else")
            return new Cell(Sym.BEGIN, rest ?? (object?)Const.NIL);

        // (test => expr) — arrow form
        if (rest is Cell afterTest && afterTest.Car is Sym arrow && arrow.Name == "=>")
        {
            var expr = afterTest.Cdr is Cell e ? e.Car : Const.VOID;
            var temp = "__cond_arrow_temp";
            var elseCond = ExpandCond(args.Cdr as Cell);
            return new Cell(Sym.LET, new Cell(
                new Cell(new Cell(temp, new Cell(test, Const.NIL)), Const.NIL),
                new Cell(Sym.IF, new Cell(temp,
                    new Cell(new Cell(expr, new Cell(temp, Const.NIL)),
                    new Cell(elseCond, Const.NIL))))));
        }

        // (test) — implicit test
        if (rest is null || rest is Nil)
        {
            var elseCond = ExpandCond(args.Cdr as Cell);
            return new Cell(Sym.IF, new Cell(test, new Cell(test, new Cell(elseCond, Const.NIL))));
        }

        // (test result1 result2 ...)
        var elseCond2 = ExpandCond(args.Cdr as Cell);
        return new Cell(Sym.IF, new Cell(test,
            new Cell(new Cell(Sym.BEGIN, rest),
            new Cell(elseCond2, Const.NIL))));
    }

    internal static object? ExpandLet(Cell? args, bool isLetStarRecurse = false)
    {
        if (args is null) return Const.VOID;
        var bindings = args.Car;
        var body = args.Cdr;

        // Named let: (let name ((var val) ...) body ...)
        // => ((letrec ((name (lambda (var...) body...))) name) val...)
        if (bindings is Sym name && body is Cell bodyCell)
        {
            // bindings here is the name; the actual var/val pairs are in the first body element
            // Actually, (let name ((var val) ...) body...) => bindings is name (Sym)
            // body = (((var val) ...) body...)
            // So the var/val pairs are bodyCell.Car, and the body expressions are bodyCell.Cdr
            var varValPairs = bodyCell.Car;  // ((var val) ...)
            var bodyExprs = bodyCell.Cdr;    // body...

            var paramNames = new List<string>();
            var valExprs = new List<object?>();
            if (varValPairs is Cell vc)
            {
                var cur = vc;
                while (cur is Cell bc)
                {
                    if (bc.Car is Cell bind && bind.Cdr is Cell bindVal)
                    {
                        paramNames.Add(bind.Car.AsString());
                        valExprs.Add(bindVal.Car);
                    }
                    cur = bc.Cdr as Cell;
                }
            }

            // (letrec ((name (lambda (params) body...)))
            //   ((name) val...))
            // => ((lambda (param...) body...) val...)
            // which is a normal let with the lambda calling itself
            // Use fixpoint: ((letrec ((name (lambda (params) body...))) name) val...)
            object? lambdaParams = Const.NIL;
            for (int i = paramNames.Count - 1; i >= 0; i--)
                lambdaParams = new Cell(paramNames[i], lambdaParams);
            var lambda = new Cell(Sym.LAMBDA, new Cell(lambdaParams, bodyExprs));
            var letRecBind = new Cell(new Cell(name, new Cell(lambda, Const.NIL)), Const.NIL);
            var letRecForm = new Cell(Sym.LETREC, new Cell(letRecBind, new Cell(name, Const.NIL)));
            // Build arg list as a Cell chain
            object? valList = Const.NIL;
            for (int i = valExprs.Count - 1; i >= 0; i--)
                valList = new Cell(valExprs[i], valList);
            var app = new Cell(letRecForm, valList);
            return app;
        }

        if (bindings is not Cell) return new Cell(Sym.BEGIN, body);

        // Regular let: (let ((var val) ...) body ...)
        // => ((lambda (var ...) body ...) val ...)
        var vars = new List<object?>();
        var vals = new List<object?>();
        var cur2 = bindings;
        while (cur2 is Cell bc)
        {
            if (bc.Car is Cell bind && bind.Cdr is Cell bindVal)
            {
                vars.Add(bind.Car);
                vals.Add(bindVal.Car);
            }
            cur2 = bc.Cdr;
        }
        // Build params list and arg list as Cell chains
        object? paramsCell = Const.NIL;
        for (int i = vars.Count - 1; i >= 0; i--)
            paramsCell = new Cell(vars[i], paramsCell);
        var lambda2 = new Cell(Sym.LAMBDA, new Cell(paramsCell, body));
        object? argList = Const.NIL;
        for (int i = vals.Count - 1; i >= 0; i--)
            argList = new Cell(vals[i], argList);
        return new Cell(lambda2, argList);
    }

    internal static object? ExpandLetStar(Cell? args)
    {
        if (args is null) return Const.VOID;
        var bindings = args.Car;
        var body = args.Cdr;

        if (bindings is not Cell)
            return new Cell(Sym.BEGIN, body);

        // let* with one binding: (let ((var val)) body...) => (let ((var val)) body...)
        // Actually: (let* ((var val) rest...) body...) => (let ((var val)) (let* (rest...) body...))
        if (bindings is Cell bc)
        {
            var firstBinding = bc.Car as Cell;
            var restBindings = bc.Cdr;
            var singleBinding = new Cell(firstBinding, Const.NIL);
            var restLetStar = ExpandLetStar(new Cell(restBindings, body));
            return new Cell(Sym.LET, new Cell(singleBinding, new Cell(restLetStar, Const.NIL)));
        }
        return new Cell(Sym.BEGIN, body);
    }

    internal static object? ExpandLetRec(Cell? args)
    {
        if (args is null) return Const.VOID;
        var bindings = args.Car;
        var body = args.Cdr;

        if (bindings is not Cell)
            return new Cell(Sym.BEGIN, body);

        // letrec: (letrec ((var val) ...) body...)
        // => (let ((var #f) ...) (set! var val) ... (let () body...))
        // Actually simpler: use let + set!
        var letBindings = new List<object?>();
        var setForms = new List<object?>();
        var cur = bindings;
        while (cur is Cell bc)
        {
            if (bc.Car is Cell bind && bind.Cdr is Cell bindVal)
            {
                var varName = bind.Car;
                letBindings.Add(new Cell(varName, new Cell(Const.FALSE, Const.NIL)));
                setForms.Add(new Cell(Sym.SETBANG, new Cell(varName, new Cell(bindVal.Car, Const.NIL))));
            }
            cur = bc.Cdr;
        }
        // Build binding list and set! sequence as Cell chains
        object? letBindingsCell = Const.NIL;
        for (int i = letBindings.Count - 1; i >= 0; i--)
            letBindingsCell = new Cell(letBindings[i], letBindingsCell);
        object? setBodyList = Const.NIL;
        for (int i = setForms.Count - 1; i >= 0; i--)
            setBodyList = new Cell(setForms[i], setBodyList);
        var setBody = new Cell(setBodyList, new Cell(Sym.BEGIN, body));
        return new Cell(Sym.LET, new Cell(letBindingsCell, setBody));
    }

    internal static (List<string> Params, bool HasRest) ParseParamList(object? cell)
    {
        var @params = new List<string>();
        var cur = cell;
        bool hasRest = false;
        while (cur is Cell c) { @params.Add(c.Car.AsString()); cur = c.Cdr; }
        if (cur is not Nil)
        {
            @params.Add("rest:" + (cur as Sym)?.Name ?? cur?.ToString() ?? "");
            hasRest = true;
        }
        return (@params, hasRest);
    }
    internal static List<AstNode> ParseBody(object? body)
    {
        var result = new List<AstNode>();
        var cur = body;
        while (cur is Cell c) { result.Add(ToAst(c.Car)); cur = c.Cdr; }
        return result;
    }
    // ── Constant Folding ──
    internal static AstNode FoldConstants(AstNode node)
    {
        if (node is IfAst ifn)
        {
            var test = FoldConstants(ifn.Test);
            var then = FoldConstants(ifn.Then);
            var els = FoldConstants(ifn.Else);
            if (test is LiteralAst lv)
                return lv.Val is Sym sv && sv == Const.FALSE ? els : then;
            return new IfAst(test, then, els);
        }
        if (node is BeginAst bn)
            return new BeginAst(bn.Exprs.Select(FoldConstants).ToList());
        if (node is AppAst an)
        {
            var proc = FoldConstants(an.Proc);
            var args = an.Args.Select(FoldConstants).ToList();
            if (proc is VarAst va && args.Count >= 1 && args[0] is LiteralAst la0)
            {
                var av = la0.Val;
                if (va.Name == "not")
                    return new LiteralAst(av is Sym sv && sv == Const.FALSE ? Const.TRUE : Const.FALSE);
                if (va.Name == "null?")
                    return new LiteralAst(av is Nil ? Const.TRUE : Const.FALSE);
                if (va.Name == "pair?")
                    return new LiteralAst(av is Cell ? Const.TRUE : Const.FALSE);
                if (va.Name == "car" && av is Cell ac)
                    return new LiteralAst(ac.Car);
                if (va.Name == "cdr" && av is Cell ac2)
                    return new LiteralAst(ac2.Cdr);
                if (va.Name == "number?")
                    return new LiteralAst(IsNumeric(av) ? Const.TRUE : Const.FALSE);
                if (va.Name == "boolean?")
                    return new LiteralAst(av == Const.TRUE || av == Const.FALSE ? Const.TRUE : Const.FALSE);
                if (va.Name == "symbol?")
                    return new LiteralAst(av is Sym ? Const.TRUE : Const.FALSE);
                if (va.Name == "string?")
                    return new LiteralAst(av is string or SchemeString ? Const.TRUE : Const.FALSE);
                if (va.Name == "zero?")
                    return new LiteralAst(NumericHelper.IsZero(av) ? Const.TRUE : Const.FALSE);
                if (va.Name == "even?")
                    return new LiteralAst(NumericHelper.IsEven(av) ? Const.TRUE : Const.FALSE);
                if (va.Name == "odd?")
                    return new LiteralAst(!NumericHelper.IsEven(av) ? Const.TRUE : Const.FALSE);
            }
            if (proc is VarAst vaList && args.Count >= 1 && args.All(a => a is LiteralAst) && vaList.Name == "list")
            {
                var items = args.Cast<LiteralAst>().Select(la => la.Val).ToList();
                object? list = Const.NIL;
                for (int i = items.Count - 1; i >= 0; i--)
                    list = new Cell(items[i], list);
                return new LiteralAst(list);
            }
            if (proc is VarAst vaCons && args.Count == 2 && args[0] is LiteralAst lcar && args[1] is LiteralAst lcdr && vaCons.Name == "cons")
                return new LiteralAst(new Cell(lcar.Val, lcdr.Val));
            if (proc is VarAst va2 && args.Count == 2 && args[0] is LiteralAst ll && args[1] is LiteralAst lr)
            {
                var lv = ll.Val; var rv = lr.Val;
                if (IsNumeric(lv) && IsNumeric(rv))
                {
                    try
                    {
                        return (va2.Name) switch
                        {
                            "+" => new LiteralAst(NumericHelper.Add(lv, rv)),
                            "-" => new LiteralAst(NumericHelper.Sub(lv, rv)),
                            "*" => new LiteralAst(NumericHelper.Mul(lv, rv)),
                            "/" => new LiteralAst(NumericHelper.Div(lv, rv)),
                            "<" => new LiteralAst(NumericHelper.Compare(lv, rv) < 0 ? Const.TRUE : Const.FALSE),
                            ">" => new LiteralAst(NumericHelper.Compare(lv, rv) > 0 ? Const.TRUE : Const.FALSE),
                            "<=" => new LiteralAst(NumericHelper.Compare(lv, rv) <= 0 ? Const.TRUE : Const.FALSE),
                            ">=" => new LiteralAst(NumericHelper.Compare(lv, rv) >= 0 ? Const.TRUE : Const.FALSE),
                            "=" => new LiteralAst(NumericHelper.Compare(lv, rv) == 0 ? Const.TRUE : Const.FALSE),
                            "quotient" => new LiteralAst(NumericHelper.Quotient(lv, rv)),
                            "remainder" => new LiteralAst(NumericHelper.Remainder(lv, rv)),
                            "modulo" => new LiteralAst(NumericHelper.Modulo(lv, rv)),
                            _ => (AstNode)an
                        };
                    }
                    catch { }
                }
            }
            return new AppAst(proc, args);
        }
        if (node is LambdaAst ln)
            return new LambdaAst(ln.Params, ln.Body.Select(FoldConstants).ToList(), ln.IsSimple, RawBody: ln.RawBody);
        if (node is DefineAst dn)
            return new DefineAst(dn.Name, FoldConstants(dn.Val));
        if (node is SetBangAst sn)
            return new SetBangAst(sn.Name, FoldConstants(sn.Val));
        return node;
    }
    static bool IsNumeric(object? v) => v is int or long or System.Numerics.BigInteger or double or SchemeFraction;
    // ── Closure Detection ──
    internal static bool HasNestedClosure(AstNode node, HashSet<string> localVars)
    {
        if (node is VarAst or LiteralAst) return false;
        if (node is LambdaAst la)
        {
            var innerParams = la.Params.Select(CleanParamName).ToHashSet();
            var extended = new HashSet<string>(localVars);
            extended.UnionWith(innerParams);
            foreach (var bn in la.Body)
            {
                if (RefersOuterVar(bn, localVars, innerParams)) return true;
                if (HasNestedClosure(bn, extended)) return true;
            }
            return false;
        }
        if (node is DefineAst d) return HasNestedClosure(d.Val, localVars);
        if (node is SetBangAst s) return HasNestedClosure(s.Val, localVars);
        if (node is IfAst i) return HasNestedClosure(i.Test, localVars) || HasNestedClosure(i.Then, localVars) || HasNestedClosure(i.Else, localVars);
        if (node is AppAst a) { if (HasNestedClosure(a.Proc, localVars)) return true; return a.Args.Any(x => HasNestedClosure(x, localVars)); }
        if (node is BeginAst b) return b.Exprs.Any(e => HasNestedClosure(e, localVars));
        return false;
    }
    internal static bool RefersOuterVar(AstNode node, HashSet<string> outerVars, HashSet<string> innerParams)
    {
        if (node is VarAst v) return outerVars.Contains(v.Name) && !innerParams.Contains(v.Name);
        if (node is LiteralAst) return false;
        if (node is LambdaAst la)
        {
            var nested = la.Params.Select(CleanParamName).ToHashSet();
            var combined = new HashSet<string>(innerParams);
            combined.UnionWith(nested);
            return la.Body.Any(b => RefersOuterVar(b, outerVars, combined));
        }
        if (node is DefineAst d) return RefersOuterVar(d.Val, outerVars, innerParams);
        if (node is SetBangAst s) return (outerVars.Contains(s.Name) && !innerParams.Contains(s.Name)) || RefersOuterVar(s.Val, outerVars, innerParams);
        if (node is IfAst i) return RefersOuterVar(i.Test, outerVars, innerParams) || RefersOuterVar(i.Then, outerVars, innerParams) || RefersOuterVar(i.Else, outerVars, innerParams);
        if (node is AppAst a) { if (RefersOuterVar(a.Proc, outerVars, innerParams)) return true; return a.Args.Any(x => RefersOuterVar(x, outerVars, innerParams)); }
        if (node is BeginAst b) return b.Exprs.Any(e => RefersOuterVar(e, outerVars, innerParams));
        return false;
    }
    // ── Mutation detection for self-recursion safety ──
    static bool HasMutation(AstNode node, string varName)
    {
        if (node is SetBangAst s) return s.Name == varName;
        if (node is IfAst i) return HasMutation(i.Test, varName) || HasMutation(i.Then, varName) || HasMutation(i.Else, varName);
        if (node is AppAst a) { if (HasMutation(a.Proc, varName)) return true; return a.Args.Any(x => HasMutation(x, varName)); }
        if (node is BeginAst b) return b.Exprs.Any(e => HasMutation(e, varName));
        if (node is LambdaAst la) { if (la.Params.Select(CleanParamName).Contains(varName)) return false; return la.Body.Any(e => HasMutation(e, varName)); }
        return false;
    }
    static bool HasSelfRecursionInNestedLambda(List<AstNode> body, string selfName)
    {
        foreach (var node in body)
            if (ScanNestedSelfRecursion(node, selfName))
                return true;
        return false;
    }
    static bool ScanNestedSelfRecursion(AstNode node, string selfName)
    {
        if (node is LambdaAst la)
            return la.Body.Any(e => ContainsCallTo(e, selfName));
        if (node is BeginAst bn)
            return bn.Exprs.Any(e => ScanNestedSelfRecursion(e, selfName));
        if (node is AppAst an)
            return ScanNestedSelfRecursion(an.Proc, selfName) || an.Args.Any(e => ScanNestedSelfRecursion(e, selfName));
        if (node is DefineAst dn)
            return ScanNestedSelfRecursion(dn.Val, selfName);
        if (node is SetBangAst sn)
            return ScanNestedSelfRecursion(sn.Val, selfName);
        if (node is IfAst ifn)
            return ScanNestedSelfRecursion(ifn.Test, selfName) || ScanNestedSelfRecursion(ifn.Then, selfName) || ScanNestedSelfRecursion(ifn.Else, selfName);
        return false;
    }
    static bool ContainsCallTo(AstNode node, string name)
    {
        return node switch
        {
            AppAst an => an.Proc is VarAst vn && vn.Name == name,
            IfAst ifn => ContainsCallTo(ifn.Test, name) || ContainsCallTo(ifn.Then, name) || ContainsCallTo(ifn.Else, name),
            BeginAst bn => bn.Exprs.Any(e => ContainsCallTo(e, name)),
            _ => false
        };
    }
    // ── Expression Tree Compiler ──
    internal class AstExprCompiler
    {
        public string? SelfName { get; }
        public List<string> Params { get; }
        public bool IsSimple { get; }
        public HashSet<string> LexicalVars { get; }
        public List<ParameterExpression> ParamVars { get; }
        public List<ParameterExpression> AdditionalVars { get; }
        public List<Expression> AssignStmts { get; }
        public Dictionary<string, int> ParamIndexMap { get; }
        public LabelTarget BreakLabel { get; }
        public LabelTarget ContinueLabel { get; }
        public ParameterExpression EnvParam { get; }
        public ParameterExpression ArgsParam { get; }
        public AstExprCompiler(string? selfName, List<string> cleanedParams,
            bool isSimple, HashSet<string> lexicalVars)
        {
            SelfName = selfName;
            Params = cleanedParams;
            IsSimple = isSimple;
            LexicalVars = lexicalVars;
            BreakLabel = Expression.Label(typeof(object));
            ContinueLabel = Expression.Label();
            EnvParam = Expression.Parameter(typeof(Env), "env");
            ArgsParam = Expression.Parameter(typeof(object?[]), "args");
            ParamVars = new List<ParameterExpression>();
            AdditionalVars = new List<ParameterExpression>();
            AssignStmts = new List<Expression>();
            ParamIndexMap = new Dictionary<string, int>();
            for (int i = 0; i < cleanedParams.Count; i++)
            {
                var pv = Expression.Variable(typeof(object), cleanedParams[i]);
                ParamVars.Add(pv);
                ParamIndexMap[cleanedParams[i]] = i;
                AssignStmts.Add(Expression.Assign(pv,
                    Expression.Condition(
                        Expression.LessThan(ConstVal(i), Expression.ArrayLength(ArgsParam)),
                        Expression.ArrayIndex(ArgsParam, ConstVal(i)),
                        ObjConst(Const.NIL))));
            }
        }
        // ── Compile statement sequences ──
        public List<Expression> CompileStmtSeq(List<AstNode> nodes, bool isTail)
        {
            var stmts = new List<Expression>();
            for (int i = 0; i < nodes.Count; i++)
                stmts.AddRange(CompileStmt(nodes[i], isTail && i == nodes.Count - 1));
            return stmts;
        }
        // ── Compile a single statement ──
        public List<Expression> CompileStmt(AstNode node, bool isTail)
        {
            if (node is IfAst ifn)
            {
                var testExpr = CompileExpr(ifn.Test);
                var cond = Expression.Not(Expression.ReferenceEqual(testExpr, ObjConst(Const.FALSE)));
                var thenStmts = CompileStmt(ifn.Then, isTail);
                var elseStmts = CompileStmt(ifn.Else, isTail);
                return [Expression.IfThenElse(cond,
                    Expression.Block(thenStmts),
                    Expression.Block(elseStmts))];
            }
            if (node is BeginAst bn)
                return CompileStmtSeq(bn.Exprs, isTail);
            if (node is SetBangAst sn)
            {
                var valExpr = CompileExpr(sn.Val);
                if (ParamIndexMap.TryGetValue(sn.Name, out int si))
                {
                    var stmts = new List<Expression>
                    {
                        Expression.Assign(ParamVars[si], valExpr)
                    };
                    if (isTail) stmts.Add(Expression.Goto(BreakLabel, ObjConst(Const.VOID)));
                    return stmts;
                }
                var call = Expression.Call(typeof(JitRuntime), "EnvSetVar", null,
                    EnvParam, ConstVal(sn.Name), valExpr);
                if (isTail) return [Expression.Goto(BreakLabel, call)];
                return [call];
            }
            // Tail position AppAst → check self-recursion, cross-call, inline
            if (isTail && node is AppAst app && app.Proc is VarAst pv)
            {
                // Self-recursion
                if (SelfName is not null && pv.Name == SelfName && IsSimple)
                    return CompileSelfTailCall(app);
                // Inline ops
                if (!LexicalVars.Contains(pv.Name))
                {
                    var inl = TryInlineOp(app);
                    if (inl is not null)
                        return [Expression.Goto(BreakLabel, Expression.Convert(inl, typeof(object)))];
                    // Cross-function tail call (not immutable primitive, not local)
                    if (SelfName is not null && !JitRuntime.ImmutablePrimitives.Contains(pv.Name))
                    {
                        var procExpr = CompileExpr(app.Proc);
                        var argsExprs = app.Args.Select(a => Expression.Convert(CompileExpr(a), typeof(object))).ToList();
                        var argsArray = Expression.NewArrayInit(typeof(object), argsExprs);
                        return [Expression.Goto(BreakLabel,
                            Expression.Call(typeof(JitRuntime), "MakeTailCall", null,
                                procExpr, argsArray, EnvParam))];
                    }
                }
                // Regular call
                    return [Expression.Goto(BreakLabel, Expression.Convert(CompileAppCall(app), typeof(object)))];
            }
            if (isTail)
            {
                if (node is AppAst app2)
                {
                    var inl = TryInlineOp(app2);
                    if (inl is not null)
                        return [Expression.Goto(BreakLabel, Expression.Convert(inl, typeof(object)))];
                    return [Expression.Goto(BreakLabel, Expression.Convert(CompileAppCall(app2), typeof(object)))];
                }
                return [Expression.Goto(BreakLabel, Expression.Convert(CompileExpr(node), typeof(object)))];
            }
            // Non-tail: evaluate and discard
            return [CompileExpr(node)];
        }
        // ── Self-recursion tail call ──
        List<Expression> CompileSelfTailCall(AppAst node)
        {
            var stmts = new List<Expression>();
            int nParams = Params.Count;
            int nArgs = node.Args.Count;
            var temps = new List<ParameterExpression>();
            var tempAssigns = new List<Expression>();
            for (int i = 0; i < nArgs && i < nParams; i++)
            {
                var temp = Expression.Variable(typeof(object), $"__t_{i}");
                temps.Add(temp);
                AdditionalVars.Add(temp);
                tempAssigns.Add(Expression.Assign(temp, Expression.Convert(CompileExpr(node.Args[i]), typeof(object))));
            }
            for (int i = nArgs; i < nParams; i++)
            {
                var temp = Expression.Variable(typeof(object), $"__t_{i}");
                temps.Add(temp);
                AdditionalVars.Add(temp);
                tempAssigns.Add(Expression.Assign(temp, ObjConst(Const.NIL)));
            }
            tempAssigns.AddRange(temps.Select((t, i) =>
                Expression.Assign(ParamVars[i], t)));
            stmts.AddRange(tempAssigns);
            stmts.Add(Expression.Goto(ContinueLabel));
            return stmts;
        }
        static readonly HashSet<string> CrNames =
        [
            "caar", "cadr", "cdar", "cddr",
            "caaar", "caadr", "cadar", "caddr",
            "cdaar", "cdadr", "cddar", "cdddr",
        ];
        // ── Try inline operation ──
        Expression? TryInlineOp(AppAst node)
        {
            if (node.Proc is not VarAst vn || LexicalVars.Contains(vn.Name))
                return null;
            var op = vn.Name;
            var nArgs = node.Args.Count;
            if (nArgs == 0 && op == "the-environment")
                return EnvParam;
            if (nArgs == 1)
            {
                var arg = CompileExpr(node.Args[0]);
                if (CrNames.Contains(op))
                {
                    var expr = arg;
                    for (int i = op.Length - 2; i >= 1; i--)
                        expr = op[i] == 'a'
                            ? Expression.Call(typeof(JitRuntime), "CarOf", null, expr)
                            : Expression.Call(typeof(JitRuntime), "CdrOf", null, expr);
                    return expr;
                }
                return op switch
                {
                    "car" => Expression.Call(typeof(JitRuntime), "CarOf", null, arg),
                    "cdr" => Expression.Call(typeof(JitRuntime), "CdrOf", null, arg),
                    "null?" => Expression.Condition(
                        Expression.TypeIs(arg, typeof(Nil)),
                        ObjConst(Const.TRUE), ObjConst(Const.FALSE)),
                    "pair?" => Expression.Condition(
                        Expression.TypeIs(arg, typeof(Cell)),
                        ObjConst(Const.TRUE), ObjConst(Const.FALSE)),
                    "not" => Expression.Condition(
                        Expression.ReferenceEqual(arg, ObjConst(Const.FALSE)),
                        ObjConst(Const.TRUE), ObjConst(Const.FALSE)),
                    "number?" => Expression.Condition(
                        Expression.OrElse(
                            Expression.TypeIs(arg, typeof(int)),
                            Expression.OrElse(
                                Expression.TypeIs(arg, typeof(long)),
                                Expression.OrElse(
                                    Expression.TypeIs(arg, typeof(double)),
                                    Expression.OrElse(
                                        Expression.TypeIs(arg, typeof(SchemeFraction)),
                                        Expression.TypeIs(arg, typeof(BigInteger)))))),
                        ObjConst(Const.TRUE), ObjConst(Const.FALSE)),
                    "boolean?" => Expression.Condition(
                        Expression.OrElse(
                            Expression.ReferenceEqual(arg, ObjConst(Const.TRUE)),
                            Expression.ReferenceEqual(arg, ObjConst(Const.FALSE))),
                        ObjConst(Const.TRUE), ObjConst(Const.FALSE)),
                    "symbol?" => Expression.Condition(
                        Expression.TypeIs(arg, typeof(Sym)),
                        ObjConst(Const.TRUE), ObjConst(Const.FALSE)),
                    "string?" => Expression.Condition(
                        Expression.OrElse(
                            Expression.TypeIs(arg, typeof(string)),
                            Expression.TypeIs(arg, typeof(SchemeString))),
                        ObjConst(Const.TRUE), ObjConst(Const.FALSE)),
                    "zero?" => Expression.Condition(
                        Expression.Call(typeof(NumericHelper), "IsZero", null, arg),
                        ObjConst(Const.TRUE), ObjConst(Const.FALSE)),
                    "even?" => Expression.Condition(
                        Expression.Call(typeof(NumericHelper), "IsEven", null, arg),
                        ObjConst(Const.TRUE), ObjConst(Const.FALSE)),
                    "odd?" => Expression.Condition(
                        Expression.Not(Expression.Call(typeof(NumericHelper), "IsEven", null, arg)),
                        ObjConst(Const.TRUE), ObjConst(Const.FALSE)),
                    "reverse" => Expression.Call(typeof(JitRuntime), "Reverse1", null, arg),
                    "string-length" => Expression.Call(typeof(JitRuntime), "StringLength", null, arg),
                    "vector-length" => Expression.Call(typeof(JitRuntime), "VectorLength", null, arg),
                    "length" => Expression.Call(typeof(JitRuntime), "ListLength", null, arg),
                    _ => null
                };
            }
            if (nArgs >= 2 && (op == "+" || op == "-" || op == "*" || op == "/"))
            {
                string helper = op switch { "+" => "Add", "-" => "Sub", "*" => "Mul", "/" => "Div", _ => "Add" };
                Expression curr = Expression.Convert(CompileExpr(node.Args[0]), typeof(object));
                for (int i = 1; i < nArgs; i++)
                {
                    var nextExpr = Expression.Convert(CompileExpr(node.Args[i]), typeof(object));
                    curr = Expression.Call(typeof(NumericHelper), helper, null, curr, nextExpr);
                }
                return curr;
            }
            if (op == "list" && nArgs <= 5)
            {
                var nilConst = ObjConst(Const.NIL);
                Expression result = nilConst;
                for (int i = nArgs - 1; i >= 0; i--)
                {
                    var elem = CompileExpr(node.Args[i]);
                    result = Expression.New(typeof(Cell).GetConstructor([typeof(object), typeof(object)])!, elem, result);
                }
                return result;
            }
            if (nArgs == 2)
            {
                if (op == "eq?")
                {
                    var left = CompileExpr(node.Args[0]);
                    var right = CompileExpr(node.Args[1]);
                    return Expression.Condition(
                        Expression.OrElse(
                            Expression.ReferenceEqual(left, right),
                            Expression.AndAlso(
                                Expression.IsTrue(
                                    Expression.NotEqual(left, Expression.Constant(null, typeof(object)))),
                                Expression.Call(
                                    left, typeof(object).GetMethod("Equals", [typeof(object)])!,
                                    right))),
                        ObjConst(Const.TRUE), ObjConst(Const.FALSE));
                }
                if (op == "cons")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.New(typeof(Cell).GetConstructor([typeof(object), typeof(object)])!, a, b);
                }
                if (op == "=" || op == "<" || op == ">" || op == "<=" || op == ">=")
                {
                    var left = Expression.Convert(CompileExpr(node.Args[0]), typeof(object));
                    var right = Expression.Convert(CompileExpr(node.Args[1]), typeof(object));
                    var cmp = Expression.Call(typeof(NumericHelper), "Compare", null, left, right);
                    var zero = Expression.Constant(0);
                    Expression cond = op switch
                    {
                        "=" => Expression.Equal(cmp, zero),
                        "<" => Expression.LessThan(cmp, zero),
                        ">" => Expression.GreaterThan(cmp, zero),
                        "<=" => Expression.LessThanOrEqual(cmp, zero),
                        ">=" => Expression.GreaterThanOrEqual(cmp, zero),
                        _ => Expression.Equal(cmp, zero),
                    };
                    return Expression.Condition(cond, ObjConst(Const.TRUE), ObjConst(Const.FALSE));
                }
                if (op == "quotient" || op == "remainder" || op == "modulo")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    string helper = op switch
                    {
                        "quotient" => "Quotient",
                        "remainder" => "Remainder",
                        "modulo" => "Modulo",
                        _ => "Quotient",
                    };
                    return Expression.Call(typeof(NumericHelper), helper, null, a, b);
                }
                if (op == "append")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "Append2", null, a, b);
                }
                if (op == "string-append")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "StringAppend2", null, a, b);
                }
                if (op == "string-ref")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "StringRef", null, a, b);
                }
                if (op == "vector-ref")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "VectorRef", null, a, b);
                }
                if (op == "list-tail")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "ListTail", null, a, b);
                }
                if (op == "list-ref")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "ListRef", null, a, b);
                }
                if (op == "memq")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "Memq", null, a, b);
                }
                if (op == "assq")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "Assq", null, a, b);
                }
                if (op == "eqv?")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "Eqv", null, a, b);
                }
                if (op == "equal?")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "Equal2", null, a, b);
                }
                if (op == "member")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "Member", null, a, b);
                }
                if (op == "assoc")
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "Assoc", null, a, b);
                }
                if (op == "map" && nArgs == 2)
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "Map1", null, a, b);
                }
                if (op == "filter" && nArgs == 2)
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "Filter1", null, a, b);
                }
                if (op == "for-each" && nArgs == 2)
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "ForEach1", null, a, b);
                }
                if (op == "apply" && nArgs == 2)
                {
                    var a = CompileExpr(node.Args[0]);
                    var b = CompileExpr(node.Args[1]);
                    return Expression.Call(typeof(JitRuntime), "ApplyList", null, a, b);
                }
            }
            return null;
        }
        // ── Compile application call ──
        Expression CompileAppCall(AppAst node)
        {
            var procExpr = CompileExpr(node.Proc);
            var argsExprs = node.Args.Select(a => Expression.Convert(CompileExpr(a), typeof(object))).ToList();
            var argsArray = Expression.NewArrayInit(typeof(object), argsExprs);
            return Expression.Call(typeof(JitRuntime), "Invoke", null,
                procExpr, argsArray, EnvParam);
        }
        // ── Compile expression (returns a value) ──
        public Expression CompileExpr(AstNode node)
        {
            if (node is LiteralAst lit)
                return ConstVal(lit.Val);
            if (node is VarAst vn)
            {
                var name = vn.Name;
                if (ParamIndexMap.TryGetValue(name, out int idx))
                    return ParamVars[idx];
                return Expression.Call(EnvParam, "Lookup", null, ConstVal(name));
            }
            if (node is IfAst ifn)
            {
                var test = CompileExpr(ifn.Test);
                var cond = Expression.Not(Expression.ReferenceEqual(test, ObjConst(Const.FALSE)));
                return Expression.Condition(cond,
                    Expression.Convert(CompileExpr(ifn.Then), typeof(object)),
                    Expression.Convert(CompileExpr(ifn.Else), typeof(object)));
            }
            if (node is BeginAst bn)
            {
                if (bn.Exprs.Count == 0) return ObjConst(Const.VOID);
                if (bn.Exprs.Count == 1) return CompileExpr(bn.Exprs[0]);
                var allExprs = bn.Exprs.Select(CompileExpr).ToList();
                return Expression.Block(allExprs);
            }
            if (node is DefineAst dn)
            {
                var valExpr = CompileExpr(dn.Val);
                // define returns the name symbol (or void)
                return Expression.Call(EnvParam, "Define", null, ConstVal(dn.Name), valExpr);
            }
            if (node is SetBangAst sn)
            {
                var valExpr = CompileExpr(sn.Val);
                if (ParamIndexMap.TryGetValue(sn.Name, out int si))
                    return Expression.Assign(ParamVars[si], valExpr);
                return Expression.Call(typeof(JitRuntime), "EnvSetVar", null,
                    EnvParam, ConstVal(sn.Name), valExpr);
            }
            if (node is LambdaAst ln)
            {
                return Expression.Call(typeof(JitRuntime), "MakeLambda", null,
                    ConstVal(ln.Params), ConstVal(ln.IsSimple), EnvParam,
                    ConstVal(ln.RawBody ?? Const.NIL));
            }
            if (node is AppAst an)
            {
                var inl = TryInlineOp(an);
                if (inl is not null) return inl;
                return CompileAppCall(an);
            }
            return ObjConst(Const.VOID);
        }
    }
}