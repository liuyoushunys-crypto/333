using Miniscm.Types;
using Miniscm.Reader;
using Miniscm.Compiler;
using Void = Miniscm.Types.Void;

namespace Miniscm.Eval;

public static class Evaluator
{
    public static readonly Env GlobalEnv = new();
    public static readonly Dictionary<Sym, Func<object?, Env, object?>> Specials = [];
    private static readonly object? UnboundSentinel = new();

    // JIT compilation guard (prevents reentrant compilation)
    internal static bool IsCompiling = false;

    // Macro expansion helper used by the JIT compiler
    internal static object? MacroExpand(object? expr, Env env, HashSet<object?>? seen = null)
    {
        seen ??= [];
        var levelSeen = new HashSet<object?>();
        // Strip top-level SyntaxObject
        while (expr is SyntaxObject so)
            expr = so.Expr;
        while (expr is Cell cell && cell.Car is Sym opSym)
        {
            // Strip syntax from car for operator lookup
            var lookupSym = cell.Car;
            if (lookupSym is SyntaxObject so2)
                lookupSym = so2.Expr;
            if (levelSeen.Contains(expr) || seen.Contains(expr))
                break;
            levelSeen.Add(expr);
            seen.Add(expr);
            if (lookupSym is not Sym opSym2)
                break;
            var proc = env.LookupSilent(opSym2.Name, UnboundSentinel);
            if (ReferenceEquals(proc, UnboundSentinel))
                break;
            var expanded = ExpandMacro(proc, cell, cell.Cdr, env);
            if (expanded is null)
                break;
            expr = expanded;
            // Strip syntax from expanded form
            while (expr is SyntaxObject sox)
                expr = sox.Expr;
        }
        if (expr is Cell c)
        {
            if (c.Car is Sym s && s.Name == "quote")
                return expr;
            // Strip syntax from car if needed
            var carExpr = c.Car;
            while (carExpr is SyntaxObject so3)
                carExpr = so3.Expr;
            // If car was a SyntaxObject wrapping a non-Sym, keep original
            if (carExpr is not Sym)
                carExpr = c.Car;
            var childSeen = new HashSet<object?>(seen);
            var newCar = MacroExpand(c.Car, env, childSeen);
            var newCdr = MacroExpand(c.Cdr, env, childSeen);
            if (newCar is null && newCdr is null) return expr;
            return new Cell(newCar ?? c.Car, newCdr ?? c.Cdr);
        }
        return expr;
    }

    private static void Put(Sym s, Func<object?, Env, object?> f) => Specials[s] = f;

    public static void InitSpecials()
    {
        Put(Sym.QUOTE, HQuote);
        Put(Sym.IF, HIf);
        Put(Sym.LAMBDA, HLambda);
        Put(Sym.BEGIN, HBegin);
        Put(Sym.DEFINE, HDefine);
        Put(Sym.SETBANG, HSet);
        Put(Sym.SETF, HSetf);
        // 注: quasiquote 等已迁移到 Scheme (boot-min.scm)。
        //     the-environment 是 C# 暴露给 Scheme 的桥接接口:
        //     Scheme 端用 (eval expr (the-environment)) 在调用方词法环境求值
        //     unquote, 使 quasiquote/define-syntax 等可在 Scheme 自举实现。
        Put(Sym.THE_ENVIRONMENT, HTheEnvironment);
        Put(Sym.UNQUOTE, HUnquote);
        Put(Sym.UNSPLICE, HUnquote);
        Put(Sym.USYNTAX, HUnquote);
        Put(Sym.DM, HDefineMacro);
        Put(Sym.AND, HAnd);
        Put(Sym.OR, HOr);
        Put(Sym.COND, HCond);
        Put(Sym.LET, HLet);
        Put(Sym.LET_STAR, HLetStar);
        Put(Sym.LETREC, HLetrec);
    }

    // ── Special Forms ──

    // the-environment: 返回当前词法环境对象 (供 (eval expr env) 使用)
    private static object? HTheEnvironment(object? args, Env env) => env;

    private static object? HQuote(object? args, Env env)
    {
        var v = args is Cell c ? c.Car : Const.NIL;
        return StripSyntax(v);
    }

    private static object? HUnquote(object? args, Env env) =>
        throw new Exception("unquote outside quasiquote");

    private static object? HIf(object? args, Env env)
    {
        if (args is not Cell a) return Const.VOID;
        var test = Eval(a.Car, env);
        if (test is Sym t && t == Const.FALSE)
        {
            if (a.Cdr is Cell thenElse && thenElse.Cdr is Cell elseClause)
                return new TailCall(elseClause.Car, env);
            return Const.VOID;
        }
        return a.Cdr is Cell thenClause ? new TailCall(thenClause.Car, env) : Const.VOID;
    }

