using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Miniscm.Primitives;
using Miniscm.Types;

namespace Miniscm.Base;

abstract record AstNode;

sealed record LiteralAst(object? Val) : AstNode;
sealed record VarAst(string Name) : AstNode;
sealed record IfAst(AstNode Test, AstNode Then, AstNode Else) : AstNode;
sealed record DefineAst(string Name, AstNode Val) : AstNode;
sealed record SetBangAst(string Name, AstNode Val) : AstNode;
sealed record LambdaAst(List<string> Params, List<AstNode> Body, bool IsSimple, object? RawBody = null) : AstNode;
sealed record BeginAst(List<AstNode> Exprs) : AstNode;
sealed record AppAst(AstNode Proc, List<AstNode> Args) : AstNode;


public sealed class Nil : IEquatable<Nil>
{
    public static readonly Nil Instance = new();
    private Nil() { }
    public bool Equals(Nil? other) => other is not null;
    public override bool Equals(object? obj) => obj is Nil;
    public override int GetHashCode() => 0;
    public override string ToString() => "()";
}

public sealed class Void : IEquatable<Void>
{
    public static readonly Void Instance = new();
    private Void() { }
    public bool Equals(Void? other) => other is not null;
    public override bool Equals(object? obj) => obj is Void;
    public override int GetHashCode() => 1;
    public override string ToString() => "#<void>";
}

public sealed class Eof : IEquatable<Eof>
{
    public static readonly Eof Instance = new();
    private Eof() { }
    public bool Equals(Eof? other) => other is not null;
    public override bool Equals(object? obj) => obj is Eof;
    public override int GetHashCode() => 2;
    public override string ToString() => "#<eof>";
}

public static class Const
{
    public static readonly Nil NIL = Nil.Instance;
    public static readonly Void VOID = Void.Instance;
    public static readonly Eof EOF = Eof.Instance;
    public static readonly Sym TRUE = Sym.Intern("#t");
    public static readonly Sym FALSE = Sym.Intern("#f");
}

public class Env
{
    public Dictionary<string, object?> Data { get; }
    public Env? Parent { get; }

    public Env(Env? parent = null) : this(parent, 0) { }

    public Env(Env? parent, int capacity)
    {
        Parent = parent;
        Data = capacity > 0 ? new Dictionary<string, object?>(capacity) : [];
    }

    public object? Lookup(string name)
    {
        if (Data.TryGetValue(name, out var val))
            return val is BoxedCell bc ? bc.Value : val;
        if (Parent is not null) return Parent.Lookup(name);
        throw new NameError($"unbound: {name}");
    }

    public object? Lookup(Sym sym) => Lookup(sym.Name);

    public object? LookupSilent(string name, object? sentinel = null)
    {
        if (Data.TryGetValue(name, out var val))
            return val is BoxedCell bc ? bc.Value : val;
        if (Parent is not null) return Parent.LookupSilent(name, sentinel);
        return sentinel;
    }

    public void Define(string name, object? v) => Data[name] = v;
    public void Define(Sym sym, object? v) => Data[sym.Name] = v;

    public static Env MakeChild(Env parent) => new(parent);

    public Env MakeChildEnv() => new(this);

    public object? SetVal(string name, object? v)
    {
        var e = this;
        while (e is not null)
        {
            if (e.Data.ContainsKey(name)) { e.Data[name] = v; return Const.VOID; }
            e = e.Parent;
        }
        Data[name] = v;
        return Const.VOID;
    }

    public object? SetVal(Sym sym, object? v) => SetVal(sym.Name, v);
}

public class NameError : Exception
{
    public NameError(string msg) : base(msg) { }
}

public class SchemeException : Exception
{
    public object? Val { get; }
    public SchemeException(object? val) : base(val?.ToString()) => Val = val;
}


