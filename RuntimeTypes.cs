namespace Miniscm.Types;

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
}

public sealed class TailCall
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

public sealed class LambdaProc
{
    public List<string> Params { get; }
    public object? Body { get; }
    public Env ClosureEnv { get; }
    public bool IsSimple { get; }
    public string? Name { get; set; }
    public int CallCount { get; set; }
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
