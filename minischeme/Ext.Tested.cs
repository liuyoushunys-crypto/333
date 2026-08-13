using System.Collections;
using System.Text;
using System.Runtime.CompilerServices;
using Miniscm.Types;
using Miniscm.Eval;

namespace Miniscm.Primitives;

public sealed class SchemeText { public SchemeString Value { get; } public SchemeText(object? value) => Value = new SchemeString(value is SchemeString s ? s.ToString() : value?.ToString() ?? ""); }
public sealed class SchemeFlexVector { public List<object?> Items { get; } public SchemeFlexVector(int n, object? fill) => Items = Enumerable.Repeat(fill, n).ToList(); }
public sealed class SchemeIntegerSet { public HashSet<long> Items { get; } = []; }
public sealed class SchemeEnumSet { public HashSet<object?> Items { get; } = []; }
public sealed class SchemeIdeque { public List<object?> Items { get; } = []; }
public sealed class SchemeEphemeron { public object? Key { get; } public object? Value { get; set; } public SchemeEphemeron(object? key, object? value) { Key = key; Value = value; } }
public sealed class SchemeDomain { public long Low { get; } public long High { get; } public SchemeDomain(long low, long high) { Low = low; High = high; } }
public sealed class SchemeColor { public double R, G, B, A; public SchemeColor(double r, double g, double b, double a = 1) { R = r; G = g; B = b; A = a; } }
public sealed class SchemeOption { public object? Names, Default, Handler; public SchemeOption(object? n, object? d, object? h) { Names = n; Default = d; Handler = h; } }
public sealed class SchemeArray2D { public int Rows, Columns; public object?[] Data; public SchemeArray2D(int r, int c, object? fill) { Rows = r; Columns = c; Data = Enumerable.Repeat(fill, r * c).ToArray(); } }

public static partial class PrimitiveRegistry
{
    private static object? TestList(object? value) => value is Cell ? value.Cells().ToCell() : value;
    private static object? TestCall(object? p, params object?[] args) => App(p, args);
    private static bool TestTrue(object? x) => Truthy(x);

    private static object? Everywhere(object? proc, object? x)
    {
        if (x is Cell c) return new Cell(Everywhere(proc, c.Car), Everywhere(proc, c.Cdr));
        if (x is SchemeVector sv)
        {
            var nv = new SchemeVector(sv.Data.Count);
            for (int i = 0; i < sv.Data.Count; i++) nv[i] = Everywhere(proc, sv.Data[i]);
            return nv;
        }
        return App(proc, x);
    }


    private static string Base32(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder(); int buffer = 0, bits = 0;
        foreach (var b in data) { buffer = (buffer << 8) | b; bits += 8; while (bits >= 5) { bits -= 5; sb.Append(alphabet[(buffer >> bits) & 31]); } }
        if (bits > 0) sb.Append(alphabet[(buffer << (5 - bits)) & 31]);
        while (sb.Length % 8 != 0) sb.Append('=');
        return sb.ToString();
    }

    private static object? PCsvRead(object?[] a)
    {
        var port = a[0]; var text = port is ITuple t && t.Length > 2 && t[2] is StringPort sp ? sp.Data : "";
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split(',').Select(x => (object?)new SchemeString(x)).ToCell()).ToCell();
    }

    private static object? PGroupBy(object?[] a)
    {
        var yes = new List<object?>(); var no = new List<object?>();
        foreach (var x in a[1].Cells()) (Truthy(App(a[0], x)) ? yes : no).Add(x);
        return new Cell(yes.ToCell(), new Cell(no.ToCell(), Const.NIL));
    }
}
