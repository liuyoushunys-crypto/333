using Miniscm.Types;

namespace Miniscm.Compiler;

// 原生 syntax-rules 编译器 — 与 miniscm/native_syntax.py 等价。
// 把 syntax-rules 宏直接编译成原生 C# 模式匹配+模板展开器,
// 绕过 Scheme 宏引擎 (boot-min2.scm 的 sx-* 系列), 展开时零解释器参与。
public static class NativeSyntax
{
    static readonly Sym UND = Sym.Intern("_");
    static readonly Sym ELL = Sym.Intern("...");
    static readonly Sym SX_HYGIENE = Sym.Intern("sx-hygiene");
    static readonly Sym SETBANG = Sym.Intern("set!");

    internal static bool IsProcedure(object? v) =>
        v is Delegate or LambdaProc or CompiledLambda or System.Runtime.CompilerServices.ITuple;

    internal static bool SchemeEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is Nil || b is Nil) return a is Nil && b is Nil;
        if (a is Sym sa && b is Sym sb) return sa == sb;
        if (a is Cell ca && b is Cell cb)
            return SchemeEqual(ca.Car, cb.Car) && SchemeEqual(ca.Cdr, cb.Cdr);
        return Equals(a, b);
    }

    internal static int Length(object? cell)
    {
        int n = 0;
        var cur = cell;
        while (cur is Cell c) { n++; cur = c.Cdr; }
        return n;
    }

    internal static object? ListRef(object? cell, int i)
    {
        var cur = cell;
        for (int k = 0; k < i; k++) cur = ((Cell)cur!).Cdr;
        return ((Cell)cur!).Car;
    }

    internal static bool Memq(Sym x, List<object?> lst)
    {
        foreach (var it in lst)
            if (it is Sym s && s == x) return true;
        return false;
    }

    // ── 模式匹配 (sx-match 等价) ─────────────────────────────

    internal static List<(Sym, object?)>? SxMatch(object? pat, object? inp, List<object?> lits)
    {
        if (pat is Nil) return inp is Nil ? [] : null;
        if (pat is Sym ps) return SxMatchSym(ps, inp, lits);
        if (pat is not Cell pc) return SchemeEqual(pat, inp) ? [] : null;
        if (pc.Cdr is Cell pcd && pcd.Car is Sym ell && ell == ELL)
            return SxMatchEllipsis(pc.Car, pcd.Cdr, inp, lits);
        return SxMatchPair(pc, inp, lits);
    }

    internal static List<(Sym, object?)>? SxMatchSym(Sym pat, object? inp, List<object?> lits)
    {
        if (pat == UND) return [];
        if (Memq(pat, lits))
            return inp is Sym ins && ins == pat ? [] : null;
        return [(pat, inp)];
    }

    internal static List<(Sym, object?)>? SxMatchPair(Cell pat, object? inp, List<object?> lits)
    {
        if (inp is not Cell inCell) return null;
        var b1 = SxMatch(pat.Car, inCell.Car, lits);
        if (b1 is null) return null;
        var b2 = SxMatch(pat.Cdr, inCell.Cdr, lits);
        if (b2 is null) return null;
        var result = new List<(Sym, object?)>(b2);
        result.AddRange(b1);
        return result;
    }

    internal static List<(Sym, object?)>? SxMatchEllipsis(object? prefix, object? restPat, object? inp, List<object?> lits)
    {
        var res = SxMatchEllipsisLoop(prefix, restPat, inp, lits, []);
        return SxMatchEllipsisFinish(prefix, restPat, res, lits);
    }

    internal static (object? Remaining, List<List<(Sym, object?)>> Groups) SxMatchEllipsisLoop(
        object? prefix, object? restPat, object? inp, List<object?> lits, List<List<(Sym, object?)>> groups)
    {
        if (inp is not Cell inCell) return (inp, groups);
        var b = SxMatch(prefix, inCell.Car, lits);
        if (b is not null)
        {
            if (restPat is Nil)
                return SxMatchEllipsisLoop(prefix, restPat, inCell.Cdr, lits, [.. groups, b]);
            if (SxMatch(restPat, inp, lits) is not null)
                return (inp, groups);
            return SxMatchEllipsisLoop(prefix, restPat, inCell.Cdr, lits, [.. groups, b]);
        }
        return (inp, groups);
    }

    internal static List<(Sym, object?)>? SxMatchEllipsisFinish(object? prefix, object? restPat,
        (object? Remaining, List<List<(Sym, object?)>> Groups) res, List<object?> lits)
    {
        var evars = SxPatternVars(prefix);
        if (restPat is Nil)
            return res.Remaining is Nil ? SxAccumEllipsis(evars, res.Groups, []) : null;
        var rb = SxMatch(restPat, res.Remaining, lits);
        return rb is not null ? SxAccumEllipsis(evars, res.Groups, rb) : null;
    }

    internal static List<Sym> SxPatternVars(object? pat)
    {
        var stack = new Stack<object?>();
        stack.Push(pat);
        var acc = new List<Sym>();
        while (stack.Count > 0)
        {
            var curr = stack.Pop();
            if (curr is Sym s)
            {
                if (s != UND && s != ELL) acc.Add(s);
            }
            else if (curr is Cell c)
            {
                stack.Push(c.Cdr);
                stack.Push(c.Car);
            }
        }
        return acc;
    }

    internal static List<(Sym, object?)> SxAccumEllipsis(List<Sym> vars,
        List<List<(Sym, object?)>> groups, List<(Sym, object?)> baseBindings)
    {
        if (vars.Count == 0) return baseBindings;
        var v = vars[0];
        var vals = new List<object?>();
        foreach (var g in groups)
        {
            int idx = g.FindIndex(b => b.Item1 == v);
            vals.Add(idx >= 0 ? g[idx].Item2 : Const.NIL);
        }
        vals.Reverse();
        object? lst = Const.NIL;
        foreach (var x in vals) lst = new Cell(x, lst);
        var rest = SxAccumEllipsis(vars.GetRange(1, vars.Count - 1), groups, baseBindings);
        return [.. rest, (v, lst)];
    }

    // ── 模板展开 (sx-expand 等价) ─────────────────────────────

    internal static object? SxExpand(object? tmpl, List<(Sym, object?)> bindings,
        List<Sym> mutated, Env defEnv)
    {
        if (tmpl is Sym ts) return SxExpandSym(ts, bindings, mutated, defEnv);
        if (tmpl is not Cell tc) return tmpl;
        if (tc.Cdr is Cell tcd && tcd.Car is Sym ell && ell == ELL)
            return SxExpandEllipsis(tc.Car, tcd.Cdr, bindings, mutated, defEnv);
        return SxExpandPair(tc, bindings, mutated, defEnv);
    }

    internal static object? SxExpandPair(Cell tmpl, List<(Sym, object?)> bindings,
        List<Sym> mutated, Env defEnv) =>
        new Cell(SxExpand(tmpl.Car, bindings, mutated, defEnv),
            SxExpand(tmpl.Cdr, bindings, mutated, defEnv));

    internal static object? SxExpandSym(Sym tmpl, List<(Sym, object?)> bindings,
        List<Sym> mutated, Env defEnv)
    {
        var p = bindings.FindIndex(b => b.Item1 == tmpl);
        if (p >= 0) return bindings[p].Item2;
        if (tmpl == UND || tmpl == ELL) return tmpl;
        if (mutated.Contains(tmpl)) return tmpl;
        // LookupSilent 默认 sentinel 为 null: 未绑定返回 null
        var v = defEnv.LookupSilent(tmpl.Name);
        if (v is not null && !IsProcedure(v))
            return new Cell(SX_HYGIENE, new Cell(tmpl, Const.NIL));
        return tmpl;
    }

    internal static object? SxExpandEllipsis(object? sub, object? rest, List<(Sym, object?)> bindings,
        List<Sym> mutated, Env defEnv)
    {
        var evars = SxEllipsisVars(sub, bindings);
        int cnt;
        if (evars.Count > 0)
        {
            var p = bindings.FindIndex(b => b.Item1 == evars[0]);
            cnt = p >= 0 ? Length(bindings[p].Item2) : 0;
        }
        else
        {
            cnt = SxFindListCount(bindings);
        }
        return SxRepeat(sub, rest, bindings, evars, cnt, mutated, defEnv);
    }

    internal static List<Sym> SxEllipsisVars(object? sub, List<(Sym, object?)> bindings)
    {
        var out_ = new List<Sym>();
        foreach (var v in SxPatternVars(sub))
        {
            var p = bindings.FindIndex(b => b.Item1 == v);
            if (p >= 0 && (bindings[p].Item2 is Cell || bindings[p].Item2 is Nil))
                out_.Add(v);
        }
        return out_;
    }

    internal static int SxFindListCount(List<(Sym, object?)> bindings)
    {
        foreach (var (_, val) in bindings)
            if (val is Cell) return Length(val);
        return 0;
    }

    internal static object? SxRepeat(object? sub, object? rest, List<(Sym, object?)> bindings,
        List<Sym> evars, int cnt, List<Sym> mutated, Env defEnv)
    {
        object? res = SxExpand(rest, bindings, mutated, defEnv);
        for (int i = cnt - 1; i >= 0; i--)
        {
            if (evars.Count > 0)
            {
                var subB = SxSubBindings(evars, bindings, i);
                res = new Cell(SxExpand(sub, subB, mutated, defEnv), res);
            }
            else
            {
                res = new Cell(SxExpand(sub, bindings, mutated, defEnv), res);
            }
        }
        return res;
    }

    internal static List<(Sym, object?)> SxSubBindings(List<Sym> evars, List<(Sym, object?)> bindings, int i)
    {
        var out_ = new List<(Sym, object?)>();
        foreach (var v in evars)
        {
            var p = bindings.FindIndex(b => b.Item1 == v);
            var lst = p >= 0 ? bindings[p].Item2 : Const.NIL;
            int len = Length(lst);
            out_.Add((v, i < len ? ListRef(lst, i) : Const.NIL));
        }
        return out_;
    }

    // ── set! 变异收集 (sx-collect-set-targets 等价) ─────────────

    internal static List<Sym> SxCollectSetTargets(object? tmpl, List<Sym> acc)
    {
        if (tmpl is not Cell c) return acc;
        if (c.Car is Sym s && s == SETBANG)
        {
            if (c.Cdr is Cell cdr2)
            {
                if (cdr2.Car is Sym target)
                    return SxCollectSetTargets(cdr2, [.. acc, target]);
                return SxCollectSetTargets(cdr2, acc);
            }
            return acc;
        }
        return SxCollectSetTargets(c.Car, SxCollectSetTargets(c.Cdr, acc));
    }

    // ── 编译器入口 ──────────────────────────────────────────

    public static Func<object?, object?>? CompileSyntaxRules(object? lits, object? rules, Env defEnv)
    {
        var ruleList = new List<(object? PatArgs, object? Tmpl, List<Sym> Mutated)>();
        var cur = rules;
        while (cur is Cell rc)
        {
            if (rc.Car is Cell rule)
            {
                var pat = rule.Car;
                var tmpl = rule.Cdr is Cell rcd ? rcd.Car : Const.NIL;
                var patArgs = pat is Cell pc ? pc.Cdr : Const.NIL;
                var mutated = SxCollectSetTargets(tmpl, []);
                ruleList.Add((patArgs, tmpl, mutated));
            }
            cur = rc.Cdr;
        }
        var litsList = new List<object?>();
        cur = lits;
        while (cur is Cell lc) { litsList.Add(lc.Car); cur = lc.Cdr; }

        return args =>
        {
            var _dbg = System.Environment.GetEnvironmentVariable("SX_TRACE");
            if (_dbg == "1") System.Console.Error.WriteLine($"[SX-N] dispatch args={MinRef.SxPrint(args)}");
            foreach (var (patArgs, tmpl, mutated) in ruleList)
            {
                var b = SxMatch(patArgs, args, litsList);
                if (_dbg == "1") System.Console.Error.WriteLine($"[SX-N]   rule match={b is not null} patArgs={MinRef.SxPrint(patArgs)}");
                if (b is not null)
                    return SxExpand(tmpl, b, mutated, defEnv);
            }
            throw new Exception("syntax-rules: no match");
        };
    }
}
