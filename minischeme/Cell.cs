using System.Collections;
using System.Text;

namespace Miniscm.Types;

public sealed class Cell : IEnumerable<object?>
{
    public object? Car { get; set; }
    public object? Cdr { get; set; }

    public Cell(object? car, object? cdr)
    {
        Car = car;
        Cdr = cdr;
    }

    public int Length
    {
        get
        {
            int n = 0;
            var cur = this;
            while (true)
            {
                n++;
                if (cur.Cdr is Cell next) cur = next;
                else break;
            }
            return n;
        }
    }

    public object? this[int index]
    {
        get
        {
            var cur = this;
            for (int i = 0; i < index; i++)
            {
                if (cur.Cdr is Cell next) cur = next;
                else throw new IndexOutOfRangeException();
            }
            return cur.Car;
        }
    }

    public IEnumerator<object?> GetEnumerator()
    {
        var cur = this;
        while (true)
        {
            yield return cur.Car;
            if (cur.Cdr is Cell next) cur = next;
            else yield break;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override int GetHashCode() => HashCode.Combine(Car, Cdr);
    public override bool Equals(object? obj) => obj is Cell c && Equals(Car, c.Car) && Equals(Cdr, c.Cdr);

    public override string ToString()
    {
        var sb = new StringBuilder("(");
        ToStringHelper(sb, new HashSet<Cell>());
        sb.Append(')');
        return sb.ToString();
    }

    private void ToStringHelper(StringBuilder sb, HashSet<Cell> seen)
    {
        if (seen.Contains(this)) { sb.Append("..."); return; }
        seen.Add(this);
        sb.Append(Printer.Format(Car));
        var cur = Cdr;
        while (cur is Cell cell)
        {
            if (seen.Contains(cell)) { sb.Append(" ..."); break; }
            seen.Add(cell);
            sb.Append(' ');
            sb.Append(Printer.Format(cell.Car));
            cur = cell.Cdr;
        }
        if (cur is not Nil) { sb.Append(" . "); sb.Append(Printer.Format(cur)); }
    }
}

public static class CellHelper
{
    public static Cell? AsCell(this object? v) => v as Cell;
    public static string AsString(this object? v) => v is Sym s ? s.Name : (v as string) ?? "";

    public static int CellLength(this object? v)
    {
        int n = 0;
        var cur = v;
        while (cur is Cell cell) { n++; cur = cell.Cdr; }
        return n;
    }

    public static List<object?> Cells(this object? v)
    {
        var res = new List<object?>();
        var cur = v;
        while (cur is Cell cell) { res.Add(cell.Car); cur = cell.Cdr; }
        return res;
    }

    public static object? ToCell(this IEnumerable<object?> items)
    {
        object? head = Const.NIL;
        Cell? tail = null;
        foreach (var x in items)
        {
            var n = new Cell(x, Const.NIL);
            if (tail is null) head = n;
            else tail.Cdr = n;
            tail = n;
        }
        return head;
    }
}
