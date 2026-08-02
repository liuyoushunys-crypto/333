using System.Text.RegularExpressions;
using Miniscm.Types;

namespace Miniscm.Reader;

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