    private static object? HLambda(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad lambda form");
        var @params = new List<string>();
        var cur = a.Car;
        bool hasRest = false;
        while (cur is Cell pc) { @params.Add(pc.Car.AsString()); cur = pc.Cdr; }
        if (cur is not Nil) { @params.Add("rest:" + (cur as Sym)?.Name ?? cur?.ToString() ?? ""); hasRest = true; }
        return new LambdaProc(@params, a.Cdr, env, !hasRest);
    }

    private static object? HBegin(object? args, Env env) => SeqTailCall(args, env);

    private static object? HDefine(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad define form");
        if (a.Car is Cell pat)
        {
            var name = pat.Car.AsString();
            var @params = new List<string>();
            var cur = pat.Cdr;
            bool hasRest = false;
            while (cur is Cell pc) { @params.Add(pc.Car.AsString()); cur = pc.Cdr; }
            if (cur is not Nil) { @params.Add("rest:" + (cur as Sym)?.Name ?? cur?.ToString() ?? ""); hasRest = true; }
            env.Data[name] = new LambdaProc(@params, a.Cdr, env, !hasRest, name);
            return Sym.Intern(name);
        }
        var name2 = a.Car.AsString();
        if (a.Cdr is Cell valCell)
            env.Data[name2] = Eval(valCell.Car, env);
        return Sym.Intern(name2);
    }

    private static object? HSet(object? args, Env env)
    {
        if (args is not Cell a) return Const.VOID;
        if (a.Car is Sym s)
        {
            var v = Eval(a.Cdr is Cell c ? c.Car : Const.NIL, env);
            var e = env;
            while (e is not null)
            {
                if (e.Data.ContainsKey(s.Name)) { e.Data[s.Name] = v; return Const.VOID; }
                e = e.Parent;
            }
            env.Data[s.Name] = v;
            return Const.VOID;
        }
        return new TailCall(new Cell(Sym.SETF, new Cell(a.Car, new Cell(a.Cdr is Cell c2 ? c2.Car : Const.NIL, Const.NIL))), env);
    }

    private static object? HSetf(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad set!-form");
        var place = a.Car;
        var val = a.Cdr is Cell c ? c.Car : Const.NIL;
        if (place is Cell pc && pc.Car is Sym ps)
        {
            if (ps.Name is "car" or "cdr")
            {
                var setter = Sym.Intern($"set-{ps.Name}!");
                return new TailCall(new Cell(setter, new Cell(pc.Cdr is Cell cc ? cc.Car : Const.NIL, new Cell(val, Const.NIL))), env);
            }
        }
        throw new Exception($"set!: invalid place: {place}");
    }

    private static object? HAnd(object? args, Env env)
    {
        if (args is not Cell a) return Const.TRUE;
        var last = a;
        while (last is Cell cc)
        {
            if (cc.Cdr is Nil)
            {
                var result = Eval(cc.Car, env);
                if (result is Sym s && s == Const.FALSE) return Const.FALSE;
                return result;
            }
            var r = Eval(cc.Car, env);
            if (r is Sym s2 && s2 == Const.FALSE) return Const.FALSE;
            last = cc.Cdr as Cell ?? a;
        }
        return Const.TRUE;
    }

    private static object? HOr(object? args, Env env)
    {
        if (args is not Cell a) return Const.FALSE;
        var last = a;
        while (last is Cell cc)
        {
            if (cc.Cdr is Nil)
            {
                return new TailCall(cc.Car, env);
            }
            var r = Eval(cc.Car, env);
            if (r is Sym s && s == Const.FALSE) { last = cc.Cdr as Cell ?? a; continue; }
            return r;
        }
        return Const.FALSE;
    }

