using Miniscm.Types;

namespace Miniscm.Reader;

public static class InfixParser
{
    private static readonly Dictionary<string, (int Prec, string Assoc)> InfixOps = new()
    {
        ["="] = (1, "left"), ["!="] = (1, "left"),
        ["<"] = (1, "left"), [">"] = (1, "left"),
        ["<="] = (1, "left"), [">="] = (1, "left"),
        ["+"] = (2, "left"), ["-"] = (2, "left"),
        ["*"] = (3, "left"), ["/"] = (3, "left"),
        ["//"] = (3, "left"), ["%"] = (3, "left"),
        ["^"] = (4, "right"), ["**"] = (4, "right"),
        ["+="] = (0, "right"), ["-="] = (0, "right"),
        ["*="] = (0, "right"), ["/="] = (0, "right"),
    };

    public static object? Parse(string src)
    {
        var toks = InfixLex(src);
        if (toks.Count == 0) return Const.NIL;
        return new InfixEngine(toks).Parse();
    }

    private static List<string> InfixLex(string src)
    {
        var tokens = new List<string>();
        int i = 0, n = src.Length;
        while (i < n)
        {
            var c = src[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == ';') { while (i < n && src[i] != '\n') i++; continue; }

            if (i + 1 < n && src[i..(i + 2)] is "**" or "//" or "<=" or ">=" or "!=" or "+=" or "-=" or "*=" or "/=")
            { tokens.Add(src[i..(i + 2)]); i += 2; continue; }

            if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(src[i + 1])))
            {
                int j = i; bool hasDot = false;
                while (j < n && (char.IsDigit(src[j]) || (src[j] == '.' && !hasDot))) { if (src[j] == '.') hasDot = true; j++; }
                if (j < n && src[j] is 'e' or 'E')
                {
                    j++;
                    if (j < n && src[j] is '+' or '-') j++;
                    while (j < n && char.IsDigit(src[j])) j++;
                }
                tokens.Add(src[i..j]); i = j; continue;
            }

            if (char.IsLetter(c) || c is '_' or '*' or '?' or '!' or '$' or '%' or '&')
            {
                int j = i;
                while (j < n && (char.IsLetterOrDigit(src[j]) || src[j] is '_' or '.' or '*' or '?' or '!' or '$' or '%' or '&' or '+' or '-' or '@' or '^' or '~'))
                    j++;
                tokens.Add(src[i..j]); i = j; continue;
            }

            if (c is '(' or ')' or '+' or '-' or '*' or '/' or '^' or '%' or '=' or '!' or '<' or '>' or ',' or '[' or ']')
            { tokens.Add(c.ToString()); i++; continue; }

            if (c == '#')
            {
                int j = i + 1;
                if (j < n && src[j] is 't' or 'f') j++;
                tokens.Add(src[i..j]); i = j; continue;
            }

            i++;
        }
        return tokens;
    }

    private class InfixEngine(List<string> tokens)
    {
        private int _pos = 0;

        private string? Peek() => _pos < tokens.Count ? tokens[_pos] : null;
        private string Next() => tokens[_pos++];

        public object? Parse() => Expr(0);

        private object? Expr(int minPrec)
        {
            var left = Primary();
            while (true)
            {
                var tok = Peek();
                if (tok is null or ")" or "}" or ",") break;
                if (!InfixOps.TryGetValue(tok, out var info)) break;
                if (info.Prec < minPrec) break;
                Next();
                var nxt = info.Assoc == "right" ? info.Prec : info.Prec + 1;
                var right = Expr(nxt);

                if (tok == "=")
                {
                    if (left is not Sym ls) throw new Exception($"Invalid lvalue: {left}");
                    left = new Cell(Sym.Intern("set!"), new Cell(ls, new Cell(right, Const.NIL)));
                    continue;
                }

                string? assignTarget = tok switch
                {
                    "+=" => "+", "-=" => "-", "*=" => "*", "/=" => "/",
                    _ => null
                };
                if (assignTarget is not null)
                {
                    if (left is not Sym ls2) throw new Exception($"Invalid lvalue: {left}");
                    left = new Cell(Sym.Intern("set!"), new Cell(ls2,
                        new Cell(new Cell(Sym.Intern(assignTarget), new Cell(ls2, new Cell(right, Const.NIL))), Const.NIL)));
                    continue;
                }

                var schemeOp = tok switch
                {
                    "^" or "**" => "expt",
                    "//" => "quotient",
                    "%" => "modulo",
                    "!=" => "not=",
                    _ => tok
                };
                left = new Cell(Sym.Intern(schemeOp), new Cell(left, new Cell(right, Const.NIL)));
            }
            return left;
        }

        private object? Primary()
        {
            var tok = Peek() ?? throw new Exception("Unexpected end of infix expression");
            if (tok == "(")
            {
                Next();
                var expr = Expr(0);
                if (Next() != ")") throw new Exception("Expected ')'");
                return expr;
            }
            if (tok == "-")
            {
                Next();
                return new Cell(Sym.Intern("-"), new Cell(Expr(5), Const.NIL));
            }
            if (tok == "+")
            {
                Next();
                return Expr(5);
            }

            var raw = Next();
            if (double.TryParse(raw, out var fv)) return fv;
            if (long.TryParse(raw, out var iv)) return iv;
            if (raw == "#t") return Const.TRUE;
            if (raw == "#f") return Const.FALSE;
            return Sym.Intern(raw);
        }
    }
}
