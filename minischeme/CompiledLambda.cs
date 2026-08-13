using Miniscm.Base;
using Miniscm.Types;

namespace Miniscm.Compiler;

public sealed class CompiledLambda
{
    public Func<Env, object?[], object?> PyFunc { get; }
    public List<string> Params { get; }
    public Env Env { get; }
    public bool IsSimple { get; }
    public int NRegular { get; }

    public CompiledLambda(Func<Env, object?[], object?> pyFunc, List<string> @params, Env env, bool isSimple)
    {
        PyFunc = pyFunc;
        Params = @params;
        Env = env;
        IsSimple = isSimple;
        NRegular = isSimple ? @params.Count : @params.Count - 1;
    }

    public object? Invoke(Env env, object?[] args)
    {
        if (IsSimple)
            return PyFunc(env, args);
        var n = NRegular;
        var regular = new object?[n + 1];
        Array.Copy(args, regular, Math.Min(n, args.Length));
                object? restArgs = Const.NIL;
        for (int i = args.Length - 1; i >= n; i--)
            restArgs = new Cell(args[i], restArgs);
        regular[n] = restArgs;
        return PyFunc(env, regular);
    }
}
