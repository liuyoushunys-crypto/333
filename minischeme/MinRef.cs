// ============================================================================
// MinRef.cs — min.cs.txt 的正式源码版 (boot-min2.scm 的原生 C# 语义等价改写)
// 来源   : min.cs.txt (参考实现) — 正文逐函数一致
// 说明   : REPL ,expand 命令使用本类的 Expand 显示宏展开结果
// ============================================================================
using System.Runtime.CompilerServices;
using Miniscm.Eval;
using Miniscm.Base;
using Miniscm.Types;
using Void = Miniscm.Base.Void;

namespace Miniscm.Compiler;

// 参考实现 (与 NativeSyntax.cs 的功能重叠, 但逐函数对应 boot-min2.scm/min.py.txt,
// 含 quasiquote / syntax-case / with-syntax / let-syntax / define-macro 构造部分)
public static class MinRef
{
    // ── 符号常量 ──────────────────────────────────────────────────────────
    static readonly Sym SYM_UNDERSCORE = Sym.Intern("_");
    static readonly Sym SYM_ELLIPSIS = Sym.Intern("...");
    static readonly Sym SYM_QUOTE = Sym.Intern("quote");
    static readonly Sym SYM_QUASIQUOTE = Sym.Intern("quasiquote");
    static readonly Sym SYM_UNQUOTE = Sym.Intern("unquote");
    static readonly Sym SYM_UNQUOTE_SPLICING = Sym.Intern("unquote-splicing");
    static readonly Sym SYM_UNSYNTAX = Sym.Intern("unsyntax");
    static readonly Sym SYM_UNSYNTAX_SPLICING = Sym.Intern("unsyntax-splicing");
    static readonly Sym SYM_QUASISYNTAX = Sym.Intern("quasisyntax");
    static readonly Sym SYM_SYNTAX_RULES = Sym.Intern("syntax-rules");
    static readonly Sym SYM_LAMBDA = Sym.Intern("lambda");
    static readonly Sym SYM_SETBANG = Sym.Intern("set!");
    static readonly Sym SYM_SX_HYGIENE = Sym.Intern("sx-hygiene");
    static readonly Sym SYM_DEFINE_MACRO = Sym.Intern("define-macro");
    static readonly Sym SYM_SX_DISPATCH = Sym.Intern("sx-dispatch");
    static readonly Sym SYM_CONS = Sym.Intern("cons");
    static readonly Sym SYM_ARGS = Sym.Intern("args");

    // ── 基础工具 ──────────────────────────────────────────────────────────
    // SchemeEqual / IsProcedure / Length / ListRef / Memq 见 NativeSyntax.cs (internal)

    static (Sym, object?) Assq(Sym var, List<(Sym, object?)> bindings)  // (assq var bindings)
    {
        int idx = bindings.FindIndex(b => b.Item1 == var);
        if (idx < 0) throw new Exception($"assq: unbound {var.Name}");
        return bindings[idx];
    }

    static object? ToCell(IEnumerable<object?> items)           // list → Cell 链
        => items.ToCell();

    static List<object?> IterCells(object? lst)                 // Cell 链 → list
    {
        var out_ = new List<object?>();
        var cur = lst;
        while (cur is Cell c) { out_.Add(c.Car); cur = c.Cdr; }
        return out_;
    }

    // ── 桥接原语 ──────────────────────────────────────────────────────────

    static object? Eval(object? expr, Env env)                  // (eval expr env)
    {
        if (System.Environment.GetEnvironmentVariable("SX_TRACE") == "1")
            System.Console.Error.WriteLine($"[QQE] eval={SxPrint(expr)}");
        return Evaluator.Eval(expr, env);
    }

    static object? SxExpandCall(object? expr, Env env)          // (sx-expand-call expr env)
    {
        if (System.Environment.GetEnvironmentVariable("SX_TRACE") == "1")
            System.Console.Error.WriteLine($"[SXC] call={SxPrint(expr)}");
        if (expr is Cell call && call.Car is Sym ops)
        {
            var proc = env.LookupSilent(ops.Name, null);
            if (proc is not null)
            {
                var expanded = Evaluator.ExpandMacro(proc, call.Cdr, env);
                if (expanded is not null) return expanded;
            }
        }
        return Const.FALSE;
    }

    internal static Env SxDefEnv()                                       // (sx-def-env)
        => Evaluator.CurrentMacroDefEnv ?? Evaluator.GlobalEnv;

