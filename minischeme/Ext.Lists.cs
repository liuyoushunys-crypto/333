using System.Numerics;
using Miniscm.Types;
using Miniscm.Eval;
using Void = Miniscm.Types.Void;

namespace Miniscm.Primitives;

public static partial class PrimitiveRegistry
{
    private static object? RegisterExtLists()
    {
        // basics
        _b("cons*", args => ConsStar(args));
        _b("list*", args => ConsStar(args));
        _b("list-copy", args => CopyList(args[0]));
        _b("iota", args =>
        {
            long n = NumericHelper.ToInt(args[0]);
            long s = args.Length > 1 ? NumericHelper.ToInt(args[1]) : 0;
            long st = args.Length > 2 ? NumericHelper.ToInt(args[2]) : 1;
            var res = new List<object?>();
            for (long i = 0; i < n; i++) res.Add(s + i * st);
            return res.ToCell();
        });
        _b("first", args => Nth(args[0], 0));
        _b("second", args => Nth(args[0], 1));
        _b("third", args => Nth(args[0], 2));
        _b("fourth", args => Nth(args[0], 3));
        _b("fifth", args => Nth(args[0], 4));
        _b("sixth", args => Nth(args[0], 5));
        _b("seventh", args => Nth(args[0], 6));
        _b("eighth", args => Nth(args[0], 7));
        _b("ninth", args => Nth(args[0], 8));
        _b("tenth", args => Nth(args[0], 9));
        _b("list-head", args => TakeList(args[0], NumericHelper.ToInt(args[1])));

        _b("take", args => TakeList(args[0], NumericHelper.ToInt(args[1])));
        _b("drop", args => DropList(args[0], NumericHelper.ToInt(args[1])));
        _b("take-right", args => TakeRight(args[0], NumericHelper.ToInt(args[1])));
        _b("drop-right", args => DropRight(args[0], NumericHelper.ToInt(args[1])));
        _b("take-while", args => TakeWhileList(args[0], args[1]));
        _b("drop-while", args => DropWhileList(args[0], args[1]));
        _b("last", args => LastList(args[0]));
        _b("last-pair", args => LastPair(args[0]));
        _b("but-last", args => ButLast(args[0]));
        _b("length+", args => LengthPlus(args[0]));
        _b("list-tabulate", args => ListTabulate(args));
        _b("list-index", args => ListIndex(args[0], args[1]));
        _b("list-set!", args => ListSetBang(args));
        _b("list-find", args => ListFind(args[0], args[1]));
        _b("list-find-index", args => ListFindIndex(args[0], args[1]));
        _b("list-any", args => ListAny(args));
        _b("list-every", args => ListEvery(args));
        _b("list-filter-map", args => ListFilterMap(args[0], args[1]));
        _b("list-partition", args => ListPartition(args[0], args[1]));
        _b("list-remove", args => ListRemove(args[0], args[1]));
        _b("list-flatten", args => FlattenList(args[0]));
        _b("list-zip", args => Zip(args));
        _b("zip", args => Zip(args));
        _b("list-sort", args => SortList(args));
        _b("list-stable-sort", args => SortList(args));
        _b("list=", args => ListEqual(args));
        _b("sorted?", args => SortedP(args));
        _b("merge", args => Merge(args));
        _b("merge!", args => Merge(args));
        _b("find", args => ListFind(args[0], args[1]));
        _b("fold", args => FoldLeft(args[0], args[1], args[2]));
        _b("fold-left", args => FoldLeft(args[0], args[1], args[2]));
        _b("fold-right", args => FoldRight(args[0], args[1], args[2]));
        _b("reduce", args => FoldLeft(args[0], args[1], args[2]));
        _b("reduce-right", args => FoldRight(args[0], args[1], args[2]));
        _b("any", args => ListAny(args));
        _b("every", args => ListEvery(args));
        _b("count", args => CountFn(args[0], args[1]));
        _b("delete", args => DeleteFn(args));
        _b("delete-duplicates", args => DeleteDups(args));
        _b("delete-assoc", args => DeleteAssoc(args[0], args[1]));
        _b("alist-cons", args => new Cell(new Cell(args[0], args[1]), args[2]));
        _b("alist-delete", args => AlistDelete(args));
        _b("append-map", args => AppendMap(args));
        _b("append-reverse", args => AppendRev(args[0], args[1]));
        _b("concatenate", args => Concatenate(args[0]));
        _b("flatten", args => FlattenList(args[0]));
        _b("filter-map", args => ListFilterMap(args[0], args[1]));
        _b("map-in-order", args => MapInOrder(args));        _b("pair-for-each", args => PairForEach(args[0], args[1]));
        _b("xcons", args => new Cell(args[1], args[0]));
        _b("unzip1", args => Unzip(args[0], 1));
        _b("unzip2", args => Unzip(args[0], 2));
        _b("unzip3", args => Unzip(args[0], 3));
        _b("unzip4", args => Unzip(args[0], 4));
        _b("unzip5", args => Unzip(args[0], 5));
        _b("curry", args => Curry(args));
        _b("complement", args => (Func<object?[], object?>)(a => ReferenceEquals(App(args[0], a), Const.TRUE) ? Const.FALSE : Const.TRUE));
        _b("flip", args => (Func<object?[], object?>)(a => App(args[0], a[1], a[0])));
        _b("const", args => (Func<object?[], object?>)(_ => args[0]));
        _b("iterate", args => Iterate(args[0], NumericHelper.ToInt(args[1]), args[2]));
        _b("product", args => args.Aggregate((object?)1L, (a, b) => NumericHelper.Mul(a!, b))!);
        _b("square", args => NumericHelper.Mul(args[0], args[0]));
        _b("range", args => Range(args));
        _b("interleave", args => Interleave(args));
        _b("symbolic-append", args => Sym.Intern(string.Concat(args.Select(x => x is Sym sy ? sy.Name : ToStr(x)))));
        _b("<>", args => !NumericHelper.IsZero(NumericHelper.Sub(args[0], args[1])) ? Const.TRUE : Const.FALSE);

