using System.Numerics;
using System.Text;
using System.Runtime.CompilerServices;

namespace Miniscm.Types;

public static class Printer
{
    public static string Format(object? x)
    {
        if (x is Nil) return "()";
        if (x is Sym s) return s.Name;
        if (x is Cell c) return c.ToString();
        if (x is string str) return $"\"{str.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        if (x is int i) return i.ToString();
        if (x is long l) return l.ToString();
        if (x is BigInteger bi) return bi.ToString();
        if (x is SchemeFraction f) return f.ToString();
        if (x is double d)
        {
            if (double.IsPositiveInfinity(d)) return "+inf.0";
            if (double.IsNegativeInfinity(d)) return "-inf.0";
            if (double.IsNaN(d)) return "+nan.0";
            var ds = d.ToString("G");
            if (!ds.Contains('.') && !ds.Contains('E') && !ds.Contains('e')) ds += ".0";
            return ds;
        }
        if (x is decimal dec) return dec.ToString();
        if (x is bool b) return b ? "#t" : "#f";
        if (x is Void) return "#<void>";
        if (x is Eof) return "#<eof>";
        if (x is SchemeString ss) return $"\"{ss.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        if (x is SchemeChar sc) return sc.ToString();
        if (x is SchemeVector sv) return sv.ToString();
        if (x is SchemeBytevector sb) return sb.ToString();
        if (x is SyntaxObject so) return $"#<syntax {Format(so.Expr)}>";
        if (x is ErrorObject eo) return Format(eo.Message);
        if (x is LambdaProc lp) return $"#<procedure{(lp.Name is not null ? " " + lp.Name : "")}>";
        if (x is Delegate) return "#<procedure>";
        if (x is Promise) return "#<promise>";
        if (x is ValueTuple<string, object?> vt) return $"#<{vt.Item1}>";
        if (x is ITuple it3 && it3.Length >= 2 && it3[0] is string s0 && s0 == "port" && it3[1] is string dir)
            return $"#<{dir}>";
        if (x is System.Numerics.Complex cx)
        {
            if (cx.Imaginary == 0) return Format(cx.Real);
            var r = cx.Real == 0 ? "" : Format(cx.Real);
            var sign = cx.Imaginary >= 0 ? "+" : "-";
            var imPart = cx.Imaginary == 1 ? "" : cx.Imaginary == -1 ? "" : Format(Math.Abs(cx.Imaginary));
            return $"{r}{sign}{imPart}i";
        }
        return x?.ToString() ?? "#<null>";
    }

    public static string ToDisplayString(object? x)
    {
        if (x is string s) return s;
        if (x is SchemeString ss) return ss.ToString();
        return Format(x);
    }
}
