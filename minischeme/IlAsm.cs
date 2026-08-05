using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Miniscm;

static class IlAsm
{
    static readonly Dictionary<ushort, OpCode> Map = BuildMap();

    static Dictionary<ushort, OpCode> BuildMap()
    {
        var d = new Dictionary<ushort, OpCode>();
        foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.FieldType != typeof(OpCode)) continue;
            var op = (OpCode)f.GetValue(null)!;
            d[(ushort)op.Value] = op;
        }
        return d;
    }

    public static string Disassemble(MethodBase m, LambdaExpression? debugExpr = null)
    {
        var body = SafeGetBody(m);
        if (body is null || body.GetILAsByteArray() is not byte[] il || il.Length == 0)
        {
            // DynamicMethod (表达式树编译产物) 无 IL body → 输出 Expression DebugView (伪 IL)
            if (debugExpr is not null)
            {
                try
                {
                    var prop = typeof(Expression).GetProperty("DebugView",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    return (string)prop!.GetValue(debugExpr)!;
                }
                catch { }
            }
            return "(no IL body)";
        }
        var sb = new StringBuilder();
        sb.Append($".method {m.DeclaringType?.Name}.{m.Name}(");
        sb.Append(string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name)));
        sb.AppendLine(")");
        var locs = body.LocalVariables;
        if (locs.Count > 0)
            sb.AppendLine("  .locals init (" +
                string.Join(", ", locs.Select((l, i) => $"[{i}] {l.LocalType.Name}")) + ")");

        var targets = CollectBranchTargets(il);

        int i = 0;
        while (i < il.Length)
        {
            long start = i;
            var op = ReadOp(il, ref i);
            if (targets.Contains(start))
                sb.Append($"IL_{start:X4}:");
            else
                sb.Append("       ");
            sb.Append($"  {op.Name}");
            EmitOperand(sb, op, m, il, ref i, start);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    static MethodBody? SafeGetBody(MethodBase m)
    {
        try { return m.GetMethodBody(); }
        catch { return null; }   // DynamicMethod 不支持
    }

    static HashSet<long> CollectBranchTargets(byte[] il)
    {
        var targets = new HashSet<long>();
        int i = 0;
        while (i < il.Length)
        {
            var op = ReadOp(il, ref i);
            switch (op.OperandType)
            {
                case OperandType.ShortInlineBrTarget:
                    targets.Add(i + 1 + (sbyte)il[i]); i += 1; break;
                case OperandType.InlineBrTarget:
                    targets.Add(i + 4 + BitConverter.ToInt32(il, i)); i += 4; break;
                case OperandType.InlineSwitch:
                {
                    int n = BitConverter.ToInt32(il, i); i += 4;
                    int baseOff = i + n * 4;
                    for (int k = 0; k < n; k++)
                        targets.Add(baseOff + BitConverter.ToInt32(il, i + k * 4));
                    i += n * 4; break;
                }
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineI: case OperandType.ShortInlineVar: i += 1; break;
                case OperandType.InlineVar: i += 2; break;
                case OperandType.InlineI: case OperandType.InlineString:
                case OperandType.InlineMethod: case OperandType.InlineField:
                case OperandType.InlineType: case OperandType.InlineTok:
                case OperandType.InlineSig: i += 4; break;
                case OperandType.InlineI8: case OperandType.InlineR: i += 8; break;
                case OperandType.ShortInlineR: i += 4; break;
            }
        }
        return targets;
    }

    static OpCode ReadOp(byte[] il, ref int i)
    {
        ushort code = il[i++];
        if (code == 0xFE)
            code = (ushort)(0xFE00 | il[i++]);
        return Map[code];
    }

    static void EmitOperand(StringBuilder sb, OpCode op, MethodBase m,
        byte[] il, ref int i, long start)
    {
        switch (op.OperandType)
        {
            case OperandType.InlineNone:
                break;
            case OperandType.ShortInlineI:
                sb.Append($" {(sbyte)il[i]}"); i += 1; break;
            case OperandType.ShortInlineVar:
                sb.Append($" {il[i]}"); i += 1; break;
            case OperandType.ShortInlineBrTarget:
                sb.Append($" IL_{start + i + 1 + (sbyte)il[i]:X4}"); i += 1; break;
            case OperandType.InlineVar:
                sb.Append($" {BitConverter.ToUInt16(il, i)}"); i += 2; break;
            case OperandType.InlineI:
                sb.Append($" {BitConverter.ToInt32(il, i)}"); i += 4; break;
            case OperandType.InlineBrTarget:
                sb.Append($" IL_{start + i + 4 + BitConverter.ToInt32(il, i):X4}"); i += 4; break;
            case OperandType.InlineI8:
                sb.Append($" {BitConverter.ToInt64(il, i)}"); i += 8; break;
            case OperandType.ShortInlineR:
                sb.Append($" {BitConverter.ToSingle(il, i)}"); i += 4; break;
            case OperandType.InlineR:
                sb.Append($" {BitConverter.ToDouble(il, i)}"); i += 8; break;
            case OperandType.InlineString:
            {
                int tok = BitConverter.ToInt32(il, i);
                try { sb.Append($" \"{m.Module.ResolveString(tok)}\""); }
                catch { sb.Append($" {tok:X8}"); }
                i += 4; break;
            }
            case OperandType.InlineMethod:
            case OperandType.InlineField:
            case OperandType.InlineType:
            case OperandType.InlineTok:
            case OperandType.InlineSig:
            {
                int tok = BitConverter.ToInt32(il, i);
                try { sb.Append($" {m.Module.ResolveMember(tok)}"); }
                catch { sb.Append($" {tok:X8}"); }
                i += 4; break;
            }
            case OperandType.InlineSwitch:
            {
                int n = BitConverter.ToInt32(il, i); i += 4;
                int baseOff = i + n * 4;
                sb.Append(" (");
                for (int k = 0; k < n; k++)
                {
                    if (k > 0) sb.Append(", ");
                    sb.Append($"IL_{baseOff + BitConverter.ToInt32(il, i + k * 4):X4}");
                }
                sb.Append(')');
                i += n * 4; break;
            }
        }
    }
}