        // list predicates
        _b("circular-list", args => MakeCircularList(args));
        _b("circular-list?", args => IsCircularList(args[0]) ? Const.TRUE : Const.FALSE);
        _b("dotted-list?", args => IsDottedList(args[0]) ? Const.TRUE : Const.FALSE);
        _b("proper-list?", args => IsProperList(args[0]) ? Const.TRUE : Const.FALSE);
        _b("null-list?", args => args[0] is Nil ? Const.TRUE : Const.FALSE);
        _b("not-pair?", args => args[0] is not Cell ? Const.TRUE : Const.FALSE);
        _b("ne-list?", args => args[0] is Cell c && c.Cdr is Nil ? Const.TRUE : Const.FALSE);

        // mutation
        _b("drop!", args => DropList(args[0], NumericHelper.ToInt(args[1])));
        _b("take!", args => TakeList(args[0], NumericHelper.ToInt(args[1])));
        _b("filter!", args => ListRemove(args[0], args[1]));
        _b("flat-map", args => AppendMap(args));

        // lset
        _b("lset-union", args => LsetUnion(args));
        _b("lset-intersection", args => LsetIntersection(args));
        _b("lset-difference", args => LsetDifference(args));
        _b("lset-xor", args => LsetXor(args));
        _b("lset-=?", args => LsetEqual(args));

        // assoc/mem with eq
        _b("assq", args => Assoc(args[0], args[1], true));
        _b("assv", args => Assoc(args[0], args[1], false));
        _b("assoc", args => Assoc(args[0], args[1], false));
        _b("memq", args => Mem(args[0], args[1], true));
        _b("memv", args => Mem(args[0], args[1], false));
        _b("member", args => Mem(args[0], args[1], false));

        // list-queue (SRFI-117)
        _b("make-list-queue", args => MakeListQueue(args));
        _b("list-queue", args => MakeListQueue(args));
        _b("list-queue?", args => args[0] is Cell lq && lq.Car is Sym ls && ls.Name == "list-queue" ? Const.TRUE : Const.FALSE);
        _b("list-queue-front", args => ListQueueFront(args[0]));
        _b("list-queue-back", args => ListQueueBack(args[0]));
        _b("list-queue-empty?", args => ListQueueEmpty(args[0]) ? Const.TRUE : Const.FALSE);
        _b("list-queue-add!", args => ListQueueAdd(args));
        _b("list-queue-add-back!", args => ListQueueAdd(args));
        _b("list-queue-add-front!", args => ListQueueAddFront(args));
        _b("list-queue-remove!", args => ListQueueRemove(args));
        _b("list-queue-remove-front!", args => ListQueueRemove(args));
        _b("list-queue-list", args => ListQueueToList(args[0]));
        _b("list-queue-first", args => ListQueueFirst(args[0]));
        throw new SchemeException("list-set!: index out of bounds");
    }