public static partial class Tokenizer
{
    [GeneratedRegex(@"
        \s*
        ( ;[^\n]*                                
        | \#\|[\s\S]*?\|\#                       
        | \#;                                     
        | """"""[\s\S]*?""""""                     
        | '''[\s\S]*?'''                           
        | ""(?:[^""\\]|\\.)*""                    
        | \#\\(?:[a-zA-Z]+|[\uD800-\uDBFF][\uDC00-\uDFFF]|.)                     
        | \#u8\(
        | \#\(
        | \#\{[^}]*\}                              
        | [\(\)]                                  
        | \#'|\#\`|\#,@|\#,|\'|`|,@|,             
        | \.\.\.                                  
        | \#t|\#f                                 
        | [-+]?(?:0x[0-9a-fA-F]+|0o[0-7]+|0b[01]+
                |[0-9]+/[0-9]+                   
                |[0-9]+(?:\.[0-9]*)?(?:[eE][-+]?[0-9]+)?
                |\.[0-9]+(?:[eE][-+]?[0-9]+)?
                )(?:i|[-+]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)i)?
                (?![a-zA-Z0-9!$%&*+\-./:<=>?@^~_])
        | \.                                      
        | [^\s\(\)""',;`#]+                       
        )
    ", RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled)]
    private static partial Regex TokenRegex();

    public static List<string> Tokenize(string s)
    {
        var res = new List<string>();
        foreach (Match m in TokenRegex().Matches(s))
        {
            var g = m.Groups[1].Value;
            if (g.Length > 0 && g[0] != ';')
                res.Add(g);
        }
        return res;
    }

    public static List<(string text, int pos)> TokenizeWithPos(string s)
    {
        var res = new List<(string, int)>();
        foreach (Match m in TokenRegex().Matches(s))
        {
            var g = m.Groups[1].Value;
            if (g.Length > 0 && g[0] != ';')
                res.Add((g, m.Index));
        }
        return res;
    }
}

public sealed class Sym : IEquatable<Sym>
{
    private static readonly Dictionary<string, Sym> _intern = [];

    public string Name { get; }

    private Sym(string s) => Name = s;

    public static Sym Intern(string s)
    {
        if (_intern.TryGetValue(s, out var existing)) return existing;
        var obj = new Sym(s);
        _intern[s] = obj;
        return obj;
    }

    public bool Equals(Sym? other) => other is not null && Name == other.Name;
    public override bool Equals(object? obj) => obj is Sym s && Name == s.Name;
    public override int GetHashCode() => Name.GetHashCode();
    public override string ToString() => Name;
    public static implicit operator string(Sym s) => s.Name;

    public static readonly Sym QUOTE = Intern("quote");
    public static readonly Sym QQ = Intern("quasiquote");
    public static readonly Sym UNQUOTE = Intern("unquote");
    public static readonly Sym UNSPLICE = Intern("unquote-splicing");
    public static readonly Sym SYNTAX = Intern("syntax");
    public static readonly Sym QS = Intern("quasisyntax");
    public static readonly Sym USYNTAX = Intern("unsyntax");
    public static readonly Sym USPLICES = Intern("unsyntax-splicing");
    public static readonly Sym ELLIPSIS = Intern("...");
    public static readonly Sym LAMBDA = Intern("lambda");
    public static readonly Sym DEFINE = Intern("define");
    public static readonly Sym SETBANG = Intern("set!");
    public static readonly Sym IF = Intern("if");
    public static readonly Sym BEGIN = Intern("begin");
    public static readonly Sym APPLY = Intern("apply");
    public static readonly Sym ARGS = Intern("args");
    public static readonly Sym SETF = Intern("set!-form");
    public static readonly Sym THE_ENVIRONMENT = Intern("the-environment");
    public static readonly Sym USCORE = Intern("_");
    public static readonly Sym VOID_SYM = Intern("if #f #f");
    public static readonly Sym LT = Intern("<>");
    public static readonly Sym LT3 = Intern("<...>");
    public static readonly Sym TRUE = Intern("#t");
    public static readonly Sym FALSE = Intern("#f");
}

public sealed class BoxedCell
{
    public object? Value { get; set; }
}

public sealed class SyntaxObject
{
    public object? Expr { get; }
    public SyntaxObject(object? expr) => Expr = expr;
    public override string ToString() => $"#<syntax {Printer.Format(Expr)}>";
}

public sealed class ErrorObject
{
    public object? Message { get; }
    public object? Irritants { get; }
    public ErrorObject(object? message, object? irritants) { Message = message; Irritants = irritants; }
    public override string ToString() => Printer.Format(Message);
}

public sealed class Promise
{
    public bool Forced { get; set; }
    public object? Val { get; set; }
    public Func<object?>? Thunk { get; }
    public Promise(Func<object?> thunk) => Thunk = thunk;
}public sealed class TailCall
{
    public object? Expr { get; }
    public Env Env { get; }
    public TailCall(object? expr, Env env) { Expr = expr; Env = env; }
}

public sealed class ContinuationEscape : Exception
{
    public object? Val { get; }
    public int Id { get; }
    public ContinuationEscape(object? val, int id) { Val = val; Id = id; }
}

public static class ContCounter
{
    public static int Value;
}

public class StringPort
{
    public string Data;
    public int Pos;
    public StringPort(string data) { Data = data; Pos = 0; }
    public void SetPos(int p) => Pos = p;
}

public sealed class BytePort : IDisposable
{
    public byte[] Data;
    public int Pos;
    public string? FilePath { get; }
    public BytePort(IEnumerable<byte> data, string? filePath = null) { Data = data.ToArray(); FilePath = filePath; Pos = 0; }
    public void Append(byte value)
    {
        Array.Resize(ref Data, Data.Length + 1);
        Data[^1] = value;
    }
    public void Dispose()
    {
        if (FilePath is not null) File.WriteAllBytes(FilePath, Data);
    }
}

public sealed class LambdaProc
{
    public List<string> Params { get; }
    public object? Body { get; }
    public Env ClosureEnv { get; }
    public bool IsSimple { get; }
    public string? Name { get; set; }
    public object? CompiledVersion { get; set; }

    public LambdaProc(List<string> @params, object? body, Env env, bool isSimple, string? name = null)
    {
        Params = @params;
        Body = body;
        ClosureEnv = env;
        IsSimple = isSimple;
        Name = name;
    }
}


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


public sealed class Cell : IEnumerable<object?>
{
    public object? Car { get; set; }
    public object? Cdr { get; set; }

    public Cell(object? car, object? cdr)
    {
        Car = car;
        Cdr = cdr;
    }

    public int Length
    {
        get
        {
            int n = 0;
            var cur = this;
            while (true)
            {
                n++;
                if (cur.Cdr is Cell next) cur = next;
                else break;
            }
            return n;
        }
    }

    public object? this[int index]
    {
        get
        {
            var cur = this;
            for (int i = 0; i < index; i++)
            {
                if (cur.Cdr is Cell next) cur = next;
                else throw new IndexOutOfRangeException();
            }
            return cur.Car;
        }
    }

    public IEnumerator<object?> GetEnumerator()
    {
        var cur = this;
        while (true)
        {
            yield return cur.Car;
            if (cur.Cdr is Cell next) cur = next;
            else yield break;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override int GetHashCode() => HashCode.Combine(Car, Cdr);
    public override bool Equals(object? obj) => obj is Cell c && Equals(Car, c.Car) && Equals(Cdr, c.Cdr);

    public override string ToString()
    {
        var sb = new StringBuilder("(");
        ToStringHelper(sb, new HashSet<Cell>());
        sb.Append(')');
        return sb.ToString();
    }

    private void ToStringHelper(StringBuilder sb, HashSet<Cell> seen)
    {
        if (seen.Contains(this)) { sb.Append("..."); return; }
        seen.Add(this);
        sb.Append(Printer.Format(Car));
        var cur = Cdr;
        while (cur is Cell cell)
        {
            if (seen.Contains(cell)) { sb.Append(" ..."); break; }
            seen.Add(cell);
            sb.Append(' ');
            sb.Append(Printer.Format(cell.Car));
            cur = cell.Cdr;
        }
        if (cur is not Nil) { sb.Append(" . "); sb.Append(Printer.Format(cur)); }
    }
}

public static class CellHelper
{
    public static Cell? AsCell(this object? v) => v as Cell;
    public static string AsString(this object? v) => v is Sym s ? s.Name : (v as string) ?? "";

    public static int CellLength(this object? v)
    {
        int n = 0;
        var cur = v;
        while (cur is Cell cell) { n++; cur = cell.Cdr; }
        return n;
    }

    public static List<object?> Cells(this object? v)
    {
        var res = new List<object?>();
        var cur = v;
        while (cur is Cell cell) { res.Add(cell.Car); cur = cell.Cdr; }
        return res;
    }

    public static object? ToCell(this IEnumerable<object?> items)
    {
        object? head = Const.NIL;
        Cell? tail = null;
        foreach (var x in items)
        {
            var n = new Cell(x, Const.NIL);
            if (tail is null) head = n;
            else tail.Cdr = n;
            tail = n;
        }
        return head;
    }
}


public sealed class SchemeString
{
    public List<int> Data { get; }

    public SchemeString(string s)
    {
        Data = [];
        foreach (var rune in s.EnumerateRunes())
            Data.Add(rune.Value);
    }

    public SchemeString(IEnumerable<int> codepoints) => Data = [.. codepoints];

    public int Length => Data.Count;
    public int this[int i] { get => Data[i]; set => Data[i] = value; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var cp in Data)
            sb.Append(char.ConvertFromUtf32(cp));
        return sb.ToString();
    }

    public override int GetHashCode() => ToString().GetHashCode();
    public override bool Equals(object? obj) => obj is SchemeString ss && ToString() == ss.ToString()
        || obj is string s && ToString() == s;
}

public sealed class SchemeChar : IEquatable<SchemeChar>
{
    public int Codepoint { get; }

    public SchemeChar(int codepoint)
    {
        if (!Rune.IsValid(codepoint))
            throw new Exception($"invalid codepoint: {codepoint}");
        Codepoint = codepoint;
    }

    public bool Equals(SchemeChar? other) => other is not null && Codepoint == other.Codepoint;
    public override bool Equals(object? obj) => obj is SchemeChar c && Codepoint == c.Codepoint;
    public override int GetHashCode() => Codepoint;

    public override string ToString()
    {
        var s = char.ConvertFromUtf32(Codepoint);
        if (s.Length == 1)
        {
            return s[0] switch
            {
                ' ' => "#\\space",
                '\n' => "#\\newline",
                '\t' => "#\\tab",
                '\r' => "#\\return",
                '\0' => "#\\nul",
                '\a' => "#\\alarm",
                '\b' => "#\\backspace",
                '\x1b' => "#\\escape",
                '\x7f' => "#\\delete",
                _ => "#\\" + s
            };
        }
        return "#\\" + s;
    }
}

public sealed class SchemeVector
{
    public List<object?> Data { get; }

    public SchemeVector() => Data = [];
    public SchemeVector(IEnumerable<object?> items) => Data = [.. items];
    public SchemeVector(int size) => Data = new List<object?>(new object?[size]);

    public int Length => Data.Count;
    public object? this[int i] { get => Data[i]; set => Data[i] = value; }

    public override string ToString()
    {
        var sb = new StringBuilder("#(");
        for (int i = 0; i < Data.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(Printer.Format(Data[i]));
        }
        sb.Append(')');
        return sb.ToString();
    }
}

public sealed class SchemeBytevector
{
    public byte[] Data { get; }

    public SchemeBytevector(byte[] data) => Data = data;
    public SchemeBytevector(IEnumerable<int> ints) => Data = [.. ints.Select(i => (byte)i)];
    public SchemeBytevector(string s) => Data = Encoding.UTF8.GetBytes(s);

    public int Length => Data.Length;
    public byte this[int i] { get => Data[i]; set => Data[i] = value; }

    public override bool Equals(object? obj) => obj is SchemeBytevector b && Data.AsSpan().SequenceEqual(b.Data);
    public override int GetHashCode() => HashCode.Combine(Data.Length, Data.Length == 0 ? 0 : Data[0]);

    public override string ToString() => "#u8(" + string.Join(",", Data) + ")";
}


public static class AtomParser
{
    private static readonly Dictionary<string, string> NamedChars = new()
    {
        ["space"] = " ", ["newline"] = "\n", ["tab"] = "\t", ["return"] = "\r",
        ["null"] = "\0", ["nul"] = "\0", ["alarm"] = "\a", ["backspace"] = "\b",
        ["escape"] = "\x1b", ["delete"] = "\x7f"
    };

    private static readonly Dictionary<char, char> Escapes = new()
    {
        ['t'] = '\t', ['n'] = '\n', ['r'] = '\r', ['\\'] = '\\',
        ['"'] = '"', ['0'] = '\0', ['a'] = '\a', ['b'] = '\b',
        ['f'] = '\f', ['v'] = '\v'
    };

    public static object? ParseAtom(string tok)
    {
        if (tok == "#t") return Const.TRUE;
        if (tok == "#f") return Const.FALSE;

        if (tok.StartsWith("#\\"))
        {
            var ch = tok[2..];
            if (ch.Length > 0 && ch[0] == '"')
            {
                var cp = ch == "\"" ? (int)'"' : (ch.Length > 1 && ch[^1] == '"' ? (int)ch[1..^1][0] : (int)' ');
                return new SchemeChar(cp);
            }
            if (NamedChars.TryGetValue(ch, out var nc))
                return new SchemeChar((int)nc[0]);
            if (ch.Length >= 2 && char.IsHighSurrogate(ch[0]) && char.IsLowSurrogate(ch[1]))
                return new SchemeChar(char.ConvertToUtf32(ch[0], ch[1]));
            return new SchemeChar(ch.Length > 0 ? (int)ch[0] : (int)' ');
        }

        if (tok.StartsWith("\"") || tok.StartsWith("'"))
        {
            // 三引号字符串 """...""" 或 '''...''': 去 3 个引号, 内容保留换行
            bool triple = tok.StartsWith("\"\"\"") || tok.StartsWith("'''");
            var inner = triple ? tok[3..^3] : tok[1..^1];
            var s = inner;
            var r = new List<char>();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    var nxt = s[i + 1];
                    if (Escapes.TryGetValue(nxt, out var esc)) { r.Add(esc); i++; }
                    else if (nxt == 'x')
                    {
                        i += 2;
                        var hex = "";
                        while (i < s.Length && Uri.IsHexDigit(s[i])) { hex += s[i]; i++; }
                        if (i < s.Length && s[i] == ';') i++;
                        if (hex.Length > 0)
                        {
                            var cp = Convert.ToInt32(hex, 16);
                            if (cp > 0xFFFF)
                            {
                                r.Add((char)(0xD800 | ((cp - 0x10000) >> 10)));
                                r.Add((char)(0xDC00 | ((cp - 0x10000) & 0x3FF)));
                            }
                            else
                                r.Add((char)cp);
                        }
                    }
                    else { r.Add(nxt); i++; }
                }
                else r.Add(s[i]);
            }
            var result = new string([.. r]);
            // 三引号字符串: 源码行尾的 CRLF 归一化为 LF (Scheme 标准换行)
            if (triple)
                result = result.Replace("\r\n", "\n");
            return result;
        }

        if (tok.Length > 1 && (tok[0] == 'b' || tok[0] == 'B') && tok[1..].All(c => c == '0' || c == '1'))
            return ParseBigInt(tok[1..], 2);

        var pn = ParseNumber(tok);
        if (pn is not null) return pn;

        return Sym.Intern(tok);
    }

    private static object? ParseNumber(string s)
    {
        var sl = s.ToLowerInvariant();
        if (sl is "+inf.0" or "inf.0" or "+inf") return double.PositiveInfinity;
        if (sl is "-inf.0" or "-inf") return double.NegativeInfinity;
        if (sl is "+nan.0" or "nan.0" or "nan" or "-nan.0") return double.NaN;

        int radix = 10;
        int start = 0;
        if (s[0] == '#')
        {
            if (s.Length > 1)
            {
                radix = s[1] switch { 'x' => 16, 'o' => 8, 'b' => 2, 'd' => 10, _ => 10 };
                start = 2;
            }
        }

        var numPart = s[start..];
        if (radix != 10)
            return ParseBigInt(numPart, radix);

        // Complex number with imaginary part (a+bi or a-bi or just bi)
        if (numPart.Contains('i'))
        {
            var hasSign = numPart.Contains('+') || numPart[1..].Contains('-');
            if (!hasSign)
            {
                var cs = numPart.Replace("i", "");
                var cv = ParseDoubleOrInt(cs);
                if (cv is not null)
                    return new Complex(0, Convert.ToDouble(cv));
                return null;
            }

            // Find the split between real and imag
            int splitIdx = -1;
            for (int j = 1; j < numPart.Length; j++)
            {
                if (numPart[j] == '+' || numPart[j] == '-')
                {
                    splitIdx = j;
                    break;
                }
            }
            if (splitIdx > 0)
            {
                var realPart = numPart[..splitIdx];
                var imagPart = numPart[splitIdx..].Replace("i", "");
                if (imagPart == "" || imagPart == "+") imagPart = "1";
                if (imagPart == "-") imagPart = "-1";
                var rv = ParseDoubleOrInt(realPart);
                var iv = ParseDoubleOrInt(imagPart);
                if (rv is not null && iv is not null)
                    return new Complex(Convert.ToDouble(rv), Convert.ToDouble(iv));
            }
            return null;
        }

        // Fraction with '/'
        if (numPart.Contains('/'))
        {
            var parts = numPart.Split('/');
            if (parts.Length == 2)
            {
                var nObj = ParseBigInt(parts[0], 10);
                var dObj = ParseBigInt(parts[1], 10);
                if (nObj is not null && dObj is not null)
                {
                    var nVal = ToBigInteger(nObj);
                    var dVal = ToBigInteger(dObj);
                    if (dVal != 0)
                    {
                        var f = new SchemeFraction(nVal, dVal);
                        if (f.Den == 1) return PackInt(f.Num);
                        return f;
                    }
                }
            }
        }

        var n = ParseBigInt(numPart, 10);
        if (n is not null) return n;

        if (double.TryParse(numPart, out var fv)) return fv;

        return null;
    }

    private static BigInteger ToBigInteger(object? x) => x switch
    {
        int i => i,
        long l => l,
        BigInteger b => b,
        _ => BigInteger.Zero
    };

    private static object? ParseBigInt(string s, int radix)
    {
        try
        {
            var bi = radix == 10 ? BigInteger.Parse(s) : BigIntegerParseRadix(s, radix);
            return PackInt(bi);
        }
        catch
        {
            return null;
        }
    }

    private static object? PackInt(BigInteger bi)
    {
        if (bi >= long.MinValue && bi <= long.MaxValue)
        {
            var lv = (long)bi;
            if (lv >= int.MinValue && lv <= int.MaxValue) return (int)lv;
            return lv;
        }
        return bi;
    }

    private static object? ParseDoubleOrInt(string s)
    {
        if (long.TryParse(s, out var lv))
        {
            if (lv >= int.MinValue && lv <= int.MaxValue) return (double)(int)lv;
            return (double)lv;
        }
        if (double.TryParse(s, out var fv)) return fv;
        return null;
    }

    private static BigInteger BigIntegerParseRadix(string s, int radix)
    {
        BigInteger result = 0;
        bool neg = false;
        int i = 0;
        if (s[0] == '-') { neg = true; i++; }
        else if (s[0] == '+') i++;
        for (; i < s.Length; i++)
        {
            int v = s[i] >= '0' && s[i] <= '9' ? s[i] - '0' :
                    s[i] >= 'a' && s[i] <= 'f' ? s[i] - 'a' + 10 :
                    s[i] >= 'A' && s[i] <= 'F' ? s[i] - 'A' + 10 : 0;
            result = result * radix + v;
        }
        return neg ? -result : result;
    }
}


public class ReaderState
{
    public List<string> Toks { get; }
    public int Pos { get; set; }
    public ReaderState(List<string> toks) => Toks = toks;

