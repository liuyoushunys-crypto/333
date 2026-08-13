using System.Numerics;
using Miniscm.Types;
using Miniscm.Eval;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    private static bool[] CharsetData(object? value)
    {
        if (value is bool[] bits) return bits;
        if (value is SchemeVector vector)
            return vector.Data.Select(x => x switch
            {
                bool b => b,
                Sym s => !ReferenceEquals(s, Const.FALSE),
                _ => x is not Nil
            }).ToArray();
        throw new ArgumentException("not a character set");
    }

    private static bool IsControlChar(int cp) => cp < 32 || cp == 127;

    private static object? CharName(object? c)
    {
        if (c is SchemeString or string)
        {
            var name = Str(c);
            int cp = name switch
            {
                "space" => ' ',
                "newline" => '\n',
                "tab" => '\t',
                "return" => '\r',
                "null" => '\0',
                "alarm" => '\a',
                "backspace" => '\b',
                "escape" => 0x1b,
                "delete" => 0x7f,
                _ => name.Length == 1 ? name[0] : -1
            };
            return cp >= 0 ? new Cell(Sym.Intern("char"), new Cell(new SchemeChar(cp), Const.NIL)) : Const.FALSE;
        }
        int cc = AsChar(c);
        string nm = cc switch
        {
            ' ' => "space",
            '\n' => "newline",
            '\t' => "tab",
            '\r' => "return",
            '\0' => "null",
            '\a' => "alarm",
            '\b' => "backspace",
            0x1b => "escape",
            0x7f => "delete",
            _ => ((char)cc).ToString()
        };
        return new SchemeString(nm);
    }

    private static object? MakeCharSet(object?[] args)
    {
        var cs = new bool[256];
        foreach (var c in args)
        {
            int cp = AsChar(c);
            if (cp < 256) cs[cp] = true;
        }
        return cs;
    }

    private static object? MakeCharSet(string s)
    {
        var cs = new bool[256];
        foreach (var rune in s.EnumerateRunes())
            if (rune.Value < 256) cs[rune.Value] = true;
        return cs;
    }

    private static bool CharSetContains(object? cs, object? c)
    {
        int i = AsChar(c);
        return i < 256 && CharsetData(cs)[i];
    }

    private static object? CharSetToList(object? cs)
    {
        var data = CharsetData(cs);
        var res = new List<object?>();
        for (int i = 0; i < 256; i++) if (data[i]) res.Add(new SchemeChar(i));
        return res.ToCell();
    }

    private static object? CharSetToString(object? cs)
    {
        var data = CharsetData(cs);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 256; i++) if (data[i]) sb.Append(char.ConvertFromUtf32(i));
        return new SchemeString(sb.ToString());
    }

    private static object? CharSetBinOp(object?[] args, bool union)
    {
        if (args.Length == 0) return new bool[256];
        var result = (bool[])CharsetData(args[0]).Clone();
        for (int k = 1; k < args.Length; k++)
        {
            var other = CharsetData(args[k]);
            for (int i = 0; i < 256; i++)
                result[i] = union ? (result[i] || other[i]) : (result[i] && other[i]);
        }
        return result;
    }

    private static object? CharSetDiff(object?[] args)
    {
        var result = (bool[])CharsetData(args[0]).Clone();
        for (int k = 1; k < args.Length; k++)
        {
            var other = CharsetData(args[k]);
            for (int i = 0; i < 256; i++) if (other[i]) result[i] = false;
        }
        return result;
    }

    private static object? CharSetXor(object?[] args)
    {
        var result = new bool[256];
        foreach (var csArg in args)
        {
            var cs = CharsetData(csArg);
            for (int i = 0; i < 256; i++) if (cs[i]) result[i] = !result[i];
        }
        return result;
    }

    private static object? CharSetComplement(object? cs)
    {
            var data = CharsetData(cs);
        var result = new bool[256];
        for (int i = 0; i < 256; i++) result[i] = !data[i];
        return result;
    }

    private static object? CharSetAdjoin(object?[] args, bool add)
    {
        var result = (bool[])CharsetData(args[0]).Clone();
        for (int k = 1; k < args.Length; k++)
        {
            int cp = AsChar(args[k]);
            if (cp < 256) result[cp] = add;
        }
        return result;
    }

    private static object? CharSetAny(object? pred, object? cs)
    {
        var data = CharsetData(cs);
        for (int i = 0; i < 256; i++)
            if (data[i] && !ReferenceEquals(App(pred, new SchemeChar(i)), Const.FALSE)) return new SchemeChar(i);
        return Const.FALSE;
    }

    private static object? CharSetEvery(object? pred, object? cs)
    {
        var data = CharsetData(cs);
        for (int i = 0; i < 256; i++)
            if (data[i] && !ReferenceEquals(App(pred, new SchemeChar(i)), Const.TRUE)) return Const.FALSE;
        return Const.TRUE;
    }

    private static object? CharSetFilter(object?[] args)
    {
        var pred = args[0];
        var basis = args.Length > 2 ? CharsetData(args[2]) : CharsetData(args[1]);
        var result = new bool[256];
        for (int i = 0; i < 256; i++)
            if (basis[i] && ReferenceEquals(App(pred, new SchemeChar(i)), Const.TRUE)) result[i] = true;
        return result;
    }

    private static object? CharSetFold(object? kons, object? knil, object? cs)
    {
        var data = CharsetData(cs);
        object? acc = knil;
        for (int i = 0; i < 256; i++)
            if (data[i]) acc = App(kons, acc, new SchemeChar(i));
        return acc;
    }

    private static object? CharSetForEach(object? proc, object? cs)
    {
        var data = CharsetData(cs);
        for (int i = 0; i < 256; i++)
            if (data[i]) App(proc, new SchemeChar(i));
        return Const.VOID;
    }

    private static object? CharSetMap(object? proc, object? cs)
    {
        var data = CharsetData(cs);
        var result = new bool[256];
        for (int i = 0; i < 256; i++)
        {
            if (data[i])
            {
                var r = App(proc, new SchemeChar(i));
                int cp;
                if (r is SchemeChar rc) cp = rc.Codepoint;
                else if (r is string str && str.Length > 0) cp = str[0];
                else throw new SchemeException("char-set-map: proc must return a char");
                if (cp < 256) result[cp] = true;
            }
        }
        return result;
    }

    private static object? CharSetHash(object?[] args)
    {
        var cs = CharsetData(args[0]);
        long bound = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 65536;
        long h = 0;
        for (int i = 0; i < 256; i++)
            if (cs[i]) h = (h * 41 + i) % bound;
        return h;
    }

    private static object? CharSetEqual(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        var first = CharsetData(args[0]);
        for (int k = 1; k < args.Length; k++)
        {
            var other = CharsetData(args[k]);
            for (int i = 0; i < 256; i++)
                if (first[i] != other[i]) return Const.FALSE;
        }
        return Const.TRUE;
    }
}