    private static object? ConsStar(object?[] args)    {
        if (args.Length == 0) return Const.NIL;
        object? r = args[^1];
        for (int i = args.Length - 2; i >= 0; i--) r = new Cell(args[i], r);
        return r;
    }

    private static object? CopyList(object? lst)
    {
        if (lst is not Cell) return lst;
        var head = new Cell(((Cell)lst).Car, Const.NIL);
        var tail = head;
        var cur = ((Cell)lst).Cdr;
        while (cur is Cell c)
        {
            tail.Cdr = new Cell(c.Car, Const.NIL);
            tail = (Cell)tail.Cdr;
            cur = c.Cdr;
        }
        tail.Cdr = cur; // preserve dotted tail
        return head;
    }

    private static object? Nth(object? lst, int n)
    {
        var cur = lst;
        for (int i = 0; i < n; i++)
        {
            if (cur is not Cell c || c.Cdr is not Cell) return Const.FALSE;
            cur = c.Cdr;
        }
        return cur is Cell cc ? cc.Car : Const.FALSE;
    }

    private static object? TakeList(object? lst, int n)
    {
        var res = new List<object?>();
        var cur = lst;
        int i = 0;
        while (cur is Cell c && i < n) { res.Add(c.Car); cur = c.Cdr; i++; }
        return res.ToCell();
    }

    private static object? DropList(object? lst, int n)
    {
        var cur = lst;
        int i = 0;
        while (cur is Cell c && i < n) { cur = c.Cdr; i++; }
        return cur;
    }

    private static object? TakeRight(object? lst, int n)
    {
        if (n <= 0 || lst is not Cell) return Const.NIL;
        var items = lst.Cells();
        if (n >= items.Count) return items.ToCell();
        return items.Skip(items.Count - n).ToCell();
    }

    private static object? DropRight(object? lst, int n)
    {
        if (n <= 0 || lst is not Cell) return lst;
        var items = lst.Cells();
        if (n >= items.Count) return Const.NIL;
        return items.Take(items.Count - n).ToCell();
    }

    private static object? TakeWhileList(object? pred, object? lst)
    {
        var res = new List<object?>();
        var cur = lst;
        while (cur is Cell c && ReferenceEquals(App(pred, c.Car), Const.TRUE)) { res.Add(c.Car); cur = c.Cdr; }
        return res.ToCell();
    }

    private static object? DropWhileList(object? pred, object? lst)
    {
        var cur = lst;
        while (cur is Cell c && ReferenceEquals(App(pred, c.Car), Const.TRUE)) cur = c.Cdr;
        return cur;
    }

    private static object? LastList(object? lst)
    {
        var cur = lst;
        while (cur is Cell c && c.Cdr is Cell) cur = c.Cdr;
        return cur is Cell c2 ? c2.Car : Const.FALSE;
    }

    private static object? ButLast(object? lst)
    {
        var items = lst.Cells();
        if (items.Count <= 1) return Const.NIL;
        return items.Take(items.Count - 1).ToCell();
    }

    private static object? LengthPlus(object? lst)
    {
        if (lst is not Cell) return lst is Nil ? (object?)0L : Const.FALSE;
        var cur = lst;
        int n = 0;
        while (cur is Cell c) { n++; cur = c.Cdr; }
        return cur is Nil ? (object?)(long)n : Const.FALSE;
    }

    private static object? ListTabulate(object?[] args)
    {
        int n = NumericHelper.ToInt(args[0]);
        var fn = args[1];
        var res = new List<object?>();
        for (int i = 0; i < n; i++) res.Add(App(fn, (long)i));
        return res.ToCell();
    }

    private static object? ListIndex(object? pred, object? lst)
    {
        int i = 0;
        foreach (var x in lst.Cells())
        {
            if (ReferenceEquals(App(pred, x), Const.TRUE)) return (long)i;
            i++;
        }
        return Const.FALSE;
    }

    private static object? ListSetBang(object?[] args)
    {
        var cur = args[0];
        int i = 0;
        while (cur is Cell c)
        {
            if (i == NumericHelper.ToInt(args[1])) { c.Car = args[2]; return Const.VOID; }
            cur = c.Cdr; i++;
        }
        return Const.VOID;
    }