    private static object? HCond(object? args, Env env)
    {
        if (args is not Cell a) return Const.VOID;
        var cur = a;
        while (cur is Cell c)
        {
            if (c.Car is not Cell clause) return Const.VOID;
            var test = clause.Car;
            if (test is Sym s && s.Name == "else")
            {
                return SeqTailCall(clause.Cdr, env);
            }
            // Check for => form: (test => expression)
            if (clause.Cdr is Cell afterTest && afterTest.Car is Sym arrow && arrow.Name == "=>")
            {
                var expr = afterTest.Cdr is Cell e ? e.Car : Const.VOID;
                var arrowTestVal = Eval(test, env);
                if (arrowTestVal is Sym t2 && t2 == Const.FALSE)
                {
                    cur = (Cell)c.Cdr;
                    continue;
                }
                // Call the procedure with testVal as argument
                return new TailCall(new Cell(expr, new Cell(arrowTestVal, Const.NIL)), env);
            }
            var testVal = Eval(test, env);
            if (testVal is Sym t && t == Const.FALSE)
            {
                cur = (Cell)c.Cdr;
                continue;
            }
            if (clause.Cdr is Nil)
                return testVal;
            return SeqTailCall(clause.Cdr, env);
        }
        return Const.VOID;
    }

    private static object? HLet(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad let form");
        var bindings = a.Car;
        var body = a.Cdr;
        if (bindings is Sym name && body is Cell)
        {
            // Named let: (let name ((var val) ...) body ...)
            return HLetrec(args, env);
        }
        if (bindings is not Cell) throw new Exception("bad let bindings");
        var vars = new List<string>();
        var vals = new List<object?>();
        var cur = bindings;
        while (cur is Cell bc)
        {
            if (bc.Car is not Cell bind || bind.Cdr is Nil)
                throw new Exception("bad let binding");
            vars.Add(bind.Car.AsString());
            vals.Add(Eval(((Cell)bind.Cdr).Car, env));
            cur = bc.Cdr;
        }
        var nenv = new Env(env, vars.Count);
        for (int i = 0; i < vars.Count; i++)
            nenv.Data[vars[i]] = vals[i];
        return SeqTailCall(body, nenv);
    }

    private static object? HLetStar(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad let* form");
        var bindings = a.Car;
        var body = a.Cdr;
        if (bindings is not Cell) throw new Exception("bad let* bindings");
        var curEnv = env;
        var cur = bindings;
        while (cur is Cell bc)
        {
            if (bc.Car is not Cell bind || bind.Cdr is Nil)
                throw new Exception("bad let* binding");
            var var = bind.Car.AsString();
            var val = Eval(((Cell)bind.Cdr).Car, curEnv);
            curEnv = new Env(curEnv) { Data = { [var] = val } };
            cur = bc.Cdr;
        }
        return SeqTailCall(body, curEnv);
    }

    private static object? HLetrec(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad letrec form");
        var first = a.Car;
        string? loopName = null;
        Cell? bindings = null;
        Cell? body = null;
        if (first is Sym name)
        {
            // Named letrec/let
            loopName = name.Name;
            var cdrCell = a.Cdr as Cell;
            bindings = cdrCell?.Car as Cell;
            body = cdrCell?.Cdr as Cell;
        }
        else
        {
            bindings = first as Cell;
            body = a.Cdr as Cell;
        }
        if (bindings is not Cell) throw new Exception("bad letrec bindings");
        var nenv = new Env(env, 0);
        // First pass: bind all vars to #f
        var vars = new List<string>();
        var cur = bindings;
        while (cur is Cell bc)
        {
            if (bc.Car is not Cell bind || bind.Cdr is Nil)
                throw new Exception("bad letrec binding");
            vars.Add(bind.Car.AsString());
            nenv.Data[bind.Car.AsString()] = Const.FALSE;
            cur = bc.Cdr as Cell;
        }
        // Second pass: evaluate init expressions and update bindings
        cur = bindings;
        while (cur is Cell bc)
        {
            if (bc.Car is not Cell bind || bind.Cdr is Nil)
                throw new Exception("bad letrec binding");
            var var = bind.Car.AsString();
            var val = Eval(((Cell)bind.Cdr).Car, nenv);
            nenv.Data[var] = val;
            cur = bc.Cdr as Cell;
        }
        if (loopName is not null)
        {
            // Named let: create lambda and bind loopName to it
            var lambdaBody = new Cell(Sym.BEGIN, body);
            var lambda = new LambdaProc(vars, lambdaBody, nenv, true, loopName);
            nenv.Data[loopName] = lambda;
            return SeqTailCall(body, nenv);
        }
        return SeqTailCall(body, nenv);
    }

    private static object? HDefineMacro(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad define-macro form");
        if (a.Car is Cell pat)
        {
            var name = pat.Car.AsString();
            var @params = new List<string>();
            var cur = pat.Cdr;
            bool hasRest = false;
            while (cur is Cell pc) { @params.Add(pc.Car.AsString()); cur = pc.Cdr; }
            if (cur is not Nil) { @params.Add("rest:" + (cur as Sym)?.Name ?? cur?.ToString() ?? ""); hasRest = true; }
            env.Data[name] = ("macro", @params, a.Cdr, env, !hasRest);
        }
        else
            env.Data[a.Car.AsString()] = ("macro", new List<string>(), a.Cdr, env, true);
        return Sym.Intern(a.Car.AsString());
    }

