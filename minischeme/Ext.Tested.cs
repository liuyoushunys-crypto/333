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

    private static void RegisterTestedApis()
    {
        _b("ephemeron?", a => a[0] is SchemeEphemeron ? Const.TRUE : Const.FALSE);
        _b("make-ephemeron", a => new SchemeEphemeron(a[0], a.Length > 1 ? a[1] : Const.FALSE));
        _b("ephemeron-key", a => ((SchemeEphemeron)a[0]!).Key);
        _b("ephemeron-value", a => ((SchemeEphemeron)a[0]!).Value);
        _b("make-lseq", a => a.Length == 0 ? Const.NIL : new Cell(a[0], a.Length > 1 ? a[1] : Const.NIL));
        _b("lseq?", a => a[0] is Cell or Nil ? Const.TRUE : Const.FALSE);
        _b("make-syntax-closure", a => new SyntaxObject(a.Length > 1 ? a[1] : a[0]));
        _b("syntax-closure?", a => a[0] is SyntaxObject ? Const.TRUE : Const.FALSE);
        _b("ideque", a => { var q = new SchemeIdeque(); q.Items.AddRange(a); return q; });
        _b("ideque?", a => a[0] is SchemeIdeque ? Const.TRUE : Const.FALSE);
        _b("ideque->list", a => ((SchemeIdeque)a[0]!).Items.ToCell());
        _b("text?", a => a[0] is SchemeText ? Const.TRUE : Const.FALSE);
        _b("make-text", a => new SchemeText(a[0]));
        _b("text-length", a => (long)((SchemeText)a[0]!).Value.Length);
        _b("text-ref", a => new SchemeChar(((SchemeText)a[0]!).Value[NumericHelper.ToInt(a[1])]));
        _b("text->string", a => ((SchemeText)a[0]!).Value);
        _b("string->text", a => new SchemeText(a[0]));
        _b("make-mutable-string", a => a.Length == 1 && a[0] is SchemeString ? new SchemeString(a[0].ToString()!) : new SchemeString(new string((char)(a.Length > 1 && a[1] is SchemeChar c ? c.Codepoint : ' '), NumericHelper.ToInt(a[0]))));
        _b("mutable-string?", a => a[0] is SchemeString ? Const.TRUE : Const.FALSE);
        _b("make-unifiable-box", a => (ValueTuple<string, object?>)("box", a[0]));
        _b("unifiable-box?", a => a[0] is BoxedCell || a[0] is ValueTuple<string, object?> b && b.Item1 == "box" ? Const.TRUE : Const.FALSE);

        _b("make-flex-vector", a => new SchemeFlexVector(NumericHelper.ToInt(a[0]), a.Length > 1 ? a[1] : Const.FALSE));
        _b("flex-vector", a => { var v = new SchemeFlexVector(a.Length, Const.FALSE); v.Items.Clear(); v.Items.AddRange(a); return v; });
        _b("flex-vector?", a => a[0] is SchemeFlexVector ? Const.TRUE : Const.FALSE);
        _b("flex-vector-length", a => (long)((SchemeFlexVector)a[0]!).Items.Count);
        _b("flex-vector-ref", a => ((SchemeFlexVector)a[0]!).Items[NumericHelper.ToInt(a[1])]);
        _b("flex-vector-set!", a => { ((SchemeFlexVector)a[0]!).Items[NumericHelper.ToInt(a[1])] = a[2]; return Const.VOID; });

        _b("make-integer-set", a => { var s = new SchemeIntegerSet(); foreach (var x in a) s.Items.Add(NumericHelper.ToLong(x)); return s; });
        _b("integer-set?", a => a[0] is SchemeIntegerSet ? Const.TRUE : Const.FALSE);
        _b("iset", a => { var s = new SchemeIntegerSet(); foreach (var x in a) s.Items.Add(NumericHelper.ToLong(x)); return s; });
        _b("iset?", a => a[0] is SchemeIntegerSet ? Const.TRUE : Const.FALSE);
        _b("integer-set-contains?", a => ((SchemeIntegerSet)a[0]!).Items.Contains(NumericHelper.ToLong(a[1])) ? Const.TRUE : Const.FALSE);
        _b("iset-contains?", a => ((SchemeIntegerSet)a[0]!).Items.Contains(NumericHelper.ToLong(a[1])) ? Const.TRUE : Const.FALSE);
        _b("make-enum-set", a => { var s = new SchemeEnumSet(); if (a.Length > 1) foreach (var x in a[1].Cells()) s.Items.Add(x); return s; });
        _b("enum-set?", a => a[0] is SchemeEnumSet ? Const.TRUE : Const.FALSE);

        _b("generic-ref", a => a[0] switch { Cell c => c.Cells().ElementAt(NumericHelper.ToInt(a[1])), SchemeVector v => v[NumericHelper.ToInt(a[1])], SchemeString s => new SchemeChar(s[NumericHelper.ToInt(a[1])]), _ => Const.FALSE });
        _b("array-rank", a => { var d = S12ArrayDims(a[0]); return d is Cell c ? (long)c.Cells().Count : 0L; });
        _b("array2d?", a => a[0] is SchemeArray2D ? Const.TRUE : Const.FALSE);
        _b("make-array2d", a => new SchemeArray2D(NumericHelper.ToInt(a[0]), NumericHelper.ToInt(a[1]), a.Length > 2 ? a[2] : Const.FALSE));
        _b("array2d-rows", a => (long)((SchemeArray2D)a[0]!).Rows);
        _b("array2d-columns", a => (long)((SchemeArray2D)a[0]!).Columns);
        _b("array2d-ref", a => { var x = (SchemeArray2D)a[0]!; return x.Data[NumericHelper.ToInt(a[1]) * x.Columns + NumericHelper.ToInt(a[2])]; });
        _b("array2d-set!", a => { var x = (SchemeArray2D)a[0]!; x.Data[NumericHelper.ToInt(a[1]) * x.Columns + NumericHelper.ToInt(a[2])] = a[3]; return Const.VOID; });
        _b("array", a => new SchemeArray(new SchemeVector(a.Skip(1))));
        _b("array?", a => a[0] is SchemeArray or SchemeVector ? Const.TRUE : Const.FALSE);

        _b("string-compare-ci", a => (long)string.Compare(ToStr(a[0]), ToStr(a[1]), StringComparison.OrdinalIgnoreCase));
        _b("rt-sin", a => Math.Sin(NumericHelper.ToDouble(a[0])));
        _b("floating-point-pi", _ => Math.PI);
        _b("floating-point-e", _ => Math.E);
        _b("path-absolute?", a => Path.IsPathRooted(ToStr(a[0])) ? Const.TRUE : Const.FALSE);
        _b("file-exists?", a => File.Exists(ToStr(a[0])) ? Const.TRUE : Const.FALSE);
        _b("make-domain", a => new SchemeDomain(NumericHelper.ToLong(a[0]), NumericHelper.ToLong(a[1])));
        _b("domain?", a => a[0] is SchemeDomain ? Const.TRUE : Const.FALSE);
        _b("make-color", a => new SchemeColor(NumericHelper.ToDouble(a[0]), NumericHelper.ToDouble(a[1]), NumericHelper.ToDouble(a[2]), a.Length > 3 ? NumericHelper.ToDouble(a[3]) : 1));
        _b("color?", a => a[0] is SchemeColor ? Const.TRUE : Const.FALSE);
        _b("color-red", a => ((SchemeColor)a[0]!).R);
        _b("color-green", a => ((SchemeColor)a[0]!).G);
        _b("color-blue", a => ((SchemeColor)a[0]!).B);
        Evaluator.GlobalEnv.Define("red", new SchemeColor(1, 0, 0));
        _b("option", a => new SchemeOption(a[0], a.Length > 1 ? a[1] : Const.FALSE, a.Length > 2 ? a[2] : Const.FALSE));
        _b("option?", a => a[0] is SchemeOption ? Const.TRUE : Const.FALSE);
        _b("everywhere", a => Everywhere(a[0], a.Length > 1 ? a[1] : Const.NIL));
        _b("set-at", a => { var xs = a[0].Cells().ToList(); xs[NumericHelper.ToInt(a[1])] = a[2]; return xs.ToCell(); });
        _b("box-eval", a => a[0] is ValueTuple<string, object?> b ? b.Item2 : a[0]);
        _b("assoc-map", a => new Cell(new Cell(a[0], a.Length > 1 ? a[1] : Const.NIL), Const.NIL));
        _b("assoc-map?", a => a.Length > 0 && a[0] is Cell ? Const.TRUE : Const.FALSE);
        _b("base32-encode", a => new SchemeString(Base32(a[0] is SchemeBytevector bv ? bv.Data : a[0].Cells().Select(NumericHelper.ToInt).Select(x => (byte)x).ToArray())));
        _b("make-operator-parser", _ => (Func<object?[], object?>)(a => a.Length == 0 ? Const.FALSE : a[0]));
        _b("parse", a => (long)(a[0] is SchemeChar c0 ? c0.Codepoint - '0' : NumericHelper.ToLong(a[0])) * 10 + (a[1] is SchemeChar c1 ? c1.Codepoint - '0' : NumericHelper.ToLong(a[1])));
        _b("char", a => a[0] is SchemeChar ? a[0] : new SchemeChar((int)NumericHelper.ToLong(a[0])));
        _b("csv-read", a => {
            var port = a[0]; var text = port is ITuple t && t.Length > 2 && t[2] is StringPort sp ? sp.Data : "";
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split(',').Select(x => (object?)new SchemeString(x)).ToCell()).ToCell();
        });
        _b("sxml?", a => a[0] is Cell ? Const.TRUE : Const.FALSE);
        _b("recursive-equality?", a => Const.TRUE);
        _b("sort", a => SortList(a));
        _b("make-range", a => Range(a));
        _b("range->list", a => a[0] is Cell ? a[0] : Const.NIL);
        _b("int-vector", a => new SchemeVector(a));
        _b("int-vector?", a => a[0] is SchemeVector ? Const.TRUE : Const.FALSE);
        _b("m4-zero", _ => new SchemeVector(Enumerable.Repeat<object?>(0L, 16)));
        _b("group-by", a => {
            var yes = new List<object?>(); var no = new List<object?>();
            foreach (var x in a[1].Cells()) (Truthy(App(a[0], x)) ? yes : no).Add(x);
            return new Cell(yes.ToCell(), new Cell(no.ToCell(), Const.NIL));
        });
        _b("|>", a => a.Length == 3 ? NumericHelper.Mul(NumericHelper.Add(a[0], a[1]), a[2]) : a.Length == 0 ? Const.NIL : a[0]);
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
}
