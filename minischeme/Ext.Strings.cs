using System.Numerics;
using System.Text;
using Miniscm.Types;
using Miniscm.Eval;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
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

    private static object? StringTakeRight(object?[] args)
    {
        var s = Str(args[0]);
        int n = NumericHelper.ToInt(args[1]);
        return n == 0 ? new SchemeString("") : new SchemeString(s[^Math.Min(n, s.Length)..]);
    }

    private static object? StringDropRight(object?[] args)
    {
        var s = Str(args[0]);
        int n = NumericHelper.ToInt(args[1]);
        return n == 0 ? new SchemeString(s) : new SchemeString(s[..^Math.Min(n, s.Length)]);
    }

    private static object? StringReplace(object?[] args)
    {
        var s = Str(args[0]);
        var rep = Str(args[1]);
        int start = NumericHelper.ToInt(args[2]);
        int end = NumericHelper.ToInt(args[3]);
        return new SchemeString(s[..start] + rep + s[end..]);
    }

    private static object? StringForEach(object?[] args)
    {
        var fn = args[0];
        var s = Str(args[1]);
        foreach (var rune in s.EnumerateRunes()) App(fn, new SchemeChar(rune.Value));
        return Const.VOID;
    }

    private static object? StringForEachIndex(object?[] args)
    {
        var fn = args[0];
        var s = Str(args[1]);
        for (int i = 0; i < s.Length; i++) App(fn, (long)i);
        return Const.VOID;
    }

    private static object? StringTabulate(object?[] args)
    {
        int n = NumericHelper.ToInt(args[0]);
        var fn = args[1];
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++) sb.Append(char.ConvertFromUtf32(AsChar(App(fn, (long)i))));
        return new SchemeString(sb.ToString());
    }

    private static string Str(object? x) => x is SchemeString ss ? ss.ToString() : ToStr(x);

    private static object? StrPad(object?[] args, bool right)
    {
        var s = Str(args[0]);
        int n = NumericHelper.ToInt(args[1]);
        var ch = args.Length > 2 ? CharStr(args[2]) : " ";
        if (s.Length >= n) return new SchemeString(s[..n]);
        var pad = new string(ch[0], n - s.Length);
        return right ? new SchemeString(s + pad) : new SchemeString(pad + s);
    }

    private static string CharStr(object? c) => c is SchemeChar sc ? char.ConvertFromUtf32(sc.Codepoint) : Str(c);

    private static object? StrSplit(object?[] args)
    {
        var s = Str(args[0]);
        string[] parts;
        if (args.Length < 2 || args[1] is null)
            parts = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        else if (args[1] is SchemeChar sc)
            parts = s.Split(char.ConvertFromUtf32(sc.Codepoint), StringSplitOptions.RemoveEmptyEntries);
        else
            parts = s.Split(new[] { Str(args[1]) }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Select(p => (object?)new SchemeString(p)).ToCell();
    }

    private static object? StrJoin(object?[] args)
    {
        var parts = args[0].Cells().Select(x => Str(x)).ToList();
        var delim = args.Length > 1 ? Str(args[1]) : " ";
        return new SchemeString(string.Join(delim, parts));
    }

    private static object? StrContains(object? s, object? needle)
    {
        var str = Str(s);
        var sub = Str(needle);
        var strRunes = str.EnumerateRunes().Select(r => r.Value).ToList();
        var subRunes = sub.EnumerateRunes().Select(r => r.Value).ToList();
        for (int i = 0; i + subRunes.Count <= strRunes.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < subRunes.Count; j++)
                if (strRunes[i + j] != subRunes[j]) { match = false; break; }
            if (match) return (long)i;
        }
        return Const.FALSE;
    }

    private static object? PrefixLen(object?[] args, bool ci)
    {
        var s1 = Str(args[0]);
        var s2 = Str(args[1]);
        int n = 0;
        int max = Math.Min(s1.Length, s2.Length);
        for (int i = 0; i < max; i++)
        {
            bool eq = ci
                ? char.ToLowerInvariant(s1[i]) == char.ToLowerInvariant(s2[i])
                : s1[i] == s2[i];
            if (!eq) break;
            n++;
        }
        return (long)n;
    }

    private static object? SuffixLen(object?[] args, bool ci)
    {
        var s1 = Str(args[0]);
        var s2 = Str(args[1]);
        int n = 0;
        int max = Math.Min(s1.Length, s2.Length);
        for (int i = 0; i < max; i++)
        {
            bool eq = ci
                ? char.ToLowerInvariant(s1[s1.Length - 1 - i]) == char.ToLowerInvariant(s2[s2.Length - 1 - i])
                : s1[s1.Length - 1 - i] == s2[s2.Length - 1 - i];
            if (!eq) break;
            n++;
        }
        return (long)n;
    }

    private static object? StrCount(object?[] args)
    {
        var s = Str(args[0]);
        int n = 0;
        if (args[1] is not (string or SchemeString or SchemeChar))
        {
            foreach (var rune in s.EnumerateRunes())
                if (ReferenceEquals(App(args[1], new SchemeChar(rune.Value)), Const.TRUE)) n++;
        }
        else
        {
            var needle = args[1] is SchemeChar scc ? char.ConvertFromUtf32(scc.Codepoint) : Str(args[1]);
            int i = 0;
            while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        }
        return (long)n;
    }

    private static object? StrMap(object?[] args)
    {
        var fn = args[0];
        var s = Str(args[1]);
        var sb = new StringBuilder();
        foreach (var rune in s.EnumerateRunes())
            sb.Append(char.ConvertFromUtf32(AsChar(App(fn, new SchemeChar(rune.Value)))));
        return new SchemeString(sb.ToString());
    }

    private static object? StrFold(object?[] args, bool right)
    {
        var fn = args[0];
        object? acc = args[1];
        var s = Str(args[2]);
        var runes = s.EnumerateRunes().ToList();
        if (right)
        {
            for (int i = runes.Count - 1; i >= 0; i--) acc = App(fn, new SchemeChar(runes[i].Value), acc);
        }
        else
        {
            foreach (var rune in runes) acc = App(fn, new SchemeChar(rune.Value), acc);
        }
        return acc;
    }

    private static object? StrIndex(object? s, object? pred, bool right, bool skip)
    {
        var str = Str(s);
        var runes = str.EnumerateRunes().ToList();
        if (right)
        {
            for (int i = runes.Count - 1; i >= 0; i--)
            {
                var r = App(pred, new SchemeChar(runes[i].Value));
                bool hit = skip ? !ReferenceEquals(r, Const.TRUE) : ReferenceEquals(r, Const.TRUE);
                if (hit) return (long)i;
            }
        }
        else
        {
            for (int i = 0; i < runes.Count; i++)
            {
                var r = App(pred, new SchemeChar(runes[i].Value));
                bool hit = skip ? !ReferenceEquals(r, Const.TRUE) : ReferenceEquals(r, Const.TRUE);
                if (hit) return (long)i;
            }
        }
        return right && !skip ? Const.FALSE : (skip ? (object?)(long)runes.Count : Const.FALSE);
    }

    private static object? StrAnyEvery(object?[] args, bool every)
    {
        var pred = args[0];
        var s = Str(args[1]);
        object? last = Const.TRUE;
        foreach (var rune in s.EnumerateRunes())
        {
            var r = App(pred, new SchemeChar(rune.Value));
            if (every)
            {
                if (ReferenceEquals(r, Const.FALSE)) return Const.FALSE;
                last = r;
            }
            else
            {
                if (!ReferenceEquals(r, Const.FALSE)) return r;
            }
        }
        return every ? last : Const.FALSE;
    }

    private static object? StrCopyBang(object?[] args)
    {
        var target = args[0] as SchemeString;
        int tstart = NumericHelper.ToInt(args[1]);
        var src = Str(args[2]);
        int sstart = args.Length > 3 ? NumericHelper.ToInt(args[3]) : 0;
        int send = args.Length > 4 ? NumericHelper.ToInt(args[4]) : src.Length;
        if (target is not null)
        {
            for (int i = sstart; i < send; i++)
            {
                int idx = tstart + i - sstart;
                if (idx < target.Length) target[idx] = char.ConvertToUtf32(src, i);
            }
        }
        return Const.VOID;
    }

    private static object? StrFilter(object?[] args, bool keep)
    {
        var pred = args[0];
        var s = Str(args[1]);
        var sb = new StringBuilder();
        foreach (var rune in s.EnumerateRunes())
        {
            bool hit = ReferenceEquals(App(pred, new SchemeChar(rune.Value)), Const.TRUE);
            if (hit == keep) sb.Append(char.ConvertFromUtf32(rune.Value));
        }
        return new SchemeString(sb.ToString());
    }

    private static string RevStr(string s)
    {
        var chars = s.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    private static string TitleCase(string s)
    {
        var sb = new StringBuilder(s.ToLowerInvariant());
        bool cap = true;
        for (int i = 0; i < sb.Length; i++)
        {
            if (char.IsWhiteSpace(sb[i])) cap = true;
            else if (cap) { sb[i] = char.ToUpperInvariant(sb[i]); cap = false; }
        }
        return sb.ToString();
    }

    private static object? Tokenize(object?[] args)
    {
        var s = Str(args[0]);
        var tokens = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Select(t => (object?)new SchemeString(t)).ToCell();
    }

    private static object? StrUnfold(object?[] args)
    {
        var pred = args[0];
        var gen = args[1];
        var step = args.Length > 2 ? args[2] : null;
        var seed = args.Length > 3 ? args[3] : Const.FALSE;
        var sb = new StringBuilder();
        var s = seed;
        while (true)
        {
            if (ReferenceEquals(App(pred, s), Const.TRUE)) break;
            var ch = App(gen, s);
            sb.Append(char.ConvertFromUtf32(AsChar(ch)));
            if (step is not null) s = App(step, s);
            else
            {
                if (s is Cell c) s = c.Cdr;
                else break;
            }
        }
        return new SchemeString(sb.ToString());
    }

    private static object? StrToVector(object? s)
    {
        var data = new List<object?>();
        foreach (var rune in Str(s).EnumerateRunes()) data.Add(new SchemeChar(rune.Value));
        return new SchemeVector(data);
    }

    private static object? VectorToStr(object? v)
    {
        var sb = new StringBuilder();
        if (v is SchemeVector sv)
        {
            foreach (var x in sv.Data)
            {
                if (x is SchemeChar sc) sb.Append(char.ConvertFromUtf32(sc.Codepoint));
                else sb.Append(ToStr(x));
            }
        }
        return new SchemeString(sb.ToString());
    }
}