    private static object? ListFind(object? pred, object? lst)
    {
        foreach (var x in lst.Cells())
            if (ReferenceEquals(App(pred, x), Const.TRUE)) return x;
        return Const.FALSE;
    }

    private static object? ListFindIndex(object? pred, object? lst)
    {
        int i = 0;
        foreach (var x in lst.Cells())
        {
            if (ReferenceEquals(App(pred, x), Const.TRUE)) return (long)i;
            i++;
        }
        return Const.FALSE;
    }

    private static object? ListAny(object?[] args)
    {
        if (args.Length < 2) return Const.FALSE;
        var pred = args[0];
        var curs = args[1..];
        while (true)
        {
            var curArgs = new List<object?>();
            foreach (var c in curs)
            {
                if (c is not Cell cc) return Const.FALSE;
                curArgs.Add(cc.Car);
            }
            if (ReferenceEquals(App(pred, curArgs.ToArray()), Const.TRUE)) return Const.TRUE;
            for (int i = 0; i < curs.Length; i++) curs[i] = ((Cell)curs[i]!).Cdr;
        }
    }

    private static object? ListEvery(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        var pred = args[0];
        var curs = args[1..];
        while (true)
        {
            var curArgs = new List<object?>();
            foreach (var c in curs)
            {
                if (c is not Cell cc) return Const.TRUE;
                curArgs.Add(cc.Car);
            }
            if (!ReferenceEquals(App(pred, curArgs.ToArray()), Const.TRUE)) return Const.FALSE;
            for (int i = 0; i < curs.Length; i++) curs[i] = ((Cell)curs[i]!).Cdr;
        }
    }

    private static object? ListFilterMap(object? fn, object? lst)
    {
        var res = new List<object?>();
        foreach (var x in lst.Cells())
        {
            var r = App(fn, x);
            if (!ReferenceEquals(r, Const.FALSE)) res.Add(r);
        }
        return res.ToCell();
    }

    private static object? ListPartition(object? pred, object? lst)
    {
        var yes = new List<object?>();
        var no = new List<object?>();
        foreach (var x in lst.Cells())
        {
            if (ReferenceEquals(App(pred, x), Const.TRUE)) yes.Add(x);
            else no.Add(x);
        }
        return new Cell(yes.ToCell(), new Cell(no.ToCell(), Const.NIL));
    }

    private static object? ListRemove(object? pred, object? lst)
    {
        var res = new List<object?>();
        foreach (var x in lst.Cells())
            if (!ReferenceEquals(App(pred, x), Const.TRUE)) res.Add(x);
        return res.ToCell();
    }

    private static object? FlattenList(object? lst)
    {
        var res = new List<object?>();
        FlattenRec(lst, res);
        return res.ToCell();
    }

    private static void FlattenRec(object? x, List<object?> res)
    {
        if (x is Cell cc) { FlattenRec(cc.Car, res); FlattenRec(cc.Cdr, res); }
        else if (x is not Nil) res.Add(x);
    }

    private static object? Zip(object?[] args)
    {
        var result = new List<object?>();
        var curs = args.ToList();
        while (curs.All(c => c is Cell))
        {
            var row = new List<object?>();
            for (int i = 0; i < curs.Count; i++) { row.Add(((Cell)curs[i]!).Car); curs[i] = ((Cell)curs[i]!).Cdr; }
            result.Add(row.ToCell());
        }
        return result.ToCell();
    }

    private static object? SortList(object?[] args)
    {
        var less = args[0];
        var items = args[1].Cells().ToList();
        StableSortC(items, less);
        return items.ToCell();
    }

    private static void StableSortC(List<object?> items, object? less)
    {
        for (int i = 1; i < items.Count; i++)
        {
            var key = items[i];
            int j = i - 1;
            while (j >= 0 && IsLessC(less, key, items[j])) { items[j + 1] = items[j]; j--; }
            items[j + 1] = key;
        }
    }

    private static bool IsLessC(object? less, object? a, object? b)
    {
        var r = App(less, a, b);
        return !ReferenceEquals(r, Const.FALSE) && r is not Nil;
    }

    private static object? ListEqual(object?[] args)
    {
        if (args.Length < 2) return Const.TRUE;
        var eq = args[0];
        var first = args[1].Cells().ToList();
        for (int k = 2; k < args.Length; k++)
        {
            var other = args[k].Cells().ToList();
            if (other.Count != first.Count) return Const.FALSE;
            for (int i = 0; i < first.Count; i++)
                if (!ReferenceEquals(App(eq, first[i], other[i]), Const.TRUE)) return Const.FALSE;
        }
        return Const.TRUE;
    }