    // ── Helpers ──

    private static object? StripSyntax(object? v)
    {
        while (v is SyntaxObject so) v = so.Expr;
        if (v is Cell c) return new Cell(StripSyntax(c.Car), StripSyntax(c.Cdr));
        return v;
    }

    public static object? SeqTailCall(object? seq, Env env)
    {
        if (seq is Nil) return Const.VOID;
        var cur = seq;
        while (cur is Cell c && c.Cdr is Cell)
        {
            Eval(c.Car, env);
            cur = c.Cdr;
        }
        if (cur is Cell last) return new TailCall(last.Car, env);
        return Eval(cur, env);
    }

    private static object? TailCallLoop(object? expr, Env env)
    {
        var e = expr;
        var envPtr = env;
        while (true)
        {
            var result = EvalCore(e, envPtr);
            if (result is TailCall tc) { e = tc.Expr; envPtr = tc.Env; continue; }
            return result;
        }
    }

    public static object? Eval(object? expr, Env env) => EvalCore(expr, env);

    internal static object? EvalCore(object? expr, Env env)
    {
        while (true)
        {
            // Symbol
            if (expr is Sym sym)
            {
                if (sym == Const.TRUE || sym == Const.FALSE) return expr;
                return env.Lookup(sym);
            }

            // Non-list
            if (expr is not Cell cell)
            {
                if (expr == Const.TRUE || expr == Const.FALSE) return expr;
                if (expr is Nil or Void or Eof) return expr;
                if (expr is SyntaxObject so) return so.Expr;
                return expr;
            }

            // Special forms
            var op = cell.Car;
            var args = cell.Cdr;

            var handler = op is Sym osp ? Specials.GetValueOrDefault(osp) : null;
            if (handler is not null)
            {
                var r = handler(args, env);
                if (r is TailCall tc) { expr = tc.Expr; env = tc.Env; continue; }
                return r;
            }

            // Macro expansion
            object? proc;
            if (op is Sym ops)
            {
                proc = env.LookupSilent(ops.Name, UnboundSentinel);
                if (!ReferenceEquals(proc, UnboundSentinel))
                {
                    var newExpr = ExpandMacro(proc, cell, args, env);
                    if (newExpr is not null) { expr = newExpr; continue; }
                }
                else
                    proc = EvalCore(op, env);
            }
            else
                proc = EvalCore(op, env);

                var curArgs = args;

                // LambdaProc
                if (proc is LambdaProc lp)
            {
                // JIT compilation trigger
                if (!IsCompiling && lp.CompiledVersion is null && lp.Name is not null)
                {
                    lp.CallCount++;
                    if (lp.CallCount >= 1) // compile on first call
                    {
                        IsCompiling = true;
                        try
                        {
                            var compiled = Miniscm.Compiler.Compiler.CompileLambdaProc(lp);
                            if (compiled is not null)
                                lp.CompiledVersion = compiled;
                        }
                        catch
                        {
                        }
                        finally
                        {
                            IsCompiling = false;
                        }
                    }
                }

                if (lp.CompiledVersion is CompiledLambda cv)
                {
                    var argsArr = EvalArgsToArray(curArgs, env);
                    var r2 = cv.Invoke(lp.ClosureEnv, argsArr);
                    if (r2 is TailCall tc2) { expr = tc2.Expr; env = tc2.Env; continue; }
                    return r2;
                }

                var nenv = new Env(lp.ClosureEnv, lp.Params.Count);
                BindParams(lp.Params, EvalArgsToArray(curArgs, env), nenv);
                var r3 = SeqTailCall(lp.Body, nenv);
                if (r3 is TailCall tc3) { expr = tc3.Expr; env = tc3.Env; continue; }
                return r3;
            }

            var evaledArgs = EvalArgsToArray(curArgs, env);

            // Primitive functions (Func<object?[], object?>)
            if (proc is Func<object?[], object?> fn)
            {
                var r3 = fn(evaledArgs);
                if (r3 is TailCall tc3) { expr = tc3.Expr; env = tc3.Env; continue; }
                return r3;
            }

            // Other delegate
            if (proc is Delegate d)
            {
                var r3 = d.DynamicInvoke(evaledArgs);
                if (r3 is TailCall tc3) { expr = tc3.Expr; env = tc3.Env; continue; }
                return r3;
            }

            // Tuple proc (macro, lambda)
            if (proc is System.Runtime.CompilerServices.ITuple it && it.Length >= 2 && it[0] is string t0)
            {
                if (t0 == "lambda")
                {
                    if (it.Length >= 5 && it[1] is List<string> lamParams && it[3] is Env le)
                    {
                        var nenv2 = new Env(le);
                        BindParams(lamParams, evaledArgs, nenv2);
                        var r4 = SeqTailCall(it[2], nenv2);
                        if (r4 is TailCall tc4) { expr = tc4.Expr; env = tc4.Env; continue; }
                        return r4;
                    }
                }
            }

            throw new Exception($"not callable: {Printer.Format(proc)}");
        }
    }

