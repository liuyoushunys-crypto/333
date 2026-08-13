using System.Numerics;
using Miniscm.Types;
using Miniscm.Eval;
using Miniscm.Compiler;

namespace Miniscm.Primitives;

// Native counterparts for the ordinary procedures in the twelve extension
// libraries.  These types are deliberately mutable: Scheme record mutators
// must observe the same object as all existing references.
public sealed class SchemeHook { public List<object?> Procedures { get; set; } = []; }
public sealed class SchemeRandomSource { public long State { get; set; } public SchemeRandomSource(long state) => State = state; }
public sealed class SchemeListQueue { public List<object?> Items { get; } = []; }
public sealed class SchemeBinaryHeap
{
    public List<object?> Items { get; } = [];
    public object? Comparator { get; set; }
}
public sealed class SchemeBimap
{
    public Dictionary<object, object?> Forward { get; } = [];
    public Dictionary<object, object?> Reverse { get; } = [];
}
public sealed class SchemeDeque { public List<object?> Items { get; } = []; }
public sealed class SchemeArray { public SchemeVector Value { get; } public SchemeArray(SchemeVector value) => Value = value; }

public static partial class PrimitiveRegistry
{
    private static object? S12True(object? x) => Truthy(x) ? Const.TRUE : Const.FALSE;
    private static object? S12List(object? x) => x is Cell ? x.Cells() : [];
    private static SchemeString S12String(object? x) => x is SchemeString s ? s : new SchemeString(ToStr(x));

    private static object? S12LastPair(object? x)
    {
        if (x is not Cell c) throw new SchemeException("last-pair: expected pair");
        while (c.Cdr is Cell n) c = n;
        return c;
    }

    private static bool S12Eq(object? a, object? b) => ReferenceEquals(a, b) || Equals(a, b);
    private static object? S12Call(object? p, params object?[] a) => App(p, a);

    private static object? S12Record(object? tag, params object?[] fields) => new Cell(tag, fields.ToCell());
    private static bool S12Tag(object? x, string tag) => x is Cell c && c.Car is Sym s && s.Name == tag;
    private static object? S12Field(object? x, int i) => x is Cell c && c.Cdr is Cell f ? f.Cells().ElementAt(i) : throw new SchemeException("record field");
    private static object? S12SetField(object? x, int i, object? value)
    {
        if (x is not Cell c || c.Cdr is not Cell f) throw new SchemeException("record field");
        for (int n = 0; n < i; n++) f = (Cell)f.Cdr!;
        f.Car = value;
        return Const.VOID;
    }

    private static object? S12ListQueue(object?[] args)
    {
        var q = new SchemeListQueue();
        if (args.Length == 1 && args[0] is Cell)
            q.Items.AddRange(args[0].Cells());
        else if (args.Length == 1 && args[0] is Nil)
            return q;
        else
            q.Items.AddRange(args);
        return q;
    }
    private static object? S12QueueRemove(object? q, bool back)
    {
        var queue = (SchemeListQueue)q!;
        if (queue.Items.Count == 0) throw new SchemeException("list-queue-remove!: empty queue");
        var i = back ? queue.Items.Count - 1 : 0;
        var value = queue.Items[i]; queue.Items.RemoveAt(i); return value;
    }
    private static bool S12HeapLess(SchemeBinaryHeap heap, object? a, object? b)
        => Truthy(S12Call(heap.Comparator, a, b));
    private static void S12HeapUp(SchemeBinaryHeap heap, int i)
    {
        while (i > 0)
        {
            var parent = (i - 1) / 2;
            if (!S12HeapLess(heap, heap.Items[i], heap.Items[parent])) break;
            (heap.Items[i], heap.Items[parent]) = (heap.Items[parent], heap.Items[i]);
            i = parent;
        }
    }
    private static void S12HeapDown(SchemeBinaryHeap heap, int i)
    {
        while (true)
        {
            var best = i; var left = i * 2 + 1; var right = left + 1;
            if (left < heap.Items.Count && S12HeapLess(heap, heap.Items[left], heap.Items[best])) best = left;
            if (right < heap.Items.Count && S12HeapLess(heap, heap.Items[right], heap.Items[best])) best = right;
            if (best == i) return;
            (heap.Items[i], heap.Items[best]) = (heap.Items[best], heap.Items[i]); i = best;
        }
    }
    private static void S12Heapify(SchemeBinaryHeap heap)
    { for (int i = heap.Items.Count / 2 - 1; i >= 0; i--) S12HeapDown(heap, i); }
    private static object? S12ArrayBuild(List<int> dims, int at, object? fill)
    {
        var v = new SchemeVector(dims[at]);
        for (int i = 0; i < v.Length; i++) v[i] = at == dims.Count - 1 ? fill : S12ArrayBuild(dims, at + 1, fill);
        return v;
    }
    private static SchemeVector S12ArrayValue(object? x) => x is SchemeArray a ? a.Value : (SchemeVector)x!;
    private static object? S12ArrayRef(object? array, object?[] indices)
    {
        object? cur = S12ArrayValue(array);
        foreach (var index in indices) cur = ((SchemeVector)cur!)[NumericHelper.ToInt(index)];
        return cur;
    }
    private static object? S12ArraySet(object? array, object? value, object?[] indices)
    {
        if (indices.Length == 0) throw new SchemeException("array-set!: no indices");
        object? cur = S12ArrayValue(array);
        for (int i = 0; i < indices.Length - 1; i++) cur = ((SchemeVector)cur!)[NumericHelper.ToInt(indices[i])];
        ((SchemeVector)cur!)[NumericHelper.ToInt(indices[^1])] = value;
        return Const.VOID;
    }
    private static object? S12ArrayDims(object? x)
    {
        var result = new List<object?>(); object? cur = S12ArrayValue(x);
        while (cur is SchemeVector v) { result.Add((long)v.Length); cur = v.Length == 0 ? null : v[0]; }
        return result.ToCell();
    }


