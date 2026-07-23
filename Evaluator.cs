using Miniscm.Types;
using Miniscm.Macro;
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
        while (expr is Cell cell && cell.Car is Sym opSym)
        {
            if (levelSeen.Contains(expr) || seen.Contains(expr))
                break;
            levelSeen.Add(expr);
            seen.Add(expr);
            var proc = env.LookupSilent(opSym.Name, UnboundSentinel);
            if (ReferenceEquals(proc, UnboundSentinel))
                break;
            var expanded = ExpandMacro(proc, cell, cell.Cdr, env);
            if (expanded is null)
                break;
            expr = expanded;
        }
        if (expr is Cell c)
        {
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
        Put(Sym.QQ, HQQ);
        Put(Sym.QS, HQS);
        Put(Sym.UNQUOTE, HUnquote);
        Put(Sym.UNSPLICE, HUnquote);
        Put(Sym.USYNTAX, HUnquote);
        Put(Sym.SR, HSyntaxRules);
        Put(Sym.DM, HDefineMacro);
        Put(Sym.DS, HDefineSyntax);
        Put(Sym.LS, HLetSyntax);
        Put(Sym.LRS, HLetrecSyntax);
        Put(Sym.IMPORT, HImport);
        Put(Sym.SYNTAX, HSyntax);
        Put(Sym.SC, HSyntaxCase);
        Put(Sym.WS, HWithSyntax);
        Put(Sym.GT, HGenerateTemporaries);
        Put(Sym.DEBUG, HDebug);
        Put(Sym.DBGTRACE, HDebugTrace);
    }

    // ── Special Forms ──

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

    private static object? HQQ(object? args, Env env) => QQ(args is Cell c ? c.Car : Const.NIL, env);

    private static object? HQS(object? args, Env env)
    {
        var expr = args is Cell c ? c.Car : Const.NIL;
        var expanded = TemplateExpander.ScExpandSyntax(expr, env);
        return new SyntaxObject(QQ(expanded, env));
    }

    private static object? HSyntaxRules(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad syntax-rules form");
        var lits = a.Car;
        var rules = new List<Cell>();
        var cur = a.Cdr;
        while (cur is Cell rc) { if (rc.Car is Cell r) rules.Add(r); cur = rc.Cdr; }
        return new SyntaxTrans(lits, rules, env);
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

    private static object? HDefineSyntax(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad define-syntax form");
        var name = a.Car.AsString();
        var trans = Eval(a.Cdr is Cell c ? c.Car : Const.NIL, env);
        if (trans is SyntaxTrans st) env.Data[name] = st;
        else if (trans is ValueTuple<string, object?> && trans is ("lambda", _, _, _)) env.Data[name] = trans;
        else if (trans is LambdaProc || trans is Delegate) env.Data[name] = ("syntax-macro-callable", trans);
        return Sym.Intern(name);
    }

    private static object? HLetSyntax(object? args, Env env)
    {
        if (args is not Cell a) return Const.VOID;
        var bindings = a.Car;
        var body = a.Cdr;
        var nenv = new Env(env);
        var cur = bindings;
        while (cur is Cell bc)
        {
            var b = bc.Car;
            if (b is Cell cb)
            {
                var n = cb.Car;
                var texpr = cb.Cdr is Cell tc ? tc.Car : Const.NIL;
                nenv.Data[n.AsString()] = Eval(texpr, env);
            }
            cur = bc.Cdr;
        }
        return SeqTailCall(body, nenv);
    }

    private static object? HLetrecSyntax(object? args, Env env)
    {
        if (args is not Cell a) return Const.VOID;
        var bindings = a.Car;
        var body = a.Cdr;
        var nenv = new Env(env);
        var cur = bindings;
        while (cur is Cell bc)
        {
            var b = bc.Car;
            if (b is Cell cb)
            {
                var n = cb.Car;
                var texpr = cb.Cdr is Cell tc ? tc.Car : Const.NIL;
                nenv.Data[n.AsString()] = Eval(texpr, nenv);
            }
            cur = bc.Cdr;
        }
        return SeqTailCall(body, nenv);
    }

    private static object? HImport(object? args, Env env) => Const.VOID;

    private static object? HSyntax(object? args, Env env)
    {
        var expr = args is Cell c ? c.Car : Const.NIL;
        var b = TemplateExpander.ScCollectPatternBindings(env);
        return new SyntaxObject(TemplateExpander.ExpandTmpl(expr, b, null));
    }

    private static object? HSyntaxCase(object? args, Env env)
    {
        if (args is not Cell a) throw new Exception("bad syntax-case form");
        var exprVal = Eval(a.Car, env);
        var literals = a.Cdr is Cell c ? c.Car : Const.NIL;
        var clauses = a.Cdr is Cell c2 ? c2.Cdr : Const.NIL;
        var datum = (exprVal as SyntaxObject)?.Expr ?? exprVal;

        var lits = new HashSet<string>();
        var curLit = literals;
        while (curLit is Cell lc) { var l = lc.Car; if (l is Sym) lits.Add(l.AsString()); curLit = lc.Cdr; }

        var curClause = clauses;
        while (curClause is Cell cc)
        {
            var clause = cc.Car;
            if (clause is Cell cl)
            {
                var pat = cl.Car;
                var restCl = cl.Cdr;
                var hasFender = restCl is Cell rc && rc.Cdr is Cell && rc.Cdr is not Nil;
                var fender = hasFender ? (restCl is Cell r1 ? r1.Car : Const.NIL) : null;
                var tmpl = hasFender ? (restCl is Cell r1a ? (r1a.Cdr is Cell r2 ? r2.Car : Const.NIL) : Const.NIL)
                                      : (restCl is Cell r3 ? r3.Car : Const.NIL);

                var b = PatternMatcher.Match(pat, datum, lits);
                if (b is not null)
                {
                    var nenv = new Env(env);
                    foreach (var (k, v) in b)
                        nenv.Define(Sym.Intern(k), new SyntaxObject(v));
                    if (hasFender)
                    {
                        if (fender is not null)
                        {
                            var fv = Eval(fender, nenv);
                            if (fv is Sym ft && ft == Const.FALSE) { curClause = cc.Cdr; continue; }
                        }
                    }
                    return new TailCall(tmpl, nenv);
                }
            }
            curClause = cc.Cdr;
        }
        throw new Exception("syntax-case: no match");
    }

    private static object? HWithSyntax(object? args, Env env)
    {
        if (args is not Cell a) return Const.VOID;
        var bindings = a.Car;
        var body = a.Cdr;
        var nenv = new Env(env);
        var cur = bindings;
        while (cur is Cell bc)
        {
            var b = bc.Car;
            if (b is Cell cb)
            {
                var pat = cb.Car;
                var expr = cb.Cdr is Cell ex ? ex.Car : Const.NIL;
                var val = Eval(expr, env);
                var valSo = (val as SyntaxObject)?.Expr ?? val;
                var result = PatternMatcher.Match(pat, valSo, []);
                if (result is not null)
                {
                    foreach (var (k, v) in result)
                        nenv.Data[Sym.Intern(k)] = new SyntaxObject(v);
                }
            }
            cur = bc.Cdr;
        }
        return SeqTailCall(body, nenv);
    }

    private static object? HGenerateTemporaries(object? args, Env env)
    {
        var lst = args is Cell c ? Eval(c.Car, env) : Const.NIL;
        lst = (lst as SyntaxObject)?.Expr ?? lst;
        if (lst is Cell lc)
        {
            var items = new List<object?>();
            object? cur = lc;
            while (cur is Cell cc)
            {
                Const.GensymCounter++;
                items.Add(new SyntaxObject(Sym.Intern($"__t{Const.GensymCounter}")));
                cur = cc.Cdr;
            }
            return items.ToCell();
        }
        return Const.NIL;
    }

    private static object? HDebug(object? args, Env env) => Const.VOID;

    private static object? HDebugTrace(object? args, Env env) => Const.VOID;

    // ── Quasiquote ──

    private static object? QQ(object? e, Env env)
    {
        if (e is Cell cell)
        {
            var items = new List<object?>();
            object? tail = Const.NIL;
            var cur = e;
            while (cur is Cell cc)
            {
                var el = cc.Car;
                if (el is Cell elCell)
                {
                    var c = elCell.Car;
                    if (c == Sym.UNQUOTE || c == Sym.USYNTAX)
                        items.Add(Eval(elCell.Cdr is Cell uq ? uq.Car : Const.NIL, env));
                    else if (c == Sym.UNSPLICE || c == Sym.USPLICES)
                    {
                        var v = Eval(elCell.Cdr is Cell us ? us.Car : Const.NIL, env);
                        v = (v as SyntaxObject)?.Expr ?? v;
                        if (v is Cell vc) { foreach (var x in vc) items.Add(x); }
                        else if (v is not Nil) items.Add(v);
                    }
                    else if (c == Sym.QQ) items.Add(el);
                    else items.Add(QQ(el, env));
                }
                else items.Add(QQ(el, env));

                var curCdr = cc.Cdr;
                if (curCdr is Cell cc2)
                {
                    var cc2c = cc2.Car;
                    if (cc2c == Sym.UNQUOTE || cc2c == Sym.USYNTAX)
                    {
                        var v = Eval(cc2.Cdr is Cell uq2 ? uq2.Car : Const.NIL, env);
                        v = (v as SyntaxObject)?.Expr ?? v;
                        for (int i = items.Count - 1; i >= 0; i--) v = new Cell(items[i], v);
                        return v;
                    }
                    if (cc2c == Sym.UNSPLICE || cc2c == Sym.USPLICES)
                    {
                        var v = Eval(cc2.Cdr is Cell us2 ? us2.Car : Const.NIL, env);
                        v = (v as SyntaxObject)?.Expr ?? v;
                        if (v is Cell vc2) { foreach (var x in vc2) items.Add(x); }
                        else if (v is not Nil) items.Add(v);
                        cur = curCdr;
                        continue;
                    }
                }
                cur = curCdr;
            }
            var r = cur is not Nil ? cur : Const.NIL;
            for (int i = items.Count - 1; i >= 0; i--) r = new Cell(items[i], r);
            return r;
        }
        if (e is SchemeVector sv)
        {
            var newData = new List<object?>();
            foreach (var el in sv.Data)
            {
                if (el is Cell elCell)
                {
                    var c = elCell.Car;
                    if (c == Sym.UNQUOTE || c == Sym.USYNTAX)
                        newData.Add(Eval(elCell.Cdr is Cell uq ? uq.Car : Const.NIL, env));
                    else if (c == Sym.UNSPLICE || c == Sym.USPLICES)
                    {
                        var v = Eval(elCell.Cdr is Cell us ? us.Car : Const.NIL, env);
                        v = (v as SyntaxObject)?.Expr ?? v;
                        if (v is Cell vc) { foreach (var x in vc) newData.Add(x); }
                        else if (v is not Nil) newData.Add(v);
                    }
                    else newData.Add(QQ(el, env));
                }
                else newData.Add(QQ(el, env));
            }
            return new SchemeVector(newData);
        }
        return e is Nil ? Const.NIL : e;
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

            // Tuple proc (macro, lambda, syntax-macro-callable)
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
                // syntax-macro-callable handled during macro expansion
            }

            throw new Exception($"not callable: {Printer.Format(proc)}");
        }
    }

    private static object? ExpandMacro(object? proc, Cell expr, object? args, Env env)
    {
        if (proc is System.Runtime.CompilerServices.ITuple it && it.Length >= 2 && it[0] is string p0)
        {
            if (p0 == "macro" && it.Length >= 5 && it[1] is List<string> mparams
                && it[3] is Env mpenv)
            {
                var mbody = it[2];
                var nenv = new Env(mpenv);
                var al = new List<object?>();
                var cur = args;
                while (cur is Cell c) { al.Add(c.Car); cur = c.Cdr; }
                BindParams(mparams, al, nenv);
                var r = EvalSeq(mbody, nenv);
                while (r is TailCall tcr) r = EvalCore(tcr.Expr, tcr.Env);
                return (r as SyntaxObject)?.Expr ?? r;
            }
            if (p0 == "syntax-macro-callable" && it.Length == 2)
            {
                var callable = it[1];
                object? r;
                if (callable is LambdaProc lp)
                {
                    var nenv = new Env(lp.ClosureEnv);
                    BindParams(lp.Params, new object?[] { expr }, nenv);
                    r = SeqTailCall(lp.Body, nenv);
                }
                else if (callable is Delegate d)
                {
                    r = d.DynamicInvoke(expr);
                }
                else
                {
                    return null;
                }
                while (r is TailCall tcr) r = EvalCore(tcr.Expr, tcr.Env);
                return (r as SyntaxObject)?.Expr ?? r;
            }
        }
            if (proc is SyntaxTrans st)
            {
                return TemplateExpander.ApplyStx(st, expr);
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