    static Env SxExpandEnv()                                    // (sx-expand-env)
        => Evaluator.CurrentExpandEnv ?? Evaluator.GlobalEnv;

    static bool SxDefined(Sym s, Env env)                       // (sx-defined? s env)
        => env.LookupSilent(s.Name, null) is not null;

    // ── 基础 ──────────────────────────────────────────────────────────────

    static bool Atom(object? x)                                 // 1: (atom? x)
        => x is not Cell;

    static bool IsVoid(object? x)                               // 3: (void? x)
        => x is Void;

    // ── 宏展开入口 ────────────────────────────────────────────────────────

    static object? MyMacroExpand(object? expr, Env env)         // 5: (my-macro-expand expr env)
        => MyMacroExpandHelper(expr, env);

    static object? MyMacroExpandHelper(object? expr, Env env)   // 6
    {
        if (expr is not Cell e) return expr;
        if (e.Car is Sym cs && cs == SYM_QUOTE) return expr;    // (eq? (car expr) 'quote)
        if (e.Car is Sym cs2 && cs2 == SYM_QUASIQUOTE) return expr;
        // lambda 特型: 参数列表是绑定结构, 不能当调用展开 (如 (lambda () identity)
        // 的 body 符号 identity 不能变成 (identity) 宏调用); body 逐表达式展开。
        if (e.Car is Sym cs3 && cs3 == SYM_LAMBDA && !SxDefined(cs3, env))
        {
            var formals = e.Cdr is Cell fc ? fc.Car : Const.NIL;
            var body = e.Cdr is Cell fc2 ? fc2.Cdr : Const.NIL;
            return new Cell(e.Car, new Cell(formals, ExpandBody(body, env)));
        }
        var expanded = SxExpandCall(expr, env);
        if (expanded is Sym fsym && fsym == Const.FALSE)                            // (eq? expanded #f)
            return new Cell(MyMacroExpand(e.Car, env), ExpandList(e.Cdr, env));
        if (NativeSyntax.SchemeEqual(expanded, expr)) return expr;   // 恒等展开, 停止
        return MyMacroExpandHelper(expanded, env);
    }

    // cdr 是数据列表 (参数/body), 逐元素展开: 符号元素保持原样,
    // 不会像形式展开那样把 (pair? cond) 的参数 cond 误当成 (cond) 宏调用。
    static object? ExpandList(object? lst, Env env)
    {
        if (lst is Cell lc) return new Cell(MyMacroExpand(lc.Car, env), ExpandList(lc.Cdr, env));
        return lst;
    }

    static object? ExpandBody(object? body, Env env) => ExpandList(body, env);

    // ── 模式绑定 (my-definemacro 机制) ────────────────────────────────────

    static List<(Sym, object?)> MyBindPattern(object? pattern, object? args)  // 7
    {
        if (pattern is Sym ps)
        {
            if (ps == SYM_UNDERSCORE) return [];                // (eq? pattern (quote _))
            return [(ps, args)];                                // (list (cons pattern args))
        }
        if (pattern is not Cell pc || pattern is Nil) return [];
        var r1 = MyBindElem(pc.Car, ((Cell)args!).Car);
        var r2 = MyBindPattern(pc.Cdr, ((Cell)args!).Cdr);
        return [.. r1, .. r2];
    }

    static List<(Sym, object?)> MyBindElem(object? elem, object? arg)  // 8
    {
        if (elem is Sym es && es == SYM_UNDERSCORE) return [];
        if (elem is Sym es2) return [(es2, arg)];               // (symbol? elem)
        if (elem is Cell ec && ec.Car is Sym ecs && ec.Cdr is Nil)
            return [(ecs, arg)];                                // (pair? elem) (symbol? (car elem)) (null? (cdr elem))
        if (elem is Cell ec2) return MyBindPattern(ec2, arg);
        return [];
    }