    public string? Peek() => Pos < Toks.Count ? Toks[Pos] : null;
    public string Next()
    {
        if (Pos >= Toks.Count) throw new Exception("unexpected EOF");
        return Toks[Pos++];
    }
}

public static class Parser
{
    public static object? Read(string s)
    {
        var toks = Tokenizer.Tokenize(s);
        if (toks.Count == 0) return null;
        return ParseExpr(new ReaderState(toks));
    }

    public static List<object?> ReadAll(string s)
    {
        var toks = Tokenizer.Tokenize(s);
        if (toks.Count == 0) return [];
        var reader = new ReaderState(toks);
        var res = new List<object?>();
        while (reader.Pos < reader.Toks.Count)
        {
            try { res.Add(ParseExpr(reader)); }
            catch (Exception) { /* r.Next() already consumed the bad token */ }
        }
        return res;
    }

    public static object? ParseExpr(ReaderState r)
    {
        var t = r.Next();
        while (t.StartsWith("#|"))
            t = r.Next();

        if (t == "#;")
        {
            ParseExpr(r);
            return ParseExpr(r);
        }

        if (t == "(") return ParseList(r);
        if (t == "#(") return ParseVector(r);
        if (t == "#u8(") return ParseBytevector(r);
        if (t == ")") throw new Exception("unexpected closing parenthesis");
        if (t == "'") return new Cell(Sym.QUOTE, new Cell(ParseExpr(r), Const.NIL));
        if (t == "`") return new Cell(Sym.QQ, new Cell(ParseExpr(r), Const.NIL));
        if (t == ",") return new Cell(Sym.UNQUOTE, new Cell(ParseExpr(r), Const.NIL));
        if (t == ",@") return new Cell(Sym.UNSPLICE, new Cell(ParseExpr(r), Const.NIL));
        if (t == "#'") return new Cell(Sym.SYNTAX, new Cell(ParseExpr(r), Const.NIL));
        if (t == "#`") return new Cell(Sym.QS, new Cell(ParseExpr(r), Const.NIL));
        if (t == "#,") return new Cell(Sym.USYNTAX, new Cell(ParseExpr(r), Const.NIL));
        if (t == "#,@") return new Cell(Sym.USPLICES, new Cell(ParseExpr(r), Const.NIL));

