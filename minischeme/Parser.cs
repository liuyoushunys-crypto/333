using Miniscm.Types;

namespace Miniscm.Reader;

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
            else if (op == "+=" || op == "-=" || op == "*=" || op == "/=")
            {
                var sop = op[0] switch { '+' => "+", '-' => "-", '*' => "*", '/' => "/", _ => "+" };
                var opSym = Sym.Intern(sop);
                if (left is Sym lv)
                    left = new Cell(Sym.SETBANG, new Cell(lv, new Cell(new Cell(opSym, new Cell(lv, new Cell(rhs, Const.NIL))), Const.NIL)));
                else
                    left = new Cell(Sym.Intern(op), new Cell(left, new Cell(rhs, Const.NIL)));
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
