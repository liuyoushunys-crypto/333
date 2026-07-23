using Miniscm.Types;

namespace Miniscm.Macro;

public static class TemplateExpander
{
    private static readonly HashSet<Sym> NoWalkForms = [Sym.QUOTE, Sym.QQ, Sym.SYNTAX, Sym.QS];

    public static object? ExpandTmpl(object? tmpl, Dictionary<string, object?> bindings, Dictionary<Sym, Sym>? renames)
    {
        if (tmpl is SyntaxObject so)
            return new SyntaxObject(ExpandTmpl(so.Expr, bindings, renames));
        if (tmpl is Sym s)
        {
            if (bindings.TryGetValue(s.Name, out var v)) return v;
            if (renames is not null && renames.TryGetValue(s, out var r)) return r;
            return s;
        }
        if (tmpl is not Cell c) return tmpl;

        var tCar = c.Car;
        var tCdr = c.Cdr;

        if (tCar == Sym.SYNTAX && tCdr is Cell synCell)
            return new SyntaxObject(ExpandTmpl(synCell.Car, bindings, renames));

        if (tCar == Sym.QS)
            return ExpandQuasisyntax(tCdr is Cell qsCell ? qsCell.Car : Const.NIL, bindings, null);

        if (tCar == Sym.USYNTAX && tCdr is Cell usCell)
        {
            var inner = ExpandTmpl(usCell.Car, bindings, renames);
            if (inner is SyntaxObject) return inner;
            try
            {
                return Eval.Evaluator.Eval(inner, Eval.Evaluator.GlobalEnv);
            }
            catch { return inner; }
        }

        if (tCar == Sym.USPLICES && tCdr is Cell splCell)
        {
            var inner = ExpandTmpl(splCell.Car, bindings, renames);
            if (inner is SyntaxObject) inner = ((SyntaxObject)inner).Expr;
            try
            {
                Eval.Evaluator.Eval(inner, Eval.Evaluator.GlobalEnv);
            }
            catch { }
        }

        if (tCdr is Cell ellCell && ellCell.Car == Sym.ELLIPSIS)
        {
            var sub = tCar;
            var rest = ellCell.Cdr;
            var evars = EllipsisVars2(sub, bindings);

            if (evars.Count == 0)
            {
                foreach (var (k, vv) in bindings)
                {
                    if (vv is Cell)
                    {
                        var cnt = vv.CellLength();
                        var result = ExpandTmpl(rest, bindings, renames);
                        for (int i = cnt - 1; i >= 0; i--)
                            result = new Cell(ExpandTmpl(sub, bindings, renames), result);
                        return result;
                    }
                }
                throw new Exception("no ellipsis var");
            }

            var evarLists = new Dictionary<string, List<object?>>();
            foreach (var v in evars)
                evarLists[v] = bindings.TryGetValue(v, out var bv) ? (bv is Cell c1 ? [.. c1] : []) : [];

            var cnt2 = evarLists.Count > 0 ? evarLists[evarLists.Keys.First()].Count : 0;
            var result2 = ExpandTmpl(rest, bindings, renames);
            for (int i = cnt2 - 1; i >= 0; i--)
            {
                var subBindings = new Dictionary<string, object?>(bindings);
                foreach (var v in evars)
                    subBindings[v] = i < evarLists[v].Count ? evarLists[v][i] : Const.NIL;
                result2 = new Cell(ExpandTmpl(sub, subBindings, renames), result2);
            }
            return result2;
        }

        return new Cell(ExpandTmpl(tCar, bindings, renames), ExpandTmpl(tCdr, bindings, renames));
    }

    private static List<string> EllipsisVars2(object? tmpl, Dictionary<string, object?> bindings)
    {
        return PatternMatcher.GetPatternVars2(tmpl)
            .Where(v => bindings.TryGetValue(v, out var bv) && (bv is Cell || bv is Nil))
            .ToList();
    }

    public static HashSet<Sym> FindFreeSyms(object? tmpl, Dictionary<string, object?> bindings, HashSet<string> litSet)
    {
        var free = new HashSet<Sym>();
        void Walk(object? node)
        {
            if (node is Sym sn)
            {
                if (!bindings.ContainsKey(sn.Name) && !litSet.Contains(sn.Name) && sn.Name != "_" && sn != Sym.ELLIPSIS)
                    free.Add(sn);
            }
            else if (node is Cell cc)
            {
                var car = cc.Car;
                if (!(car is Sym cs && NoWalkForms.Contains(cs)))
                { Walk(car); Walk(cc.Cdr); }
            }
        }
        Walk(tmpl);
        return free;
    }

