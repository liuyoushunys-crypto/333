using Miniscm.Types;

namespace Miniscm.Macro;

public class SyntaxTrans
{
    public HashSet<string> LitNames { get; }
    public List<Cell> Rules { get; }
    public Env Env { get; }

    public SyntaxTrans(object? literals, List<Cell> rules, Env env)
    {
        var litList = new List<object?>();
        if (literals is Cell lc) litList.AddRange(lc);
        else if (literals is not Nil) litList.Add(literals);
        LitNames = [.. litList
            .Where(l => l is not Nil)
            .Select(l => (l as Sym)?.Name ?? l?.ToString() ?? "")];
        Rules = rules;
        Env = env;
    }
}

public static class PatternMatcher
{
    private static readonly Dictionary<int, List<string>> VarCache = [];

    public static List<string> GetPatternVars2(object? pat) => GetPatternVars(pat);

    private static List<string> GetPatternVars(object? pat)
    {
        if (pat is null) return [];
        var pid = pat.GetHashCode();
        if (VarCache.TryGetValue(pid, out var cached))
            return cached;

        var vars = new List<string>();
        var stack = new List<object?> { pat };
        while (stack.Count > 0)
        {
            var curr = stack[^1]; stack.RemoveAt(stack.Count - 1);
            if (curr is Sym s)
            {
                if (s.Name != "_" && s != Sym.ELLIPSIS)
                    vars.Add(s.Name);
            }
            else if (curr is Cell c)
            {
                stack.Add(c.Cdr);
                stack.Add(c.Car);
            }
        }
        VarCache[pid] = vars;
        return vars;
    }

    private static List<string> EllipsisVars(object? pat, Dictionary<string, object?> bindings)
    {
        return GetPatternVars(pat)
            .Where(v => bindings.TryGetValue(v, out var bv) && (bv is Cell || bv is Nil))
            .ToList();
    }

    public static Dictionary<string, object?>? Match(object? pat, object? inp, HashSet<string> lits)
    {
        if (pat is Nil) return inp is Nil ? [] : null;
        if (pat is Sym s)
        {
            if (s.Name == "_") return [];
            if (lits.Contains(s.Name))
                return inp is Sym si && s.Name == si.Name ? [] : null;
            return new Dictionary<string, object?> { { s.Name, inp } };
        }
        if (pat is not Cell pc) return Equals(pat, inp) ? [] : null;

        var pCdr = pc.Cdr;
        if (pCdr is Cell ellCell && ellCell.Car == Sym.ELLIPSIS)
        {
            var prefix = pc.Car;
            var restPat = ellCell.Cdr;
            var evars = GetPatternVars(prefix);

            var groups = new List<Dictionary<string, object?>>();
            while (inp is Cell inpCell)
            {
                var b = Match(prefix, inpCell.Car, lits);
                if (b is null) break;
                if (restPat is not Nil && Match(restPat, inp, lits) is not null) break;
                groups.Add(b);
                inp = inpCell.Cdr;
            }
            if (restPat is not Nil)
            {
                var b = Match(restPat, inp, lits);
                if (b is null) return null;
                foreach (var v in evars)
                    b[v] = groups.Select(g => g.GetValueOrDefault(v, Const.NIL)).ToList().ToCell();
                return b;
            }
            if (inp is not Nil) return null;
            return evars.ToDictionary(v => v, v =>
                (object?)groups.Select(g => g.GetValueOrDefault(v, Const.NIL)).ToList().ToCell());
        }

        if (inp is not Cell inpCell2) return null;
        var b1 = Match(pc.Car, inpCell2.Car, lits);
        if (b1 is null) return null;
        var b2 = Match(pc.Cdr, inpCell2.Cdr, lits);
        if (b2 is null) return null;
        foreach (var (k, v) in b2) b1[k] = v;
        return b1;
    }
}