    private static object? SortedP(object?[] args)
    {
        var less = args[0];
        var items = args[1].Cells().ToList();
        for (int i = 1; i < items.Count; i++)
            if (!IsLessC(less, items[i - 1], items[i])) return Const.FALSE;
        return Const.TRUE;
    }

    private static object? Merge(object?[] args)
    {
        var pred = args[0];
        var a = args[1].Cells().ToList();
        var b = args[2].Cells().ToList();
        var res = new List<object?>();
        int i = 0, j = 0;
        while (i < a.Count && j < b.Count)
        {
            if (ReferenceEquals(App(pred, a[i], b[j]), Const.TRUE)) res.Add(a[i++]);
            else res.Add(b[j++]);
        }
        while (i < a.Count) res.Add(a[i++]);
        while (j < b.Count) res.Add(b[j++]);
        return res.ToCell();
    }

    private static object? FoldLeft(object? f, object? init, object? lst)
    {
        object? acc = init;
        foreach (var x in lst.Cells()) acc = App(f, acc, x);
        return acc;
    }

    private static object? FoldRight(object? f, object? init, object? lst)
    {
        var stack = new List<object?>();
        foreach (var x in lst.Cells()) stack.Add(x);
        object? acc = init;
        for (int i = stack.Count - 1; i >= 0; i--) acc = App(f, stack[i], acc);
        return acc;
    }

    private static object? CountFn(object? pred, object? lst)
    {
        int n = 0;
        foreach (var x in lst.Cells())
            if (ReferenceEquals(App(pred, x), Const.TRUE)) n++;
        return (long)n;
    }

    private static object? DeleteFn(object?[] args)
    {
        var x = args[0];
        var eq = args.Length > 2 ? args[2] : null;
        var res = new List<object?>();
        foreach (var y in args[1].Cells())
        {
            if (eq is not null) { if (!ReferenceEquals(App(eq, x, y), Const.TRUE)) res.Add(y); }
            else if (!ReferenceEquals(y, x) && !(y is not null && y.Equals(x) && y.GetType() == x?.GetType())) res.Add(y);
        }
        return res.ToCell();
    }

    private static object? DeleteDups(object?[] args)
    {
        var eq = args.Length > 1 ? args[1] : null;
        var items = args[0].Cells().ToList();
        var result = new List<object?>();
        foreach (var x in items)
        {
            bool found = false;
            foreach (var y in result)
            {
                if (eq is null ? Equals(x, y) : ReferenceEquals(App(eq, x, y), Const.TRUE)) { found = true; break; }
            }
            if (!found) result.Add(x);
        }
        return result.ToCell();
    }

    private static object? DeleteAssoc(object? key, object? alist)
    {
        var res = new List<object?>();
        foreach (var p in alist.Cells())
        {
            if (p is Cell pc && !ReferenceEquals(pc.Car, key) && !Equals(pc.Car, key)) res.Add(p);
        }
        return res.ToCell();
    }

    private static object? AlistDelete(object?[] args)
    {
        var k = args[0];
        var eq = args.Length > 2 ? args[2] : null;
        var res = new List<object?>();
        foreach (var p in args[1].Cells())
        {
            var pc = p as Cell;
            bool match = eq is not null ? ReferenceEquals(App(eq, k, pc?.Car), Const.TRUE) : ReferenceEquals(pc?.Car, k) || Equals(pc?.Car, k);
            if (!match) res.Add(p);
        }
        return res.ToCell();
    }

    private static object? AppendMap(object?[] args)
    {
        var fn = args[0];
        var result = new List<object?>();
        var curs = args[1..].ToList();
        while (curs.All(c => c is Cell))
        {
            var cargs = new List<object?>();
            for (int i = 0; i < curs.Count; i++) { cargs.Add(((Cell)curs[i]!).Car); curs[i] = ((Cell)curs[i]!).Cdr; }
            var r = App(fn, cargs.ToArray());
            result.AddRange(r.Cells());
        }
        return result.ToCell();
    }

    private static object? AppendRev(object? a, object? b)
    {
        object? cur = b;
        foreach (var x in a.Cells()) cur = new Cell(x, cur);
        return cur;
    }

