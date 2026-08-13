using System.Numerics;
using Miniscm.Types;
using Miniscm.Eval;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    private static object? RegisterExtVectors()
    {
        _b("vector-map", args => VectorMap(args));
        _b("vector-map!", args => VectorMapBang(args));
        _b("vector-for-each", args => VectorForEach(args));
        _b("vector-count", args => VectorCount(args[0], args[1]));
        _b("vector-any", args => VectorAnyEvery(args, false));
        _b("vector-every", args => VectorAnyEvery(args, true));
        _b("vector-fold", args => VectorFold(args, false));
        _b("vector-fold-right", args => VectorFold(args, true));
        _b("vector-unfold", args => VectorUnfold(args));
        _b("vector-index", args => VectorIndex(args[0], args[1]));
        _b("vector-skip", args => VectorSkip(args[0], args[1]));
        _b("vector-swap!", args => VectorSwap(args));
        _b("vector-reverse!", args => VectorReverseBang(args));
        _b("vector-empty?", args => ((SchemeVector)args[0]!).Length == 0 ? Const.TRUE : Const.FALSE);
        _b("vector-append", args => VectorAppend(args));
        _b("vector-copy", args => VectorCopy(args));
        _b("vector-copy!", args => VectorCopyBang(args));
        _b("vector-concatenate", args => VectorConcat(args[0]));
        _b("vector-reverse", args => VectorReverse(args));
        _b("vector-sort", args => VectorSort(args));
        _b("vector=", args => VectorEqual(args));
        _b("reverse-list->vector", args =>
        {
            var items = args[0].Cells().ToList();
            items.Reverse();
            return new SchemeVector(items);
        });
        _b("vector-fill!", args =>
        {
            var v = (SchemeVector)args[0]!;
            int start = args.Length > 2 ? NumericHelper.ToInt(args[2]) : 0;
            int end = args.Length > 3 ? NumericHelper.ToInt(args[3]) : v.Length;
            for (int i = start; i < end && i < v.Length; i++) v[i] = args[1];
            return Const.VOID;
        });
        _b("vector-count", args => VectorCount(args[0], args[1]));
        return Const.VOID;
    }

    private static object? VectorMap(object?[] args)
    {
        var fn = args[0];
        var vdata = args[1..].Select(a => ((SchemeVector)a!).Data).ToList();
        int len = vdata[0].Count;
        var result = new List<object?>();
        for (int i = 0; i < len; i++)
        {
            var cargs = vdata.Select(d => d[i]).ToArray();
            result.Add(App(fn, cargs));
        }
        return new SchemeVector(result);
    }

    private static object? VectorMapBang(object?[] args)
    {
        var fn = args[0];
        var v = (SchemeVector)args[1]!;
        for (int i = 0; i < v.Length; i++) v[i] = App(fn, (long)i, v[i]);
        return v;
    }

    private static object? VectorForEach(object?[] args)
    {
        var fn = args[0];
        var vdata = args[1..].Select(a => ((SchemeVector)a!).Data).ToList();
        int len = vdata[0].Count;
        for (int i = 0; i < len; i++)
        {
            var cargs = vdata.Select(d => d[i]).ToArray();
            App(fn, cargs);
        }
        return Const.VOID;
    }

    private static object? VectorCount(object? pred, object? v)
    {
        int n = 0;
        foreach (var x in ((SchemeVector)v!).Data)
            if (ReferenceEquals(App(pred, x), Const.TRUE)) n++;
        return (long)n;
    }

    private static object? VectorAnyEvery(object?[] args, bool every)
    {
        var pred = args[0];
        foreach (var x in ((SchemeVector)args[1]!).Data)
        {
            var r = App(pred, x);
            if (every) { if (ReferenceEquals(r, Const.FALSE)) return Const.FALSE; }
            else { if (!ReferenceEquals(r, Const.FALSE)) return Const.TRUE; }
        }
        return every ? Const.TRUE : Const.FALSE;
    }

    private static object? VectorFold(object?[] args, bool right)
    {
        var fn = args[0];
        object? acc = args[1];
        var data = ((SchemeVector)args[2]!).Data;
        if (right)
        {
            for (int i = data.Count - 1; i >= 0; i--) acc = App(fn, (long)i, data[i], acc);
        }
        else
        {
            for (int i = 0; i < data.Count; i++) acc = App(fn, (long)i, data[i], acc);
        }
        return acc;
    }

    private static object? VectorUnfold(object?[] args)
    {
        var fn = args[0];
        int n = NumericHelper.ToInt(args[1]);
        object? s = args[2];
        var result = new List<object?>();
        for (int i = 0; i < n; i++)
        {
            var r = App(fn, (long)i, s);
            if (r is Cell c) { result.Add(c.Car); s = c.Cdr; }
            else if (r is System.Runtime.CompilerServices.ITuple t && t.Length >= 2)
            {
                result.Add(t[0]);
                if (t.Length == 2) s = t[1];
                else
                {
                    object? tail = Const.NIL;
                    for (int j = t.Length - 1; j >= 1; j--)
                        tail = new Cell(t[j], tail);
                    s = tail;
                }
            }
            else { result.Add(r); s = r; }
        }
        return new SchemeVector(result);
    }

    private static object? VectorIndex(object? pred, object? v)
    {
        var data = ((SchemeVector)v!).Data;
        for (int i = 0; i < data.Count; i++)
            if (!ReferenceEquals(App(pred, data[i]), Const.FALSE)) return (long)i;
        return Const.FALSE;
    }

    private static object? VectorSkip(object? pred, object? v)
    {
        var data = ((SchemeVector)v!).Data;
        for (int i = 0; i < data.Count; i++)
            if (ReferenceEquals(App(pred, data[i]), Const.FALSE)) return (long)i;
        return (long)data.Count;
    }

    private static object? VectorSwap(object?[] args)
    {
        var v = (SchemeVector)args[0]!;
        int i = NumericHelper.ToInt(args[1]);
        int j = NumericHelper.ToInt(args[2]);
        (v[j], v[i]) = (v[i], v[j]);
        return Const.VOID;
    }

    private static object? VectorReverseBang(object?[] args)
    {
        var v = (SchemeVector)args[0]!;
        for (int i = 0, j = v.Length - 1; i < j; i++, j--)
            (v[i], v[j]) = (v[j], v[i]);
        return Const.VOID;
    }

    private static object? VectorAppend(object?[] args)
    {
        var result = new List<object?>();
        foreach (var v in args) result.AddRange(((SchemeVector)v!).Data);
        return new SchemeVector(result);
    }

    private static object? VectorCopy(object?[] args)
    {
        var v = (SchemeVector)args[0]!;
        int start = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
        int end = args.Length > 2 ? NumericHelper.ToInt(args[2]) : v.Length;
        return new SchemeVector(v.Data.GetRange(start, end - start));
    }

    private static object? VectorCopyBang(object?[] args)
    {
        var target = (SchemeVector)args[0]!;
        int tstart = NumericHelper.ToInt(args[1]);
        var src = (SchemeVector)args[2]!;
        int sstart = args.Length > 3 ? NumericHelper.ToInt(args[3]) : 0;
        int send = args.Length > 4 ? NumericHelper.ToInt(args[4]) : src.Length;
        for (int i = sstart; i < send; i++)
        {
            int idx = tstart + i - sstart;
            if (idx < target.Length) target[idx] = src[i];
        }
        return Const.VOID;
    }

    private static object? VectorConcat(object? vecs)
    {
        var result = new List<object?>();
        foreach (var v in vecs.Cells())
        {
            if (v is SchemeVector sv) result.AddRange(sv.Data);
        }
        return new SchemeVector(result);
    }

    private static object? VectorReverse(object?[] args)
    {
        var v = (SchemeVector)args[0]!;
        int start = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
        int end = args.Length > 2 ? NumericHelper.ToInt(args[2]) : v.Length;
        var slice = v.Data.GetRange(start, end - start);
        slice.Reverse();
        var result = new List<object?>(v.Data);
        for (int i = 0; i < slice.Count; i++) result[start + i] = slice[i];
        return new SchemeVector(result);
    }

    private static object? VectorSort(object?[] args)
    {
        var pred = args[0];
        var v = (SchemeVector)args[1]!;
        var items = v.Data.ToList();
        StableSortC(items, pred);
        return new SchemeVector(items);
    }

    private static object? VectorEqual(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        var eq = args[0];
        var first = ((SchemeVector)args[1]!).Data;
        for (int k = 2; k < args.Length; k++)
        {
            var other = ((SchemeVector)args[k]!).Data;
            if (other.Count != first.Count) return Const.FALSE;
            for (int i = 0; i < first.Count; i++)
                if (!ReferenceEquals(App(eq, first[i], other[i]), Const.TRUE)) return Const.FALSE;
        }
        return Const.TRUE;
    }
}
