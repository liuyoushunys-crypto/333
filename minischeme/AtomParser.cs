using System.Numerics;
using Miniscm.Types;

namespace Miniscm.Reader;

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

        if (tok.StartsWith("\""))
        {
            var s = tok[1..^1];
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
            return new string([.. r]);
        }

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
