namespace Miniscm.Types;

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
