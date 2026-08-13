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

    static object? PEqvQ(object?[] args)
    {
        var a = args[0];
        var b = args[1];
        if (ReferenceEquals(a, b)) return Const.TRUE;
        if (a is null || b is null) return Const.FALSE;
        // 数值：跨类型同值（如 int 1 vs long 1）也 #t，但 exact/inexact 混合必须 #f
        if (a is int or long or BigInteger or SchemeFraction or double or float or Complex
            && b is int or long or BigInteger or SchemeFraction or double or float or Complex)
        {
            if (a is Complex || b is Complex)
            {
                if (a.GetType() != b.GetType()) return Const.FALSE;
                return a.Equals(b) ? Const.TRUE : Const.FALSE;
            }
            var ta = NumericHelper.Classify(a);
            var tb = NumericHelper.Classify(b);
            var exactA = ta <= NumericHelper.NumType.Fraction;
            var exactB = tb <= NumericHelper.NumType.Fraction;
            if (exactA != exactB) return Const.FALSE;
            return NumericHelper.Compare(a, b) == 0 ? Const.TRUE : Const.FALSE;
        }
        if (a.GetType() == b.GetType())
        {
            if (a is string s) return s == (string)b ? Const.TRUE : Const.FALSE;
            if (a is SchemeChar sc) return sc.Codepoint == ((SchemeChar)b).Codepoint ? Const.TRUE : Const.FALSE;
        }
        return Const.FALSE;
    }


    static object? PListTail(object?[] args)
    {
        var n = NumericHelper.ToInt(args[1]);
        object? cur = args[0];
        for (int i = 0; i < n; i++) cur = cur is Cell c ? c.Cdr : Const.NIL;
        return cur;
    }


    static object? PAppend(object?[] args)
    {
        if (args.Length == 0) return Const.NIL;
        object? result = args[^1];
        for (int i = args.Length - 2; i >= 0; i--)
        {
            var lst = args[i];
            if (lst is Cell cc)
            {
                var items = new List<object?> { cc.Car };
                var cur = cc.Cdr;
                while (cur is Cell c) { items.Add(c.Car); cur = c.Cdr; }
                for (int j = items.Count - 1; j >= 0; j--)
                    result = new Cell(items[j], result);
            }
        }
        return result;
    }


    static object? PReverse(object?[] args)
    {
        var items = new List<object?>();
        object? cur = args[0];
        while (cur is Cell c) { items.Add(c.Car); cur = c.Cdr; }
        if (cur is not Nil) throw new Exception("reverse: not a proper list");
        return CellHelper.ToCell(items.AsEnumerable().Reverse());
    }


    static object? PListQ(object?[] args)
    {
        var x = args[0];
        if (x is Nil) return Const.TRUE;
        if (x is not Cell) return Const.FALSE;
        object? slow = x, fast = x;
        while (fast is Cell fc && fc.Cdr is Cell fcc)
        {
            slow = ((Cell)slow!).Cdr;
            fast = fcc.Cdr;
            if (ReferenceEquals(slow, fast)) return Const.FALSE;
        }
        // Loop exited because fast's cdr is not a Cell. A proper list ends
        // with the empty list (fast may be Nil or a single Cell whose cdr
        // is Nil); a dotted list ends with a non-Nil atom.
        return (fast is Nil) || (fast is Cell last && last.Cdr is Nil)
            ? Const.TRUE : Const.FALSE;
    }


    static object? PListCopy(object?[] args)
    {
        if (args[0] is Nil) return Const.NIL;
        if (args[0] is not Cell first) return args[0];
        var head = new Cell(first.Car, Const.NIL);
        var tail = head;
        object? cur = first.Cdr;
        while (cur is Cell c)
        {
            var n = new Cell(c.Car, Const.NIL);
            tail.Cdr = n;
            tail = n;
            cur = c.Cdr;
        }
        if (cur is not Nil) tail.Cdr = cur;  // 保留点对尾
        return head;
    }


    static object? PMemq(object?[] args)
    {
        object? cur = args[1];
        while (cur is Cell c) { if (ReferenceEquals(c.Car, args[0]) || c.Car?.Equals(args[0]) == true) return cur; cur = c.Cdr; }
        return Const.FALSE;
    }


    static object? PMinus(object?[] args)
    {
        if (args.Length == 0) return 0L;
        if (args.Length == 1) return NumericHelper.Negate(args[0]);
        return args.Skip(1).Aggregate((object?)args[0], (acc, x) => NumericHelper.Sub(acc!, x))!;
    }


    static object? PNumberString(object?[] args)
    {
        var radix = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 10;
        if (radix == 10) return Printer.Format(args[0]);
        var n = NumericHelper.ToBigInt(args[0]);
        if (n < 0) return "-" + ToRadixString(-n, radix);
        return ToRadixString(n, radix);
    }


    static object? PEq(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        var first = args[0];
        for (int i = 1; i < args.Length; i++)
        {
            var other = args[i];
            var firstBool = first is Sym fs && (fs.Name == "#t" || fs.Name == "#f");
            var otherBool = other is Sym os && (os.Name == "#t" || os.Name == "#f");
            if (firstBool != otherBool) return Const.FALSE;
            if (firstBool ? !ReferenceEquals(first, other) : NumericHelper.Compare(first, other) != 0)
                return Const.FALSE;
        }
        return Const.TRUE;
    }


    static object? PLt(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        for (int i = 1; i < args.Length; i++)
            if (NumericHelper.Compare(args[i - 1], args[i]) >= 0) return Const.FALSE;
        return Const.TRUE;
    }


    static object? PGt(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        for (int i = 1; i < args.Length; i++)
            if (NumericHelper.Compare(args[i - 1], args[i]) <= 0) return Const.FALSE;
        return Const.TRUE;
    }


    static object? PLe(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        for (int i = 1; i < args.Length; i++)
            if (NumericHelper.Compare(args[i - 1], args[i]) > 0) return Const.FALSE;
        return Const.TRUE;
    }


    static object? PGe(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        for (int i = 1; i < args.Length; i++)
            if (NumericHelper.Compare(args[i - 1], args[i]) < 0) return Const.FALSE;
        return Const.TRUE;
    }


    static object? PMap(object?[] args)
    {
        var fn = args[0];
        var results = new List<object?>();
        if (args.Length == 2)
        {
            object? cur = args[1];
            while (cur is Cell c) { results.Add(App(fn, c.Car)); cur = c.Cdr; }
        }
        else
        {
            var lists = new List<object?>[args.Length - 1];
            for (int i = 0; i < lists.Length; i++) lists[i] = args[i + 1].Cells();
            int minLen = lists.Min(l => l.Count);
            for (int i = 0; i < minLen; i++)
            {
                var callArgs = new object?[lists.Length];
                for (int j = 0; j < lists.Length; j++) callArgs[j] = lists[j][i];
                results.Add(App(fn, callArgs));
            }
        }
        return results.ToCell();
    }


    static object? PForEach(object?[] args)
    {
        var fn = args[0];
        if (args.Length == 2)
        {
            object? cur = args[1];
            while (cur is Cell c) { App(fn, c.Car); cur = c.Cdr; }
        }
        else
        {
            var lists = new List<object?>[args.Length - 1];
            for (int i = 0; i < lists.Length; i++) lists[i] = args[i + 1].Cells();
            int minLen = lists.Min(l => l.Count);
            for (int i = 0; i < minLen; i++)
            {
                var callArgs = new object?[lists.Length];
                for (int j = 0; j < lists.Length; j++) callArgs[j] = lists[j][i];
                App(fn, callArgs);
            }
        }
        return Const.VOID;
    }


    static object? PFilter(object?[] args)
    {
        var pred = args[0];
        var results = new List<object?>();
        object? cur = args[1];
        while (cur is Cell c) { if (App(pred, c.Car) is Sym s && s != Const.FALSE) results.Add(c.Car); cur = c.Cdr; }
        return results.ToCell();
    }


    static object? PDisplay(object?[] args)
    {
        var obj = args[0];
        object? port = null;
        if (args.Length > 1 && args[1] is ITuple t && t.Length >= 3 && t[0] is string s0 && s0 == "port" && (t[1] is "output" || t[1] is "input"))
            port = t[2];
        if (port is StreamWriter sw) { sw.Write(Printer.ToDisplayString(obj)); sw.Flush(); }
        else if (port is StringBuilder sb) { sb.Append(Printer.ToDisplayString(obj)); }
        else Console.Write(Printer.ToDisplayString(obj));
        return Const.VOID;
    }


    static object? PWriteChar(object?[] args)
    {
        var cp = AsChar(args[0]);
        var cs = char.ConvertFromUtf32(cp);
        object? port = null;
        if (args.Length > 1 && args[1] is ITuple t && t.Length >= 3 && t[0] is string s0 && s0 == "port" && (t[1] is "output" || t[1] is "input"))
            port = t[2];
        if (port is StreamWriter sw) { sw.Write(cs); sw.Flush(); }
        else if (port is StringBuilder sb) { sb.Append(cs); }
        else Console.Write(cs);
        return Const.VOID;
    }


    static object? PWrite(object?[] args)
    {
        var obj = args[0];
        object? port = null;
        if (args.Length > 1 && args[1] is ITuple t && t.Length >= 3 && t[0] is string s0 && s0 == "port" && (t[1] is "output" || t[1] is "input"))
            port = t[2];
        if (port is StreamWriter sw) { sw.Write(Printer.Format(obj)); sw.Flush(); }
        else if (port is StringBuilder sb) { sb.Append(Printer.Format(obj)); }
        else Console.Write(Printer.Format(obj));
        return Const.VOID;
    }


    static object? PError(object?[] args)
    {
        var irrList = args.Skip(1).ToList();
        throw new SchemeException(new ErrorObject(args[0], irrList.ToCell()));
    }


    static object? PSxDefmacro(object?[] args)
    {
        if (args.Length >= 3 && args[0] is Sym nameSym && args[1] is not null && args[2] is not null)
        {
            // (sx-defmacro name pattern body) — Scheme 端宏注册桥接原语。
            // 微解释器无 C# define-macro 特殊形式, my-definemacro 经此注册
            // "macro" 元组到全局环境。pattern 固定为 rest 符号 args,
            // 真正的模式解构与宏体求值在 Scheme (sx-macro-expand)。
            var defEnv = args.Length > 3 && args[3] is Env de ? de : Evaluator.GlobalEnv;
            Evaluator.GlobalEnv.Data[nameSym.Name] = ("macro", args[1], args[2], defEnv, true);
            return nameSym;
        }
        throw new Exception("sx-defmacro: expected (sx-defmacro name pattern body [env])");
    }

    static object? PSxDefinedQ(object?[] args)
    {
        var name = (args[0] as Sym)?.Name ?? args[0]?.ToString() ?? "";
        var env = args.Length > 1 && args[1] is Env e2 ? e2 : Evaluator.GlobalEnv;
        return env.LookupSilent(name, null) is not null ? Const.TRUE : Const.FALSE;
    }

}