    private static object? Concatenate(object? lsts)
    {
        var res = new List<object?>();
        foreach (var sub in lsts.Cells()) res.AddRange(sub.Cells());
        return res.ToCell();
    }

    private static object? MapInOrder(object?[] args)
    {
        var fn = args[0];
        var result = new List<object?>();
        var curs = args[1..].ToList();
        while (curs.All(c => c is Cell))
        {
            var cargs = new List<object?>();
            for (int i = 0; i < curs.Count; i++) { cargs.Add(((Cell)curs[i]!).Car); curs[i] = ((Cell)curs[i]!).Cdr; }
            result.Add(App(fn, cargs.ToArray()));
        }
        return result.ToCell();
    }

    private static object? PairForEach(object? f, object? lst)
    {
        var cur = lst;
        while (cur is Cell c) { App(f, cur); cur = c.Cdr; }
        return Const.VOID;
    }

    private static object? Unzip(object? lst, int n)
    {
        var cols = new List<object?>[n];
        for (int i = 0; i < n; i++) cols[i] = new List<object?>();
        foreach (var x in lst.Cells())
        {
            if (x is Cell c)
            {
                var row = c.Cells();
                for (int i = 0; i < n && i < row.Count; i++) cols[i].Add(row[i]);
            }
        }
        if (n == 1) return cols[0].ToCell();
        return cols.Select(c => c.ToCell()).ToCell();
    }

    private static object? Curry(object?[] args)
    {
        var f = args[0];
        var pre = args[1..];
        return (Func<object?[], object?>)(more => App(f, pre.Concat(more).ToArray()));
    }

    private static object? Iterate(object? f, int n, object? x)
    {
        for (int i = 0; i < n; i++) x = App(f, x);
        return x;
    }

    private static object? Range(object?[] args)
    {
        long s = NumericHelper.ToInt(args[0]);
        long e = NumericHelper.ToInt(args[1]);
        long st = args.Length > 2 ? NumericHelper.ToInt(args[2]) : 1;
        var res = new List<object?>();
        for (long i = s; i < e; i += st) res.Add(i);
        return res.ToCell();
    }

    private static object? Interleave(object?[] args)
    {
        var res = new List<object?>();
        var curs = args.ToList();
        while (curs.Any(c => c is Cell))
        {
            for (int i = 0; i < curs.Count; i++)
            {
                if (curs[i] is Cell c) { res.Add(c.Car); curs[i] = c.Cdr; }
            }
        }
        return res.ToCell();
    }

    private static object? MakeCircularList(object?[] args)
    {
        if (args.Length == 0) return Const.NIL;
        var items = args.ToList();
        var head = new Cell(items[0], Const.NIL);
        var tail = head;
        for (int i = 1; i < items.Count; i++)
        {
            tail.Cdr = new Cell(items[i], Const.NIL);
            tail = (Cell)tail.Cdr;
        }
        tail.Cdr = head;
        return head;
    }

    private static bool IsCircularList(object? lst)
    {
        if (lst is not Cell) return false;
        var slow = lst;
        var fast = lst;
        while (fast is Cell fc && fc.Cdr is Cell fc2)
        {
            slow = ((Cell)slow!).Cdr;
            fast = fc2.Cdr;
            if (ReferenceEquals(slow, fast)) return true;
        }
        return false;
    }

    private static bool IsDottedList(object? lst)
    {
        if (lst is Nil) return false;
        if (lst is not Cell) return true;
        var seen = new HashSet<Cell>(ReferenceEqualityComparer.Instance);
        var cur = lst;
        while (cur is Cell c)
        {
            if (!seen.Add(c)) return false; // circular
            cur = c.Cdr;
        }
        return cur is not Nil;
    }

    private static bool IsProperList(object? lst)
    {
        if (lst is Nil) return true;
        if (lst is not Cell) return false;
        var seen = new HashSet<Cell>(ReferenceEqualityComparer.Instance);
        var cur = lst;
        while (cur is Cell c)
        {
            if (!seen.Add(c)) return false; // circular
            cur = c.Cdr;
        }
        return cur is Nil;
    }

    private static object? LsetUnion(object?[] args)
    {
        var eq = args[0];
        var res = new List<object?>();
        foreach (var lst in args[1..])
        {
            foreach (var x in lst.Cells())
            {
                bool found = res.Any(y => ReferenceEquals(App(eq, x, y), Const.TRUE));
                if (!found) res.Add(x);
            }
        }
        return res.ToCell();
    }

