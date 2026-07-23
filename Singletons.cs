using System.Collections;
using System.Text;

namespace Miniscm.Types;

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
    public static int GensymCounter = 0;
}