    internal static object? SxMacroExpand(object? pattern, object? body, object? args, Env callEnv)  // 9
    {
        // 还原 let: bindings → params/quoted-vals → app-form → eval
        var bindings = MyBindPattern(pattern, args);
        var pars = new List<object?>();
        var quotedVals = new List<object?>();
        foreach (var (v, val) in bindings)                      // (map ... bindings)
        {
            pars.Add(v);
            quotedVals.Add(new Cell(SYM_QUOTE, new Cell(val, Const.NIL)));
        }
        var appForm = new Cell(new Cell(SYM_LAMBDA, new Cell(pars.ToCell(), body)),
                               quotedVals.ToCell());
        try { var r = Evaluator.Eval(appForm, callEnv); if (System.Environment.GetEnvironmentVariable("SX_TRACE") == "1") System.Console.Error.WriteLine($"[SXR] expand-> {SxPrint(r)}"); return r; }                // (eval app-form callenv)
        catch (Exception e)
        {
            if (System.Environment.GetEnvironmentVariable("SX_TRACE") == "1")
            {
                Console.Error.WriteLine($"[SXE] eval failed: {e.Message}");
                Console.Error.WriteLine($"  pattern={SxPrint(pattern)}");
                Console.Error.WriteLine($"  args={SxPrint(args)}");
                Console.Error.WriteLine($"  appForm={SxPrint(appForm)}");
            }
            throw;
        }
    }

    // 10-11 忽略: define-macro 机制 (my-definemacro 及 define-macro 语法注册)

    // ── quasiquote 处理 ────────────────────────────────────────────────────

    static object? QqReverseHelper(object? src, object? dst)   // 12
    {
        if (src is Nil) return dst;
        var c = (Cell)src!;
        return QqReverseHelper(c.Cdr, new Cell(c.Car, dst));
    }

    static object? QqReverse(object? l)                         // 13
        => QqReverseHelper(l, Const.NIL);

    static object? QqAppendLists(object? a, object? b)          // 14
    {
        if (a is Nil) return b;
        var c = (Cell)a!;
        return new Cell(c.Car, QqAppendLists(c.Cdr, b));
    }

    static object? QqBuildList(object? items, object? tail)     // 15
    {
        if (items is Nil) return tail;
        var c = (Cell)items!;
        return QqBuildList(c.Cdr, new Cell(c.Car, tail));
    }

    static bool QqUnquote(object? x)                            // 16
        => x is Cell c && c.Car is Sym s && s == SYM_UNQUOTE;

    static bool QqUnsplice(object? x)                           // 17
        => x is Cell c && c.Car is Sym s && s == SYM_UNQUOTE_SPLICING;

    static bool QqTailUnquote(object? tail)                     // 18
        => tail is Cell c && c.Car is Sym s && s == SYM_UNQUOTE;

    static bool QqTailUnsplice(object? tail)                    // 19
        => tail is Cell c && c.Car is Sym s && s == SYM_UNQUOTE_SPLICING;

    static object? QqProcessEl(object? el, object? items, Env env)  // 20
    {
        if (QqUnquote(el))
            return new Cell(Eval(((Cell)((Cell)el!).Cdr!).Car, env), items);  // (cons (eval (cadr el) env) items)
        if (QqUnsplice(el))
        {
            // 还原 let: v = (eval (cadr el) env)
            var v = Eval(((Cell)((Cell)el!).Cdr!).Car, env);
            if (v is Cell) return QqAppendLists(QqReverse(v), items);  // (pair? v)
            if (v is Nil) return items;                   // (null? v)
            return new Cell(v, items);                          // (cons v items)
        }
        if (el is Cell ec)
        {
            if (ec.Car is Sym es && es == SYM_QUASIQUOTE)       // (eq? (car el) 'quasiquote)
                return new Cell(el, items);
            return new Cell(QqWalk(el, env), items);
        }
        return new Cell(QqWalk(el, env), items);
    }

    static object? QqWalkListHelper(object? cur, object? items, Env env)  // 21
    {
        if (cur is Nil) return QqReverse(items);          // (null? cur)
        if (cur is not Cell cc) return QqBuildList(QqReverse(items), cur);  // (not (pair? cur))
        // 还原 let: new-items / tail
        var newItems = QqProcessEl(cc.Car, items, env);
        var tail = cc.Cdr;
        if (QqTailUnquote(tail))                                // (qq-tail-unquote? tail)
        {
            var v = Eval(((Cell)((Cell)tail!).Cdr!).Car, env);  // (eval (cadr tail) env)
            return QqBuildList(QqReverse(newItems), v);
        }
        if (QqTailUnsplice(tail))                               // (qq-tail-unsplice? tail)
        {
            var v = Eval(((Cell)((Cell)tail!).Cdr!).Car, env);
            if (v is Cell) return QqWalkListHelper(tail, QqAppendLists(QqReverse(v), newItems), env);
            if (v is Nil) return QqWalkListHelper(tail, newItems, env);
            return QqWalkListHelper(tail, new Cell(v, newItems), env);  // (cons v new-items)
        }
        return QqWalkListHelper(tail, newItems, env);
    }

