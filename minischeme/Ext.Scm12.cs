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

    private static void RegisterScm12Host()
    {
        _b("symbol=?", args => args.Length < 2 || args.All(x => x is Sym) && args.Skip(1).All(x => ((Sym)x!).Name == ((Sym)args[0]!).Name) ? Const.TRUE : Const.FALSE);
        _b("char-name", args => args[0] is SchemeChar c ? _charName(c.Codepoint) : Const.FALSE);
        _b("string-foldcase", args => new SchemeString(S12String(args[0]).ToString().ToLowerInvariant()));
         _b("string->vector", args => new SchemeVector(S12String(args[0]).ToString().EnumerateRunes().Select(x => (object?)new SchemeChar(x.Value))));
        _b("vector->string", args => new SchemeString(((SchemeVector)args[0]!).Data.Select(AsChar)));
         _b("string-contains", args => { var s = S12String(args[0]).ToString(); var sub = S12String(args[1]).ToString(); var start = args.Length > 2 ? NumericHelper.ToInt(args[2]) : 0; var i = s.IndexOf(sub, start, StringComparison.Ordinal); return i < 0 ? Const.FALSE : (long)s[..i].EnumerateRunes().Count(); });
         _b("string-split", PS12StringSplit);
        _b("string-map", args => S12StringMap(args));
        _b("string-for-each", args => S12StringForEach(args));
        _b("string-any", args => S12StringQuantifier(args, false));
        _b("string-every", args => S12StringQuantifier(args, true));
        _b("string-trim", args => S12Trim(args, 0)); _b("string-trim-right", args => S12Trim(args, 1)); _b("string-trim-both", args => S12Trim(args, 2));
        _b("string-prefix?", args => S12String(args[1]).ToString().StartsWith(S12String(args[0]).ToString(), StringComparison.Ordinal) ? Const.TRUE : Const.FALSE);
        _b("string-suffix?", args => S12String(args[1]).ToString().EndsWith(S12String(args[0]).ToString(), StringComparison.Ordinal) ? Const.TRUE : Const.FALSE);
        _b("list-copy", args => CopyList(args[0]));
        _b("last-pair", args => S12LastPair(args[0]));
        _b("list-index", args => { int i = 0; foreach (var x in args[1].Cells()) { if (Truthy(S12Call(args[0], x))) return (long)i; i++; } return Const.FALSE; });
        _b("list-any", args => PAny([args[0], args[1]])); _b("list-every", args => PEvery([args[0], args[1]]));
        _b("list-find", args => PFind([args[0], args[1]])); _b("list-find-index", args => { int i=0; foreach(var x in args[1].Cells()) { if(Truthy(S12Call(args[0],x))) return (long)i; i++; } return Const.FALSE; });
        _b("length+", args => { var x=args[0]; if (x is Nil) return 0L; if (x is not Cell) return Const.FALSE; var seen=new HashSet<Cell>(ReferenceEqualityComparer.Instance); long n=0; while(x is Cell c){if(!seen.Add(c))return Const.FALSE;n++;x=c.Cdr;} return x is Nil ? n : Const.FALSE; });
        _b("reverse!", args => { object? prior=Const.NIL, cur=args[0]; while(cur is Cell c){var next=c.Cdr;c.Cdr=prior;prior=c;cur=next;} return prior; });
        _b("append-reverse", args => AppendRev(args[0], args[1])); _b("unfold-right", args => Unfold(args, true));
        _b("unzip5", args => Unzip(args[0], 5));
        _b("list-queue", S12ListQueue); _b("make-list-queue", args => S12ListQueue(args.Length == 0 ? [] : [args[0]])); _b("list-queue?", args => args[0] is SchemeListQueue ? Const.TRUE : Const.FALSE);
        _b("list-queue-copy", args => { var q=new SchemeListQueue();q.Items.AddRange(((SchemeListQueue)args[0]!).Items);return q; });
        _b("list-queue-front", args => ((SchemeListQueue)args[0]!).Items.FirstOrDefault() ?? Const.NIL); _b("list-queue-first", args => ((SchemeListQueue)args[0]!).Items.FirstOrDefault() ?? Const.NIL); _b("list-queue-back", args => ((SchemeListQueue)args[0]!).Items.LastOrDefault() ?? Const.NIL);
        _b("list-queue-empty?", args => ((SchemeListQueue)args[0]!).Items.Count == 0 ? Const.TRUE : Const.FALSE); _b("list-queue-size", args => (long)((SchemeListQueue)args[0]!).Items.Count); _b("list-queue-list", args => ((SchemeListQueue)args[0]!).Items.ToCell()); _b("list-queue->list", args => ((SchemeListQueue)args[0]!).Items.ToCell());
        _b("list-queue-add-front!", args => { ((SchemeListQueue)args[0]!).Items.Insert(0,args[1]);return Const.VOID; }); _b("list-queue-add-back!", args => { ((SchemeListQueue)args[0]!).Items.Add(args[1]);return Const.VOID; }); _b("list-queue-add!", args => { ((SchemeListQueue)args[0]!).Items.Add(args[1]);return Const.VOID; }); _b("list-queue-remove-front!", args => S12QueueRemove(args[0],false)); _b("list-queue-remove!", args => S12QueueRemove(args[0],false));
        _b("%make-list-queue", args => S12ListQueue(args)); _b("%list-queue-front", args => ((SchemeListQueue)args[0]!).Items.ToCell()); _b("%set-list-queue-front!", args => { var q=(SchemeListQueue)args[0]!; q.Items.Clear(); q.Items.AddRange(args[1].Cells()); return Const.VOID; }); _b("%list-queue-back", args => ((SchemeListQueue)args[0]!).Items.Count == 0 ? Const.NIL : new Cell(((SchemeListQueue)args[0]!).Items[^1], Const.NIL)); _b("%set-list-queue-back!", args => Const.VOID);

        _b("make-hook", _ => new SchemeHook()); _b("make-hook-internal", _ => new SchemeHook()); _b("hook?", args => args[0] is SchemeHook ? Const.TRUE : Const.FALSE); _b("hook-procedures", args => ((SchemeHook)args[0]!).Procedures.ToCell()); _b("set-hook-procedures!", args => { ((SchemeHook)args[0]!).Procedures=args[1].Cells();return Const.VOID; }); _b("add-hook!", args => { var h=(SchemeHook)args[0]!; if(args.Length>2&&Truthy(args[2]))h.Procedures.Add(args[1]);else h.Procedures.Insert(0,args[1]);return Const.VOID; }); _b("remove-hook!", args => { ((SchemeHook)args[0]!).Procedures.RemoveAll(x=>ReferenceEquals(x,args[1]));return Const.VOID; }); _b("reset-hook!", args => { ((SchemeHook)args[0]!).Procedures.Clear();return Const.VOID; }); _b("run-hook", args => { foreach(var p in ((SchemeHook)args[0]!).Procedures)App(p,args.Skip(1).ToArray());return Const.VOID; });
        var defaultSource = new SchemeRandomSource(DateTimeOffset.UtcNow.ToUnixTimeSeconds()); Evaluator.GlobalEnv.Define("*default-random-source*", defaultSource); _b("make-random-source", _ => new SchemeRandomSource(DateTimeOffset.UtcNow.ToUnixTimeSeconds())); _b("%make-random-source", args => new SchemeRandomSource(NumericHelper.ToLong(args[0]))); _b("random-source?", args => args[0] is SchemeRandomSource ? Const.TRUE : Const.FALSE); _b("random-source-state", args => ((SchemeRandomSource)args[0]!).State); _b("set-random-source-state!", args=>{((SchemeRandomSource)args[0]!).State=NumericHelper.ToLong(args[1]);return Const.VOID;}); _b("random-source-random-integer", args=>S12RandomInt((SchemeRandomSource)args[0]!,NumericHelper.ToInt(args[1]))); _b("random-source->random-integer", args=>S12RandomInt((SchemeRandomSource)args[0]!,NumericHelper.ToInt(args[1]))); _b("random-source-random-real", args=>S12RandomReal((SchemeRandomSource)args[0]!)); _b("random-source->random-real", args=>S12RandomReal((SchemeRandomSource)args[0]!)); _b("random-seed", args=>{defaultSource.State=NumericHelper.ToLong(args[0]);return Const.VOID;}); _b("random-integer", args=>S12RandomInt(defaultSource,NumericHelper.ToInt(args[0]))); _b("random-real", _=>S12RandomReal(defaultSource));

        _b("make-array", args => { var dims=args[0] is Cell ? args[0].Cells().Select(NumericHelper.ToInt).ToList() : [NumericHelper.ToInt(args[0])]; return new SchemeArray((SchemeVector)S12ArrayBuild(dims,0,args.Length>1?args[1]:0L)!); }); _b("array?", args=>args[0] is SchemeArray or SchemeVector ? Const.TRUE:Const.FALSE); _b("array-ref", args=>S12ArrayRef(args[0],args[1..])); _b("array-set!", args=>S12ArraySet(args[0],args[1],args[2..])); _b("array-dimensions", args=>S12ArrayDims(args[0]));
        _b("mapping", args => Mapping(args)); _b("list->mapping", args => Mapping(args[0].Cells().ToArray())); _b("mapping->list", args=>args[0]); _b("mapping-ref", args=>MappingRef(args)); _b("mapping-contains?",args=>MappingRef([args[0],args[1],Const.FALSE]) is not Sym s || s != Const.FALSE ? Const.TRUE:Const.FALSE); _b("mapping-set",args=>MappingSet(args)); _b("mapping-delete",args=>MappingDelete(args)); _b("mapping-keys",args=>args[0].Cells().Select(x=>((Cell)x!).Car).ToCell()); _b("mapping-values",args=>args[0].Cells().Select(x=>((Cell)x!).Cdr).ToCell()); _b("mapping-size",args=>(long)args[0].Cells().Count); _b("mapping-for-each",args=>{foreach(var p in args[1].Cells()){var c=(Cell)p!;App(args[0],c.Car,c.Cdr);}return Const.VOID;}); _b("mapping-map",args=>args[1].Cells().Select(x=>{var c=(Cell)x!;return new Cell(c.Car,App(args[0],c.Car,c.Cdr));}).ToCell());
        _b("make-bimap", args=>{var b=new SchemeBimap();foreach(var p in args[0].Cells()){var c=(Cell)p!;b.Forward[c.Car!]=c.Cdr;b.Reverse[c.Cdr!]=c.Car;}return b;}); _b("bimap?",args=>args[0] is SchemeBimap?Const.TRUE:Const.FALSE); _b("bimap-forward",args=>((SchemeBimap)args[0]!).Forward[args[1]!]); _b("bimap-forward/default",args=>((SchemeBimap)args[0]!).Forward.TryGetValue(args[1]!,out var v)?v:args[2]); _b("bimap-reverse",args=>((SchemeBimap)args[0]!).Reverse[args[1]!]); _b("bimap-set!",args=>{var b=(SchemeBimap)args[0]!;b.Forward[args[1]!]=args[2];b.Reverse[args[2]!] = args[1];return Const.VOID;}); _b("bimap-contains?",args=>((SchemeBimap)args[0]!).Forward.ContainsKey(args[1]!)?Const.TRUE:Const.FALSE);
        _b("%make-bimap", _ => new SchemeBimap()); _b("%bimap-forward", args => ((SchemeBimap)args[0]!).Forward); _b("%bimap-forward-set!", args => { var b=(SchemeBimap)args[0]!; b.Forward.Clear(); foreach(var p in ((Dictionary<object,object?>)args[1]!)) b.Forward[p.Key]=p.Value; return Const.VOID; }); _b("%bimap-rev", args => ((SchemeBimap)args[0]!).Reverse); _b("%bimap-rev-set!", args => { var b=(SchemeBimap)args[0]!; b.Reverse.Clear(); foreach(var p in ((Dictionary<object,object?>)args[1]!)) b.Reverse[p.Key]=p.Value; return Const.VOID; });
        _b("make-deque",args=>{var d=new SchemeDeque();d.Items.AddRange(args);return d;}); _b("deque?",args=>args[0] is SchemeDeque?Const.TRUE:Const.FALSE); _b("deque-empty?",args=>((SchemeDeque)args[0]!).Items.Count==0?Const.TRUE:Const.FALSE); _b("deque-add-front",args=>{((SchemeDeque)args[0]!).Items.Insert(0,args[1]);return args[0];}); _b("deque-add-back",args=>{((SchemeDeque)args[0]!).Items.Add(args[1]);return args[0];}); _b("deque-front",args=>((SchemeDeque)args[0]!).Items.First()); _b("deque-back",args=>((SchemeDeque)args[0]!).Items.Last()); _b("deque-remove-front",args=>{var d=(SchemeDeque)args[0]!;var x=d.Items[0];d.Items.RemoveAt(0);return x;}); _b("deque-remove-back",args=>{var d=(SchemeDeque)args[0]!;var i=d.Items.Count-1;var x=d.Items[i];d.Items.RemoveAt(i);return x;}); _b("deque-length",args=>(long)((SchemeDeque)args[0]!).Items.Count); _b("deque->list",args=>((SchemeDeque)args[0]!).Items.ToCell());
        _b("%make-deque", args => { var d=new SchemeDeque(); if(args.Length>1)d.Items.AddRange(args[1].Cells()); if(args.Length>3)d.Items.AddRange(args[3].Cells().AsEnumerable().Reverse()); return d; }); _b("%deque-fl", args => (long)((SchemeDeque)args[0]!).Items.Count); _b("%set-deque-fl!", args=>{var d=(SchemeDeque)args[0]!;var n=NumericHelper.ToInt(args[1]);if(n<d.Items.Count)d.Items.RemoveRange(n,d.Items.Count-n);return Const.VOID;}); _b("%deque-f", args=>((SchemeDeque)args[0]!).Items.ToCell()); _b("%set-deque-f!", args=>{var d=(SchemeDeque)args[0]!;d.Items.Clear();d.Items.AddRange(args[1].Cells());return Const.VOID;}); _b("%deque-bl", _=>0L); _b("%set-deque-bl!", _=>Const.VOID); _b("%deque-b", _=>Const.NIL); _b("%set-deque-b!", _=>Const.VOID);
        _b("fixnum?",args=>args[0] is int or long or BigInteger?Const.TRUE:Const.FALSE); _b("flonum?",args=>args[0] is double or float?Const.TRUE:Const.FALSE); _b("procedure-rename",args=>args[0]); _b("scheme-implementation-name",_=>new SchemeString("Hermes Scheme")); _b("scheme-implementation-version",_=>new SchemeString("0.1 (R7RS-small + SRFIs)")); _b("version",_=>new SchemeString("0.1 (R7RS-small + SRFIs)")); Evaluator.GlobalEnv.Define("fx-width",64L); Evaluator.GlobalEnv.Define("fx-greatest",long.MaxValue); Evaluator.GlobalEnv.Define("fx-least",long.MinValue); _b("fixnum-width",_=>64L); _b("greatest-fixnum",_=>long.MaxValue); _b("least-fixnum",_=>long.MinValue); _b("fxcopy-bit",args=>{var n=NumericHelper.ToLong(args[0]);var mask=1L<<NumericHelper.ToInt(args[1]);return Truthy(args[2])?(n|mask):(n&~mask);});
        Evaluator.GlobalEnv.Define("char-set:empty", new bool[256]); Evaluator.GlobalEnv.Define("char-set:full", Enumerable.Repeat(true,256).ToArray()); Evaluator.GlobalEnv.Define("char-set:lower-case", UcsRangeCharSet([97L,123L])); Evaluator.GlobalEnv.Define("char-set:lower", UcsRangeCharSet([97L,123L])); Evaluator.GlobalEnv.Define("char-set:upper-case", UcsRangeCharSet([65L,91L])); Evaluator.GlobalEnv.Define("char-set:upper", UcsRangeCharSet([65L,91L])); Evaluator.GlobalEnv.Define("char-set:digit", UcsRangeCharSet([48L,58L])); Evaluator.GlobalEnv.Define("char-set:letter", CharSetBinOp([UcsRangeCharSet([97L,123L]),UcsRangeCharSet([65L,91L])],true)); Evaluator.GlobalEnv.Define("char-set:whitespace", MakeCharSet(" \t\r\n")); Evaluator.GlobalEnv.Define("char-set:blank",MakeCharSet(" \t")); Evaluator.GlobalEnv.Define("char-set:punctuation",MakeCharSet(".,;:!?-'\"()[]{}\\/@#$%^&*+=<>|~")); Evaluator.GlobalEnv.Define("char-set:graphic",CharSetBinOp([UcsRangeCharSet([97L,123L]),UcsRangeCharSet([65L,91L]),UcsRangeCharSet([48L,58L]),MakeCharSet(".,;:!?-'\"()[]{}\\/@#$%^&*+=<>|~")],true)); Evaluator.GlobalEnv.Define("char-set:printing",UcsRangeCharSet([32L,127L])); Evaluator.GlobalEnv.Define("char-set:symbol",MakeCharSet("$%&*+-./:<=>?@^_~")); Evaluator.GlobalEnv.Define("char-set:hex-digit",MakeCharSet("0123456789abcdefABCDEF")); Evaluator.GlobalEnv.Define("char-set:iso-control",UcsRangeCharSet([0L,32L]));

        _b("%make-binary-heap", args => { var h = new SchemeBinaryHeap { Comparator = args.Length > 2 ? args[2] : (Func<object?[], object?>)(a => NumericHelper.Compare(a[0], a[1]) < 0 ? Const.TRUE : Const.FALSE) }; if (args.Length > 0 && args[0] is SchemeVector v) h.Items.AddRange(v.Data.Take(args.Length > 1 ? NumericHelper.ToInt(args[1]) : v.Length)); S12Heapify(h); return h; });
        _b("make-binary-heap", args => { var h = new SchemeBinaryHeap { Comparator = args.Length > 0 ? args[0] : (Func<object?[], object?>)(a => NumericHelper.Compare(a[0], a[1]) < 0 ? Const.TRUE : Const.FALSE) }; if (args.Length > 1) h.Items.AddRange(args[1].Cells()); S12Heapify(h); return h; });
        _b("binary-heap?", args => args[0] is SchemeBinaryHeap ? Const.TRUE : Const.FALSE); _b("binary-heap-vec", args => new SchemeVector(((SchemeBinaryHeap)args[0]!).Items)); _b("binary-heap-n", args => (long)((SchemeBinaryHeap)args[0]!).Items.Count); _b("binary-heap-cmp", args => ((SchemeBinaryHeap)args[0]!).Comparator!);
        _b("set-binary-heap-vec!", args => { var h=(SchemeBinaryHeap)args[0]!;h.Items.Clear();h.Items.AddRange(((SchemeVector)args[1]!).Data);return Const.VOID; }); _b("set-binary-heap-n!", args => { var h=(SchemeBinaryHeap)args[0]!;h.Items.RemoveRange(Math.Min(NumericHelper.ToInt(args[1]),h.Items.Count),Math.Max(0,h.Items.Count-NumericHelper.ToInt(args[1])));return Const.VOID; });
        _b("binary-heap-insert!", args => { var h=(SchemeBinaryHeap)args[0]!;h.Items.Add(args[1]);S12HeapUp(h,h.Items.Count-1);return h; }); _b("binary-heap-min", args => ((SchemeBinaryHeap)args[0]!).Items.First()); _b("binary-heap-size", args => (long)((SchemeBinaryHeap)args[0]!).Items.Count); _b("binary-heap-empty?", args => ((SchemeBinaryHeap)args[0]!).Items.Count==0?Const.TRUE:Const.FALSE); _b("binary-heap-remove-min!", args => { var h=(SchemeBinaryHeap)args[0]!;var x=h.Items[0];var last=h.Items[^1];h.Items.RemoveAt(h.Items.Count-1);if(h.Items.Count>0){h.Items[0]=last;S12HeapDown(h,0);}return x; }); _b("binary-heap-delete-min!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("binary-heap-remove-min!"),args[0]));
        _b("deque-push-front!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-add-front"),args[0],args[1])); _b("deque-push-back!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-add-back"),args[0],args[1])); _b("deque-pop-front!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-remove-front"),args[0])); _b("deque-pop-back!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-remove-back"),args[0])); _b("deque-add-front!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-add-front"),args[0],args[1])); _b("deque-add-back!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-add-back"),args[0],args[1])); _b("deque-remove-front!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-remove-front"),args[0])); _b("deque-remove-back!", args => S12Call(Evaluator.GlobalEnv.LookupSilent("deque-remove-back"),args[0]));
        _b("make-coroutine-generator", args => MakeCoroutineGenerator(args[0])); _b("generator?", args => args[0] is Delegate or LambdaProc or CompiledLambda ? Const.TRUE : Const.FALSE);
        _b("exact", args => PInexactExact(args)); _b("inexact", args => NumericHelper.ToDouble(args[0])); _b("degrees->radians", args => NumericHelper.ToDouble(args[0])*Math.PI/180.0); _b("radians->degrees", args => NumericHelper.ToDouble(args[0])*180.0/Math.PI); _b("log2", args => Math.Log2(NumericHelper.ToDouble(args[0]))); _b("log10", args => Math.Log10(NumericHelper.ToDouble(args[0])));
        _b("arithmetic-shift-right", args => NumericHelper.ToLong(args[0]) >> NumericHelper.ToInt(args[1])); _b("bitwise-or", args => args.Aggregate(0L,(a,b)=>a|NumericHelper.ToLong(b))); _b("logior", args => args.Aggregate(0L,(a,b)=>a|NumericHelper.ToLong(b))); _b("logand", args => args.Aggregate(-1L,(a,b)=>a&NumericHelper.ToLong(b))); _b("logxor", args => args.Aggregate(0L,(a,b)=>a^NumericHelper.ToLong(b))); _b("lognot", args => ~NumericHelper.ToLong(args[0])); _b("integer->string/radix", args => new SchemeString(Convert.ToString(NumericHelper.ToLong(args[0]), NumericHelper.ToInt(args[1]))!)); _b("real->exact", args => PInexactExact(args)); _b("linear-update-list", args => args.ToList().ToCell()); _b("object->string", args => new SchemeString(Printer.Format(args[0]))); _b("with-exception-handler/k", args => S12Call(args[1])); _b("with-output-to-string", args => { var old=Console.Out; using var sw=new StringWriter(); Console.SetOut(sw); try { S12Call(args[0]); } finally { Console.SetOut(old); } return new SchemeString(sw.ToString()); }); _b("loop-n", args => { for(long i=NumericHelper.ToLong(args[0]);i>0;i--); return Sym.Intern("done"); }); _b("test-begin", args=>Const.VOID); _b("test-end", args=>Const.VOID); _b("char-set->integer", args=>{long n=0; if(args[0] is bool[] bits){for(int i=0;i<bits.Length&&i<256;i++)if(bits[i])n=n*33+i;} else if(args[0] is SchemeVector v){for(int i=0;i<v.Data.Count&&i<256;i++)if(Truthy(v.Data[i]))n=n*33+i;} return n;}); _b("%bits->integer", args=>BitsToInteger(args[0])); _b("void-sentinel", _=>Const.VOID);
        _b("tmap", args => (Func<object?[],object?>)(r => (Func<object?[],object?>)(a => a.Length==1 ? App(r,a[0]) : App(r,a[0],App(args[0],a[1]))))); _b("tfilter", args => (Func<object?[],object?>)(r => (Func<object?[],object?>)(a => a.Length==1 ? App(r,a[0]) : Truthy(App(args[0],a[1])) ? App(r,a[0],a[1]) : a[0]))); _b("ttake", args => { long left=NumericHelper.ToLong(args[0]); return (Func<object?[],object?>)(r => (Func<object?[],object?>)(a => a.Length==1 ? App(r,a[0]) : left-- > 0 ? App(r,a[0],a[1]) : a[0])); }); _b("tdrop", args => { long left=NumericHelper.ToLong(args[0]); return (Func<object?[],object?>)(r => (Func<object?[],object?>)(a => a.Length==1 ? App(r,a[0]) : left-- > 0 ? a[0] : App(r,a[0],a[1]))); }); _b("tconcatenate", _ => (Func<object?[],object?>)(r => r));
        _b("vector-cumulate", args => { var v=(SchemeVector)args[2]!;var outv=new SchemeVector(v.Length);object? acc=args[1];for(int i=0;i<v.Length;i++){acc=App(args[0],acc,v[i]);outv[i]=acc;}return outv; }); _b("vector-index-right", args => {var v=(SchemeVector)args[1]!;var start=args.Length>2?NumericHelper.ToInt(args[2]):v.Length-1;for(int i=start;i>=0;i--)if(Truthy(App(args[0],v[i])))return (long)i;return Const.FALSE;}); _b("vector-skip-right", args => {var v=(SchemeVector)args[1]!;var start=args.Length>2?NumericHelper.ToInt(args[2]):v.Length-1;for(int i=start;i>=0;i--)if(!Truthy(App(args[0],v[i])))return (long)i;return Const.FALSE;}); _b("vector-append-subvectors", args => {var all=new List<object?>();for(int i=0;i<args.Length;i+=3){var v=(SchemeVector)args[i]!;all.AddRange(v.Data.GetRange(NumericHelper.ToInt(args[i+1]),NumericHelper.ToInt(args[i+2])-NumericHelper.ToInt(args[i+1])));}return new SchemeVector(all);});
        Evaluator.GlobalEnv.Define("*char-names*", Const.NIL);
        foreach (var tag in new[] { "<hook>", "<random-source>", "<list-queue>", "<binary-heap>", "<bimap>", "<deque>" })
            Evaluator.GlobalEnv.Define(tag, Sym.Intern(tag));
        _b("random-source-randomize!", args => { ((SchemeRandomSource)args[0]!).State = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); return Const.VOID; });
        _b("random-source-pseudo-randomize!", args => { ((SchemeRandomSource)args[0]!).State = NumericHelper.ToLong(args[1]) * 12345 + NumericHelper.ToLong(args[2]); return Const.VOID; });
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
