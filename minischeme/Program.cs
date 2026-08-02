using Miniscm.Types;
using Miniscm.Eval;
using Miniscm.Primitives;
using Miniscm.Reader;
using Void = Miniscm.Types.Void;

namespace Miniscm;

public class Program
{
    public static void Main(string[] args)
    {
        Evaluator.InitSpecials();
        PrimitiveRegistry.Init();

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        var scmDir = Path.Combine(projectDir, "scm");

        var _libs = new[] {
            "my-definemacro.scm", "boot-min2.scm", "boot-core.scm", "boot-sugar.scm",
            "char-boolean.scm", "numeric.scm",
            "srfi-1-list.scm", "srfi-13-string.scm", "hof-vector.scm",
            "number-theory.scm", "gensym-stream.scm",
            "data-structures-ext.scm", "srfi-14-char-set.scm",
            "generators.scm", "misc.scm", "fill-gaps.scm"
        };
        if (Directory.Exists(scmDir))
        {
            foreach (var lib in _libs)
            {
                var path = Path.Combine(scmDir, lib);
                if (!File.Exists(path)) continue;
                try
                {
                    var src = File.ReadAllText(path);
                    var exprs = Parser.ReadAll(src);
                    foreach (var expr in exprs)
                    {
                        try { Evaluator.Eval(expr, Evaluator.GlobalEnv); }
                        catch { }
                    }
                }
                catch { }
            }
        }

        if (args.Length > 0)
        {
            foreach (var path in args)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var src = File.ReadAllText(path);
                        var exprs = Parser.ReadAll(src);
                        foreach (var expr in exprs)
                        {
                            try { Evaluator.Eval(expr, Evaluator.GlobalEnv); }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            return;
        }

        Console.WriteLine("miniscm .NET Core — Scheme interpreter");
        while (true)
        {
            Console.Write("mscm> ");
            var line = Console.ReadLine();
            if (line is null) break;

            int depth = 0;
            foreach (var c in line) { if (c == '(') depth++; if (c == ')') depth--; }
            while (depth > 0)
            {
                Console.Write(".> ");
                var next = Console.ReadLine();
                if (next is null) break;
                line += "\n" + next;
                foreach (var c in next) { if (c == '(') depth++; if (c == ')') depth--; }
            }

            line = line.Trim();
            if (line.Length == 0) continue;

            if (line == ",quit" || line == "(exit)")
                break;

            try
            {
                var exprs = Parser.ReadAll(line);
                foreach (var expr in exprs)
                {
                    if (expr is Eof) continue;
                    var r = Evaluator.Eval(expr, Evaluator.GlobalEnv);
                    if (r is not Void)
                        Console.WriteLine(Printer.Format(r));
                }
            }
            catch { }
        }
    }
}