    private static object? S12StringMap(object?[] args){var strings=args[1..].Select(x=>S12String(x).ToString().EnumerateRunes().ToList()).ToList();var n=strings.Min(x=>x.Count);var chars=new List<int>();for(int i=0;i<n;i++)chars.Add(AsChar(App(args[0],strings.Select(x=>(object?)new SchemeChar(x[i].Value)).ToArray())));return new SchemeString(chars);}
    private static object? S12StringForEach(object?[] args){var strings=args[1..].Select(x=>S12String(x).ToString().EnumerateRunes().ToList()).ToList();var n=strings.Min(x=>x.Count);for(int i=0;i<n;i++)App(args[0],strings.Select(x=>(object?)new SchemeChar(x[i].Value)).ToArray());return Const.VOID;}
    private static object? S12StringQuantifier(object?[] args,bool every){var s=S12String(args[1]).ToString();object? last=Const.TRUE;foreach(var rune in s.EnumerateRunes()){var r=App(args[0],new SchemeChar(rune.Value));if(every){if(!Truthy(r))return Const.FALSE;last=r;}else if(Truthy(r))return r;}return every?last:Const.FALSE;}
    private static object? S12Trim(object?[] args,int mode){var s=S12String(args[0]).ToString();return new SchemeString(mode==0?s.Trim():mode==1?s.TrimEnd():s.Trim());}
    private static string _charName(int cp)=>cp switch{' '=>"space",'\n'=>"newline",'\t'=>"tab",'\r'=>"return",'\0'=>"null",'\a'=>"alarm",'\b'=>"backspace",'\x1b'=>"escape",'\x7f'=>"delete",_=>char.ConvertFromUtf32(cp)};
    private static long S12RandomStep(SchemeRandomSource s){s.State=(1103515245L*s.State+12345)&0x7fffffff;return s.State;}
    private static object? S12RandomInt(SchemeRandomSource s,int n){if(n<=0)return 0L;var state=S12RandomStep(s);return (long)Math.Floor((state/2147483648.0)*n+0.5)%n;}
    private static object? S12RandomReal(SchemeRandomSource s)=>S12RandomStep(s)/2147483648.0;
    private static object? MappingRef(object?[] a){foreach(var p in a[0].Cells()){var c=(Cell)p!;if(S12Eq(c.Car,a[1]))return c.Cdr;}return a.Length>2?a[2]:Const.FALSE;}
    private static object? MappingSet(object?[] a){var r=new List<object?>{new Cell(a[1],a[2])};foreach(var p in a[0].Cells()){var c=(Cell)p!;if(!S12Eq(c.Car,a[1]))r.Add(c);}return r.ToCell();}
    private static object? MappingDelete(object?[] a)=>a[0].Cells().Where(p=>!S12Eq(((Cell)p!).Car,a[1])).ToCell();
    private static object? PS12StringSplit(object?[] args)
    {
        var s = S12String(args[0]).ToString(); var sep = args.Length > 1 ? (args[1] is SchemeChar c ? char.ConvertFromUtf32(c.Codepoint) : ToStr(args[1])) : " ";
        if (sep.Length == 0) throw new SchemeException("string-split: empty separator");
        return s.Split(sep, StringSplitOptions.None).Select(x => (object?)new SchemeString(x)).ToCell();
    }
}