    static object? QqWalkList(object? e, Env env)               // 22
        => QqWalkListHelper(e, Const.NIL, env);

    static object? QqWalkVectorHelper(object? cur, object? items, Env env)  // 23
    {
        if (cur is Nil)
            return new SchemeVector(QqReverse(items).Cells());  // (list->vector (qq-reverse items))
        if (cur is not Cell curCell) return items; // Safety check
        var el = curCell.Car;
        if (QqUnquote(el))
        {
            if (el is Cell elCell && elCell.Cdr is Cell elCdrCell)
                return QqWalkVectorHelper(curCell.Cdr,
                    new Cell(Eval(elCdrCell.Car, env), items), env);
            return QqWalkVectorHelper(curCell.Cdr, items, env);
        }
        if (QqUnsplice(el))
        {
            if (el is Cell elCell && elCell.Cdr is Cell elCdrCell)
            {
                // 还原 let: v = (eval (cadr el) env)
                var v = Eval(elCdrCell.Car, env);
                if (v is Cell) return QqWalkVectorHelper(curCell.Cdr, QqAppendLists(QqReverse(v), items), env);
                if (v is Nil) return QqWalkVectorHelper(curCell.Cdr, items, env);
                return QqWalkVectorHelper(curCell.Cdr, new Cell(v, items), env);
            }
            return QqWalkVectorHelper(curCell.Cdr, items, env);
        }
        return QqWalkVectorHelper(curCell.Cdr,
            new Cell(QqWalk(el, env), items), env);
    }

    static object? QqWalkVector(object? v, Env env)             // 24
        => QqWalkVectorHelper(((SchemeVector)v!).Data.ToCell(), Const.NIL, env);

    internal static object? QqWalk(object? e, Env env)                   // 25
    {
        if (e is Cell) return QqWalkList(e, env);               // (pair? e)
        if (e is SchemeVector) return QqWalkVector(e, env);     // (vector? e)
        return e;
    }

    // ── syntax-rules 模式匹配 ─────────────────────────────────────────────

    static object? SxLookup(Sym var, List<(Sym, object?)> bindings)  // 26
    {
        int idx = bindings.FindIndex(b => b.Item1 == var);      // (assq var bindings)
        return idx >= 0 ? bindings[idx].Item2 : null;           // (if b (cdr b) #f)
    }

    static object? EvalQs(object? expr, Env env)
    {
        if (expr is Sym s)
        {
            int idx = SxGetBindings().FindIndex(b => b.Item1 == s);
            if (idx >= 0) return SxGetBindings()[idx].Item2;
        }
        return Eval(expr, env);
    }

    static List<(Sym, object?)> SxMergeBindings(
        List<(Sym, object?)> b1, List<(Sym, object?)> b2)       // 27: (append b2 b1)
    {
        var res = new List<(Sym, object?)>(b2);
        res.AddRange(b1);
        return res;
    }

    static List<object?> SxRevAppend(object? src, List<object?> acc)  // 28
    {
        var out_ = new List<object?>(acc);
        var cur = src;
        while (cur is Cell c)                                   // 尾递归 → 迭代
        {
            out_.Insert(0, c.Car);                              // (cons (car src) acc)
            cur = c.Cdr;
        }
        return out_;
    }

    static List<object?> SxReverse(object? l)                   // 29
        => SxRevAppend(l, []);

    static List<Sym> SxMergeVars(List<Sym> a, object? b)        // 32
    {
        var out_ = new List<Sym>(a);
        var cur = b;
        while (cur is Cell c)
        {
            if (c.Car is Sym cs && !out_.Contains(cs))          // (memq (car b) a)
                out_.Insert(0, cs);                             // (cons (car b) a)
            cur = c.Cdr;
        }
        return out_;
    }

    // ── syntax-rules 模板展开 ─────────────────────────────────────────────
    // SxPatternVars / SxAccumEllipsis / SxMatch 系列 / SxExpand 系列 /
    // SxCollectSetTargets / SxEllipsisVars / SxFindListCount / SxRepeat /
    // SxSubBindings 见 NativeSyntax.cs (internal)

    internal static List<Sym> SxMutatedVars = [];                        // 41: (define *sx-mutated-vars* '())