        if (t.StartsWith("#{") && t.EndsWith("}"))
            return ParseInfix(t[2..^1].Trim());

        return AtomParser.ParseAtom(t);
    }

    private static object? ParseList(ReaderState r)
    {
        var t = r.Peek();
        if (t is null) throw new Exception("unterminated list");
        if (t == ")") { r.Next(); return Const.NIL; }
        if (t == ".")
        {
            r.Next();
            var ce = ParseExpr(r);
            if (r.Next() == ")") return ce;
            throw new Exception("malformed dotted list");
        }

        var h = ParseExpr(r);
        var nxt = r.Peek();

        if (nxt == ".")
        {
            r.Next();
            var de = ParseExpr(r);
            if (r.Next() == ")") return new Cell(h, de);
            throw new Exception("malformed dotted list");
        }

        var d = ParseList(r);
        return new Cell(h, d);
    }

    private static object? ParseVector(ReaderState r)
    {
        var items = ParseList(r);
        var vec = new SchemeVector();
        var cur = items;
        while (cur is Cell c) { vec.Data.Add(c.Car); cur = c.Cdr; }
        return vec;
    }

    private static object? ParseBytevector(ReaderState r)
    {
        var items = ParseList(r);
        var bytes = new List<int>();
        var cur = items;
        while (cur is Cell c)
        {
            var value = c.Car;
            if (value is not int and not long) throw new Exception("bytevector literal requires integers");
            bytes.Add(NumericHelper.ToInt(value));
            cur = c.Cdr;
        }
        return new SchemeBytevector(bytes);
    }