    private static int _expandDepth = 0;
    private static object? ExpandMacro(object? proc, Cell expr, object? args, Env env)
    {
        if (proc is System.Runtime.CompilerServices.ITuple it && it.Length >= 2 && it[0] is string p0)
        {
            if (p0 == "macro" && it.Length >= 5 && it[1] is List<string> mparams
                && it[3] is Env)
            {
                _expandDepth++;
                var macroName = (expr.Car as Sym)?.Name ?? "?";
                // if (_expandDepth <= 5)
                //     Console.Error.WriteLine($"[ExpandMacro] depth={_expandDepth} name={macroName} args={Printer.Format(args)}");
                // if (_expandDepth > 200)
                // {
                //     Console.Error.WriteLine($"[ExpandMacro] STACK OVERFLOW GUARD depth={_expandDepth} name={macroName}");
                //     _expandDepth--;
                //     throw new Exception($"syntax-rules: infinite expansion of '{macroName}'");
                // }
                var mbody = it[2];
                var nenv = new Env(env);
                var al = new List<object?>();
                var cur = args;
                while (cur is Cell c) { al.Add(c.Car); cur = c.Cdr; }
                BindParams(mparams, al, nenv);
                var savedDepth = _expandDepth;
                _expandDepth = 0;
                var r = EvalSeq(mbody, nenv);
                while (r is TailCall tcr) r = EvalCore(tcr.Expr, tcr.Env);
                _expandDepth = savedDepth;
                // if (_expandDepth <= 3)
                //     Console.Error.WriteLine($"[ExpandMacro] result={Printer.Format(r)}");
                return (r as SyntaxObject)?.Expr ?? r;
            }
        }
        return null;
    }

    public static object? EvalSeq(object? seq, Env env)
    {
        object? r = Const.VOID;
        var cur = seq;
        while (cur is Cell c) { r = EvalCore(c.Car, env); cur = c.Cdr; }
        return r;
    }

    public static object?[] EvalArgsToArray(object? args, Env env)
    {
        int cnt = 0;
        var cur = args;
        while (cur is Cell) { cnt++; cur = ((Cell)cur).Cdr; }
        var arr = new object?[cnt];
        cur = args;
        for (int i = 0; i < cnt; i++)
        {
            var c = (Cell)cur;
            arr[i] = EvalCore(c.Car, env);
            cur = c.Cdr;
        }
        return arr;
    }

    public static List<object?> EvalArgsToList(object? args, Env env)
    {
        var evaled = new List<object?>();
        var cur = args;
        while (cur is Cell c) { evaled.Add(EvalCore(c.Car, env)); cur = c.Cdr; }
        return evaled;
    }

    public static void BindParams(List<string> @params, object?[] evaledArgs, Env nenv)
    {
        int pi = 0;
        for (int i = 0; i < @params.Count; i++)
        {
            var p = @params[i];
            if (p.StartsWith("rest:"))
            {
                int len = evaledArgs.Length - pi;
                object? rest = Const.NIL;
                for (int j = evaledArgs.Length - 1; j >= pi; j--)
                    rest = new Cell(evaledArgs[j], rest);
                nenv.Data[p[5..]] = rest;
                break;
            }
            if (pi < evaledArgs.Length) nenv.Data[p] = evaledArgs[pi];
            pi++;
        }
    }

    public static void BindParams(List<string> @params, List<object?> evaledArgs, Env nenv)
    {
        int pi = 0;
        foreach (var p in @params)
        {
            if (p.StartsWith("rest:"))
            {
                nenv.Data[p[5..]] = evaledArgs.GetRange(pi, evaledArgs.Count - pi).ToCell();
                break;
            }
            if (pi < evaledArgs.Count) nenv.Data[p] = evaledArgs[pi];
            pi++;
        }
    }
}
