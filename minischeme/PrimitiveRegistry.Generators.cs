using System.Text;
using Miniscm.Types;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    static object? GenNext(object? g)
    {
        if (g is Func<object?> f) return f();
        if (g is Func<object?[], object?> fa) return fa(System.Array.Empty<object?>());
        throw new Exception("not a generator");
    }

    // generator: (generator v ...) — 生成返回各值的 thunk
    static object? PGenerator(object?[] args)
    {
        var vals = args.ToList();
        int idx = 0;
        return (Func<object?>)(() => idx < vals.Count ? vals[idx++] : Const.EOF);
    }

    // make-generator: (make-generator gen) — 恒等
    static object? PMakeGenerator(object?[] args) => args[0];

    // list->generator: 列表转 generator
    static object? PListGenerator(object?[] args)
    {
        var items = new List<object?>();
        object? cur = args[0];
        while (cur is Cell c) { items.Add(c.Car); cur = c.Cdr; }
        int idx = 0;
        return (Func<object?>)(() => idx < items.Count ? items[idx++] : Const.EOF);
    }

    // vector->generator
    static object? PVectorGenerator(object?[] args)
    {
        var items = args[0] is SchemeVector sv ? sv.Data.ToList() : new List<object?>();
        int idx = 0;
        return (Func<object?>)(() => idx < items.Count ? items[idx++] : Const.EOF);
    }

    // string->generator
    static object? PStringGenerator(object?[] args)
    {
        var items = ToStr(args[0]).EnumerateRunes().Select(r => (object?)new SchemeChar(r.Value)).ToList();
        int idx = 0;
        return (Func<object?>)(() => idx < items.Count ? items[idx++] : Const.EOF);
    }

    // generator->list
    static object? PGeneratorToList(object?[] args)
    {
        var results = new List<object?>();
        while (true) { var v = GenNext(args[0]); if (v is Eof) break; results.Add(v); }
        return results.ToCell();
    }

    // generator->vector
    static object? PGeneratorToVector(object?[] args)
    {
        var results = new List<object?>();
        while (true) { var v = GenNext(args[0]); if (v is Eof) break; results.Add(v); }
        return new SchemeVector(results);
    }

    // generator->string
    static object? PGeneratorToString(object?[] args)
    {
        var sb = new StringBuilder();
        while (true) { var v = GenNext(args[0]); if (v is Eof) break; sb.Append(char.ConvertFromUtf32(AsChar(v))); }
        return new SchemeString(sb.ToString());
    }

    // make-range-generator: (make-range-generator start end [step])
    static object? PMakeRangeGenerator(object?[] args)
    {
        var start = NumericHelper.ToLong(args[0]);
        var end = NumericHelper.ToLong(args[1]);
        var step = args.Length > 2 ? NumericHelper.ToLong(args[2]) : 1;
        long cur = start;
        return (Func<object?>)(() =>
        {
            if (step > 0 ? cur < end : cur > end) { var v = cur; cur += step; return v; }
            return Const.EOF;
        });
    }

    // make-iota-generator: (make-iota-generator n [step [start]])
    static object? PMakeIotaGenerator(object?[] args)
    {
        var n = NumericHelper.ToLong(args[0]);
        var step = args.Length > 1 ? NumericHelper.ToLong(args[1]) : 1;
        var start = args.Length > 2 ? NumericHelper.ToLong(args[2]) : 0;
        long cnt = 0;
        return (Func<object?>)(() => cnt < n ? (start + cnt++ * step) : Const.EOF);
    }

    // generator-map: (generator-map fn g)
    static object? PGeneratorMap(object?[] args)
    {
        var fn = args[0];
        var g = args[1];
        return (Func<object?>)(() =>
        {
            var v = GenNext(g);
            return v is Eof ? Const.EOF : App(fn, v);
        });
    }

    // generator-filter: (generator-filter pred g)
    static object? PGeneratorFilter(object?[] args)
    {
        var pred = args[0];
        var g = args[1];
        return (Func<object?>)(() =>
        {
            while (true)
            {
                var v = GenNext(g);
                if (v is Eof) return Const.EOF;
                if (Truthy(App(pred, v))) return v;
            }
        });
    }

    // generator-take: (generator-take g n)
    static object? PGeneratorTake(object?[] args)
    {
        var g = args[0];
        var n = NumericHelper.ToLong(args[1]);
        long cnt = 0;
        return (Func<object?>)(() =>
        {
            if (cnt >= n) return Const.EOF;
            var v = GenNext(g);
            if (v is Eof) return Const.EOF;
            cnt++;
            return v;
        });
    }

    // generator-count: (generator-count pred g)
    static object? PGeneratorCount(object?[] args)
    {
        var pred = args[0];
        var g = args[1];
        long cnt = 0;
        while (true) { var v = GenNext(g); if (v is Eof) break; if (Truthy(App(pred, v))) cnt++; }
        return cnt;
    }

    // generator-find: (generator-find pred g)
    static object? PGeneratorFind(object?[] args)
    {
        var pred = args[0];
        var g = args[1];
        while (true) { var v = GenNext(g); if (v is Eof) return Const.EOF; if (Truthy(App(pred, v))) return v; }
    }

    // generator-for-each: (generator-for-each fn g)
    static object? PGeneratorForEach(object?[] args)
    {
        var fn = args[0];
        var g = args[1];
        while (true) { var v = GenNext(g); if (v is Eof) break; App(fn, v); }
        return Const.VOID;
    }
}