    // ── syntax-rules 入口 ─────────────────────────────────────────────────

    internal static string SxPrint(object? v)
    {
        if (v is null || v == Const.NIL) return "()";
        if (v is Sym s) return s.Name;
        if (v is Cell c)
        {
            var parts = new List<string>();
            object? cur = c;
            while (cur is Cell cc) { parts.Add(SxPrint(cc.Car)); cur = cc.Cdr; }
            if (cur is not null && cur != Const.NIL) parts.Add(". " + SxPrint(cur));
            return "(" + string.Join(" ", parts) + ")";
        }
        return v.ToString() ?? "?";
    }

    internal static object? SxDispatch(object? args, object? lits, object? rules)  // 54
    {
        var litsList = IterCells(lits);
        var cur = rules;
        var _dbg = System.Environment.GetEnvironmentVariable("SX_TRACE");
        if (_dbg == "1") System.Console.Error.WriteLine($"[SX] dispatch args={SxPrint(args)} rules={(rules is Cell rc0 ? SxPrint(rc0.Car) : "()")}");
        while (cur is Cell rc)                                  // (if (null? rules) error ...) → 迭代
        {
            var rule = rc.Car;
            var pat = rule is Cell rcc ? rcc.Car : Const.NIL;   // (car rule)
            var tmpl = SxRuleTmpl(rule);                        // (sx-rule-tmpl rule)
            var patArgs = pat is Cell pc ? pc.Cdr : Const.NIL;  // (if (pair? pat) (cdr pat) '())
            var b = NativeSyntax.SxMatch(patArgs, args, litsList);     // (sx-match pat-args args lits)
            if (_dbg == "1") System.Console.Error.WriteLine($"[SX]   rule match={b is not null} patArgs={SxPrint(patArgs)}");
            if (b is not null)
            {
                var oldMut = SxMutatedVars;                     // 还原 let: old-mut
                SxMutatedVars = NativeSyntax.SxCollectSetTargets(tmpl, []); // (set! *sx-mutated-vars* ...)
                var r = NativeSyntax.SxExpand(tmpl, b, SxMutatedVars, SxDefEnv()); // (sx-expand tmpl b)
                SxMutatedVars = oldMut;                         // (set! *sx-mutated-vars* old-mut)
                return r;
            }
            cur = rc.Cdr;                                       // (sx-dispatch args lits (cdr rules))
        }
        throw new Exception("syntax-rules: no match");
    }

    static object? SxRuleTmpl(object? rule)                     // 55
    {
        if (rule is Cell rc && rc.Cdr is Cell rcd)              // (pair? (cdr rule))
            return rcd.Car;                                     // (cadr rule)
        return Const.NIL;                                       // '()
    }

    // ── 展开状态 (sx-with-bindings) ───────────────────────────────────────

    static List<(Sym, object?)> SxBindings = [];                // 56: (define *sx-bindings* '())

    internal static List<(Sym, object?)> SxGetBindings()                 // 57
        => SxBindings;

    static void SxSetBindings(List<(Sym, object?)> b)           // 58
        => SxBindings = b;

    static object? SxWithBindings(List<(Sym, object?)> b, Func<object?> thunk)  // 59
    {
        var old = SxBindings;                                   // 还原 let: old
        SxSetBindings(b);
        try { return thunk(); }                                 // (thunk)
        finally { SxSetBindings(old); }                         // (set! *sx-bindings* old)
    }

    static int SxGensymCounter = 0;                             // 60: (define *sx-gensym-counter* 0)

    static Sym SxGensym()                                       // 61
    {
        SxGensymCounter++;                                      // (set! *sx-gensym-counter* (+ ... 1))
        return Sym.Intern("__t" + SxGensymCounter);             // (string->symbol (string-append "__t" (number->string ...)))
    }

    // ── quasisyntax ───────────────────────────────────────────────────────

    static bool QsUnquote(object? x)                            // 62
        => x is Cell c && c.Car is Sym s && s == SYM_UNSYNTAX;

    static bool QsUnsplice(object? x)                           // 63
        => x is Cell c && c.Car is Sym s && s == SYM_UNSYNTAX_SPLICING;