    private static object? LsetIntersection(object?[] args)
    {
        var eq = args[0];
        var first = args[1].Cells().ToList();
        var rest = args[2..].Select(l => l.Cells().ToList()).ToList();
        var res = new List<object?>();
        foreach (var x in first)
        {
            if (rest.All(l => l.Any(y => ReferenceEquals(App(eq, x, y), Const.TRUE)))) res.Add(x);
        }
        return res.ToCell();
    }

    private static object? LsetDifference(object?[] args)
    {
        var eq = args[0];
        var first = args[1].Cells().ToList();
        var rest = args[2..].Select(l => l.Cells().ToList()).ToList();
        var res = new List<object?>();
        foreach (var x in first)
        {
            if (!rest.Any(l => l.Any(y => ReferenceEquals(App(eq, x, y), Const.TRUE)))) res.Add(x);
        }
        return res.ToCell();
    }

    private static object? LsetXor(object?[] args)
    {
        var eq = args[0];
        var res = new List<object?>();
        foreach (var lst in args[1..])
        {
            foreach (var x in lst.Cells())
            {
                if (res.Any(y => ReferenceEquals(App(eq, x, y), Const.TRUE)))
                    res.RemoveAll(y => ReferenceEquals(App(eq, x, y), Const.TRUE));
                else res.Add(x);
            }
        }
        return res.ToCell();
    }

    private static object? LsetEqual(object?[] args)
    {
        var eq = args[0];
        var first = args[1].Cells().ToList();
        foreach (var lst in args[2..])
        {
            var other = lst.Cells().ToList();
            if (first.Count != other.Count) return Const.FALSE;
            foreach (var x in first)
                if (!other.Any(y => ReferenceEquals(App(eq, x, y), Const.TRUE))) return Const.FALSE;
        }
        return Const.TRUE;
    }

    private static object? Mem(object? obj, object? lst, bool identity)
    {
        var cur = lst;
        while (cur is Cell c)
        {
            if (identity
                ? (ReferenceEquals(c.Car, obj) || Equals(c.Car, obj))
                : ReferenceEquals(Miniscm.Compiler.JitRuntime.Equal2(c.Car, obj), Const.TRUE))
                return cur;
            cur = c.Cdr;
        }
        return Const.FALSE;
    }

    private static object? MakeListQueue(object?[] args)    {
        var front = args.Length > 0 ? args[0] : Const.NIL;
        return new Cell(Sym.Intern("list-queue"), new Cell(front, Const.NIL));
    }

    private static object? ListQueueFront(object? q)
    {
        if (q is not Cell lq || lq.Cdr is not Cell front) return Const.FALSE;
        return front.Car is Cell c ? c.Car : Const.NIL;
    }

    private static object? ListQueueBack(object? q)
    {
        if (q is not Cell lq || lq.Cdr is not Cell front) return Const.NIL;
        return LastPair(front.Car) is Cell c ? c.Car : Const.NIL;
    }

    private static bool ListQueueEmpty(object? q)
    {
        return q is not Cell lq || lq.Cdr is not Cell front || front.Car is Nil;
    }

    private static object? ListQueueAdd(object?[] args)
    {
        var q = args[0];
        if (q is Cell lq && lq.Cdr is Cell front)
        {
            if (front.Car is Nil) front.Car = new Cell(args[1], Const.NIL);
            else { var last = LastPair(front.Car); ((Cell)last!).Cdr = new Cell(args[1], Const.NIL); }
        }
        return Const.VOID;
    }

    private static object? ListQueueAddFront(object?[] args)
    {
        var q = args[0];
        if (q is Cell lq && lq.Cdr is Cell front) front.Car = new Cell(args[1], front.Car);
        return Const.VOID;
    }

    private static object? ListQueueRemove(object?[] args)
    {
        var q = args[0];
        if (q is Cell lq && lq.Cdr is Cell front && front.Car is Cell c)
        {
            var v = c.Car;
            front.Car = c.Cdr;
            return v;
        }
        return Const.NIL;
    }

    private static object? ListQueueToList(object? q)
    {
        return q is Cell lq && lq.Cdr is Cell front ? front.Car : Const.NIL;
    }

    private static object? ListQueueFirst(object? q)
    {
        return ListQueueFront(q);
    }
}
