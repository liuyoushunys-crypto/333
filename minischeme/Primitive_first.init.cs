using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Miniscm.Types;
using Miniscm.Eval;
using Miniscm.Compiler;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    private static object? PSxExpandCall(object?[] args)
    {
        if (args.Length >= 1 && args[0] is Cell call)
        {
            var env = args.Length > 1 && args[1] is Env e2 ? e2 : Evaluator.GlobalEnv;
            var op = call.Car;
            if (op is Sym ops)
            {
                var proc = env.LookupSilent(ops.Name, null);
                if (proc is not null)
                {
                    var expanded = Evaluator.ExpandMacro(proc, call.Cdr, env);
                    if (expanded is not null) return expanded;
                }
            }
        }
        return Const.FALSE;
    }
}