    static object? QsWalkList(object? cur)                      // 64
    {
        if (cur is Nil) return Const.NIL;                 // (null? cur)
        if (cur is not Cell cc) return QsExpand(cur);           // (not (pair? cur))
        if (QsUnsplice(cc.Car))                                 // (qs-unsplice? (car cur))
        {
            // 还原 let: v = (eval (cadr (car cur)) (sx-expand-env))
            if (cc.Car is Cell carCell && carCell.Cdr is Cell cdrCell)
            {
                var v = EvalQs(cdrCell.Car, SxExpandEnv());
                return QqAppendLists(QqReverse(v), QsWalkList(cc.Cdr));
            }
            return QsWalkList(cc.Cdr);
        }
        if (QsUnquote(cc.Car))                                  // (qs-unquote? (car cur))
        {
            if (cc.Car is Cell carCell && carCell.Cdr is Cell cdrCell)
                return new Cell(EvalQs(cdrCell.Car, SxExpandEnv()), QsWalkList(cc.Cdr));
            return QsWalkList(cc.Cdr);
        }
        return new Cell(QsExpand(cc.Car), QsWalkList(cc.Cdr));
    }

    internal static object? QsExpand(object? x)                          // 65
    {
        if (x is Sym xs)                                        // (symbol? x)
            return NativeSyntax.SxExpandSym(xs, SxGetBindings(), SxMutatedVars, SxDefEnv());
        if (x is not Cell xc) return x;                         // (not (pair? x))
        if (QsUnquote(xc))                                      // (qs-unquote? x)
        {
            if (xc.Cdr is Cell cdrCell)
                return EvalQs(cdrCell.Car, SxExpandEnv());    // (eval (cadr x) (sx-expand-env))
            return x;
        }
        if (QsUnsplice(xc))                                     // (qs-unsplice? x)
        {
            if (xc.Cdr is Cell cdrCell)
                return EvalQs(cdrCell.Car, SxExpandEnv());
            return x;
        }
        if (xc.Car is Sym cs && cs == SYM_QUASISYNTAX)          // (eq? (car x) 'quasisyntax)
            return x;
        return QsWalkList(xc);                                  // (qs-walk-list x)
    }

    internal static object? SxGenTemps(object? lst)                      // 66
    {
        int n = NativeSyntax.Length(lst);                       // (length lst)
        object? acc = Const.NIL;
        for (int i = 0; i < n; i++)                             // (if (= n 0) acc (loop (- n 1) (cons (sx-gensym) acc)))
            acc = new Cell(SxGensym(), acc);
        return acc;
    }

    // ── syntax-case / with-syntax / let-syntax ────────────────────────────

    internal static object? SxSyntaxCase(object? expr, object? lits, object? clauses)  // 67
    {
        var datum = expr;                                       // (datum expr)
        var litsList = IterCells(lits);
        while (clauses is not Nil)                        // (if (null? clauses) error ...)
        {
            var cl = clauses is Cell clc ? clc.Car : Const.NIL;
            var restCl = cl is Cell clcc ? clcc.Cdr : Const.NIL; // (cdr cl)
            var pat = cl is Cell clc2 ? clc2.Car : Const.NIL;   // (car cl)
            var hasFender = restCl is Cell rcc && rcc.Cdr is Cell;  // (if (pair? rest-cl) (pair? (cdr rest-cl)) #f)
            object? fender = hasFender ? ((Cell)restCl!).Car : null;  // (if has-fender (car rest-cl) #f)
            var tmpl = hasFender ? ((Cell)((Cell)restCl!).Cdr!).Car
                                 : ((Cell)restCl!).Car;         // (if has-fender (cadr rest-cl) (car rest-cl))
            var b = NativeSyntax.SxMatch(pat, datum, litsList);  // (sx-match pat datum lits)
            // (if b (if (or (not has-fender) (sx-check-fender fender b)) (sx-eval-tmpl tmpl b) 递归)
            if (b is not null && (!hasFender || SxCheckFender(fender!, b)))
                return SxEvalTmpl(tmpl, b);
            clauses = clauses is Cell cl3 ? cl3.Cdr : Const.NIL;  // (sx-syntax-case datum lits (cdr clauses))
        }
        throw new Exception("syntax-case: no match");
    }

    static bool SxCheckFender(object? fender, List<(Sym, object?)> b)  // 68
        // (not (eq? (eval fender (sx-expand-env)) #f))
        => (bool)SxWithBindings(b,
            () => !(Eval(fender, SxExpandEnv()) is Sym fsym && fsym == Const.FALSE))!;