    // ── Infix parser for #{...} ──
    private static object? ParseInfix(string s)
    {
        var tokens = InfixTokenize(s);
        if (tokens.Count == 0) return Const.NIL;
        int pos = 0;
        return ParseInfixExpr(tokens, ref pos, 0);
    }

    private static List<string> InfixTokenize(string s)
    {
        var res = new List<string>();
        int i = 0;
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i])) { i++; continue; }
            if ("+-*/%<>=!".Contains(s[i]))
            {
                string op = s[i].ToString();
                if (i + 1 < s.Length && "+-*/%<>=!".Contains(s[i + 1]))
                {
                    var two = op + s[i + 1];
                    if (two == "**" || two == "==" || two == "!=" || two == "<=" || two == ">=" || two == "+=" || two == "-=" || two == "*=" || two == "/=")
                    { op = two; i++; }
                }
                res.Add(op); i++; continue;
            }
            int start = i;
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && !"+-*/%<>=!".Contains(s[i])) i++;
            res.Add(s[start..i]);
        }
        return res;
    }

    private static readonly Dictionary<string, int> Prec = new()
    {
        ["="] = 1, ["!="] = 1, ["<"] = 1, [">"] = 1, ["<="] = 1, [">="] = 1,
        ["+"] = 2, ["-"] = 2,
        ["*"] = 3, ["/"] = 3, ["%"] = 3,
        ["**"] = 4,
    };

    private static object? ParseInfixExpr(List<string> tokens, ref int pos, int minPrec)
    {
        object? left;
        if (pos < tokens.Count && tokens[pos] == "-")
        {
            pos++;
            var rhs = ParseInfixExpr(tokens, ref pos, 3);
            left = new Cell(Sym.Intern("-"), new Cell(rhs, Const.NIL));
        }
        else
        {
            left = ParseInfixAtom(tokens, ref pos);
        }
        while (pos < tokens.Count && Prec.TryGetValue(tokens[pos], out int prec) && prec >= minPrec)
        {
            var op = tokens[pos]; pos++;
            var rhs = ParseInfixExpr(tokens, ref pos, prec + 1);
            if (op == "=")
            {
                if (left is Sym lv)
                    left = new Cell(Sym.SETBANG, new Cell(lv, new Cell(rhs, Const.NIL)));
                else
                    left = new Cell(Sym.Intern("="), new Cell(left, new Cell(rhs, Const.NIL)));
            }
            else
            {
                var opSym = op switch { "**" => Sym.Intern("expt"), "!=" => Sym.Intern("not="), _ => Sym.Intern(op) };
                left = new Cell(opSym, new Cell(left, new Cell(rhs, Const.NIL)));
            }
        }
        return left;
    }

    private static object? ParseInfixAtom(List<string> tokens, ref int pos)
    {
        if (pos >= tokens.Count) return Const.NIL;
        var t = tokens[pos]; pos++;
        return AtomParser.ParseAtom(t) ?? Sym.Intern(t);
    }
}