    public static object? ApplyStx(SyntaxTrans trans, Cell form)
    {
        foreach (var rule in trans.Rules)
        {
            var pat = rule.Car;
            var tmpl = rule.Cdr is Cell tmplCell ? tmplCell.Car : Const.NIL;
            var b = PatternMatcher.Match(pat is Cell pc ? pc.Cdr : Const.NIL,
                                         form.Cdr, trans.LitNames);
            if (b is not null)
            {
                var free = FindFreeSyms(tmpl, b, trans.LitNames);
                var renames = new Dictionary<Sym, Sym>();
                var binds = new List<(Sym Gensym, object? Val)>();
                foreach (var s in free)
                {
                    if (s.Name.StartsWith("_")) continue;
                    try
                    {
                        var val = trans.Env.Lookup(s.Name);
                        if (val is SyntaxTrans or ValueTuple<string, object?> or LambdaProc) continue;
                        var specials = Eval.Evaluator.Specials;
                        if (specials.ContainsKey(s)) continue;
                        Const.GensymCounter++;
                        var gs = Sym.Intern($"{s.Name}:g{Const.GensymCounter}");
                        renames[s] = gs;
                        binds.Add((gs, val));
                    }
                    catch (NameError) { }
                }
                var result = ExpandTmpl(tmpl, b, renames);
                foreach (var (gs, val) in binds.AsEnumerable().Reverse())
                {
                    var quotedVal = new Cell(Sym.QUOTE, new Cell(val, Const.NIL));
                    result = new Cell(
                        new Cell(Sym.LAMBDA, new Cell(new Cell(gs, Const.NIL), new Cell(result, Const.NIL))),
                        new Cell(quotedVal, Const.NIL));
                }
                return result;
            }
        }
        throw new Exception("syntax-rules: no match");
    }

    public static object? ExpandQuasisyntax(object? e, Dictionary<string, object?>? b, Env? env)
    {
        if (e is Cell cell)
        {
            var items = new List<object?>();
            object? tail = Const.NIL;
            var cur = e;
            while (cur is Cell cc)
            {
                var el = cc.Car;
                var item = ExpandTmpl(el, b ?? [], null);
                if (item is SyntaxObject so && so.Expr is not Nil) item = so.Expr;
                items.Add(item);
                var nextCdr = cc.Cdr;
                if (nextCdr is Cell) { cur = nextCdr; }
                else { tail = nextCdr; break; }
            }
            var r = tail is not Nil ? tail : Const.NIL;
            for (int i = items.Count - 1; i >= 0; i--)
                r = new Cell(items[i], r);
            return new SyntaxObject(r);
        }
        var v = ExpandTmpl(e, b ?? [], null);
        return v is SyntaxObject ? v : (v is not Nil ? new SyntaxObject(v) : Const.NIL);
    }

    public static object? ScExpandSyntax(object? expr, Env env)
    {
        if (expr is SyntaxObject so)
            return new SyntaxObject(ScExpandSyntax(so.Expr, env));
        if (expr is Sym s)
        {
            try
            {
                var v = env.Lookup(s);
                if (v is SyntaxObject sov) return sov.Expr;
            }
            catch { }
            return s;
        }
        if (expr is not Cell c) return expr;
        var ec = c.Car;
        if (ec is Sym cs && (cs == Sym.USYNTAX || cs == Sym.USPLICES))
            return expr;
        return new Cell(ScExpandSyntax(ec, env), ScExpandSyntax(c.Cdr, env));
    }

    public static Dictionary<string, object?> ScCollectPatternBindings(Env env)
    {
        var b = new Dictionary<string, object?>();
        var cur = env;
        while (cur is not null)
        {
            foreach (var (k, v) in cur.Data)
                if (v is SyntaxObject && !b.ContainsKey(k))
                    b[k] = ((SyntaxObject)v).Expr;
            cur = cur.Parent;
        }
        return b;
    }
}
