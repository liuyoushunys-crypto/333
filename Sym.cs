namespace Miniscm.Types;

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
    public static readonly Sym DM = Intern("define-macro");
    public static readonly Sym DS = Intern("define-syntax");
    public static readonly Sym SR = Intern("syntax-rules");
    public static readonly Sym LS = Intern("let-syntax");
    public static readonly Sym LRS = Intern("letrec-syntax");
    public static readonly Sym APPLY = Intern("apply");
    public static readonly Sym ARGS = Intern("args");
    public static readonly Sym IMPORT = Intern("import");
    public static readonly Sym SC = Intern("syntax-case");
    public static readonly Sym WS = Intern("with-syntax");
    public static readonly Sym GT = Intern("generate-temporaries");
    public static readonly Sym DEBUG = Intern("%break");
    public static readonly Sym DBGTRACE = Intern("debug-trace");
    public static readonly Sym SETF = Intern("set!-form");
    public static readonly Sym USCORE = Intern("_");
    public static readonly Sym VOID_SYM = Intern("if #f #f");
    public static readonly Sym LT = Intern("<>");
    public static readonly Sym LT3 = Intern("<...>");
    public static readonly Sym TRUE = Intern("#t");
    public static readonly Sym FALSE = Intern("#f");
}