    static object? SxEvalTmpl(object? tmpl, List<(Sym, object?)> b)  // 69
    {
        // 还原 let: r = (eval tmpl (sx-expand-env))
        var r = SxWithBindings(b, () => Eval(tmpl, SxExpandEnv()));
        if (r is Sym rs)                                        // (symbol? r)
            return new Cell(SYM_QUOTE, new Cell(rs, Const.NIL));  // (list 'quote r)
        return r;
    }

    internal static object? SxWithSyntax(object? pairs, object? body)    // 70
    {
        var acc = new List<(Sym, object?)>();
        var ps = pairs;
        while (ps is Cell psc)                             // (if (null? ps) ...) → 迭代
        {
            var p = psc.Car;
            var pat = p is Cell pc ? pc.Car : Const.NIL;        // (caar ps)
            var val = ((Cell)((Cell)p!).Cdr!).Car;              // (cadar ps)
            var b = NativeSyntax.SxMatch(pat, val, []);          // (sx-match pat val .())
            if (b is null) throw new Exception("with-syntax: no match");
            acc = SxMergeBindings(acc, b);                      // (loop (cdr ps) (sx-merge-bindings acc b))
            ps = psc.Cdr;
        }
        // (sx-with-bindings (sx-merge-bindings acc (sx-get-bindings))
        //                   (lambda () (sx-eval-body body (sx-expand-env))))
        return SxWithBindings(SxMergeBindings(acc, SxGetBindings()),
                              () => SxEvalBody(body, SxExpandEnv()));
    }

    static object? SxEvalBody(object? body, Env env)            // 71
    {
        object? last = Const.VOID;                              // (last (void))
        var cur = body;
        while (cur is Cell c)                                   // (for-each (lambda (form) (set! last (eval form env))) body)
        {
            last = Eval(c.Car, env);
            cur = c.Cdr;
        }
        return last;
    }

    internal static object? SxLetSyntax(object? bindings, object? body)  // 72
    {
        // (append (map sx-make-macro-binding bindings) body)
        var inner = new List<object?>();
        foreach (var b in IterCells(bindings))
            inner.Add(SxMakeMacroBinding(b));
        inner.AddRange(IterCells(body));
        return new Cell(new Cell(SYM_LAMBDA, new Cell(Const.NIL, inner.ToCell())), Const.NIL);
        // (list (cons 'lambda (cons '() ...)))
    }

    internal static object? SxMakeMacroBinding(object? binding)          // 73
    {
        var bc = (Cell)binding!;
        var name = bc.Car;                                      // (car binding)
        var trans = ((Cell)bc.Cdr!).Car;                        // (cadr binding)
        if (trans is Cell tc && tc.Car is Sym ts && ts == SYM_SYNTAX_RULES)
        {
            // (if (pair? trans) (eq? (car trans) 'syntax-rules) #f)
            var lits = tc.Cdr is Cell tcd ? tcd.Car : Const.NIL;  // (if (pair? (cdr trans)) (cadr trans) '())
            var rules = tc.Cdr is Cell tcd2 ? tcd2.Cdr : Const.NIL;  // (cddr trans)
            // (list 'define-macro (cons name 'args)
            //       (list 'sx-dispatch 'args (list 'quote lits) (list 'quote rules)))
            return new Cell(SYM_DEFINE_MACRO, new Cell(new Cell(name, SYM_ARGS), new Cell(
                new Cell(SYM_SX_DISPATCH, new Cell(SYM_ARGS, new Cell(
                    new Cell(SYM_QUOTE, new Cell(lits, Const.NIL)),
                    new Cell(new Cell(SYM_QUOTE, new Cell(rules, Const.NIL)), Const.NIL)))),
                Const.NIL)));
        }
        // (list 'define-macro (cons name 'args)
        //       (list (cons 'lambda (cdr trans)) (list 'cons (list 'quote name) 'args)))
        return new Cell(SYM_DEFINE_MACRO, new Cell(new Cell(name, SYM_ARGS), new Cell(
            new Cell(new Cell(SYM_LAMBDA, ((Cell)trans!).Cdr),
                     new Cell(new Cell(SYM_CONS, new Cell(new Cell(SYM_QUOTE, new Cell(name, Const.NIL)),
                                                          new Cell(SYM_ARGS, Const.NIL))), Const.NIL)),
            Const.NIL)));
    }
    // ── 公开入口 (REPL ,expand) ─────────────────────────────────────────

    public static object? Expand(object? expr, Env env) => MyMacroExpand(expr, env);

}
