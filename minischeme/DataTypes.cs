using System.Text;

namespace Miniscm.Types;

public sealed class SchemeString
{
    public List<int> Data { get; }

    public SchemeString(string s)
    {
        Data = [];
        foreach (var rune in s.EnumerateRunes())
            Data.Add(rune.Value);
    }

    public SchemeString(IEnumerable<int> codepoints) => Data = [.. codepoints];

    public int Length => Data.Count;
    public int this[int i] { get => Data[i]; set => Data[i] = value; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var cp in Data)
            sb.Append(char.ConvertFromUtf32(cp));
        return sb.ToString();
    }

    public override int GetHashCode() => ToString().GetHashCode();
    public override bool Equals(object? obj) => obj is SchemeString ss && ToString() == ss.ToString()
        || obj is string s && ToString() == s;
}

public sealed class SchemeChar : IEquatable<SchemeChar>
{
    public int Codepoint { get; }

    public SchemeChar(int codepoint)
    {
        if (!Rune.IsValid(codepoint))
            throw new Exception($"invalid codepoint: {codepoint}");
        Codepoint = codepoint;
    }

    public bool Equals(SchemeChar? other) => other is not null && Codepoint == other.Codepoint;
    public override bool Equals(object? obj) => obj is SchemeChar c && Codepoint == c.Codepoint;
    public override int GetHashCode() => Codepoint;

    public override string ToString()
    {
        var s = char.ConvertFromUtf32(Codepoint);
        if (s.Length == 1)
        {
            return s[0] switch
            {
                ' ' => "#\\space",
                '\n' => "#\\newline",
                '\t' => "#\\tab",
                '\r' => "#\\return",
                '\0' => "#\\nul",
                '\a' => "#\\alarm",
                '\b' => "#\\backspace",
                '\x1b' => "#\\escape",
                '\x7f' => "#\\delete",
                _ => "#\\" + s
            };
        }
        return "#\\" + s;
    }
}

public sealed class SchemeVector
{
    public List<object?> Data { get; }

    public SchemeVector() => Data = [];
    public SchemeVector(IEnumerable<object?> items) => Data = [.. items];
    public SchemeVector(int size) => Data = new List<object?>(new object?[size]);

    public int Length => Data.Count;
    public object? this[int i] { get => Data[i]; set => Data[i] = value; }

    public override string ToString()
    {
        var sb = new StringBuilder("#(");
        for (int i = 0; i < Data.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(Printer.Format(Data[i]));
        }
        sb.Append(')');
        return sb.ToString();
    }
}

public sealed class SchemeBytevector
{
    public byte[] Data { get; }

    public SchemeBytevector(byte[] data) => Data = data;
    public SchemeBytevector(IEnumerable<int> ints) => Data = [.. ints.Select(i => (byte)i)];
    public SchemeBytevector(string s) => Data = Encoding.UTF8.GetBytes(s);

    public int Length => Data.Length;
    public byte this[int i] { get => Data[i]; set => Data[i] = value; }

    public override bool Equals(object? obj) => obj is SchemeBytevector b && Data.AsSpan().SequenceEqual(b.Data);
    public override int GetHashCode() => HashCode.Combine(Data.Length, Data.Length == 0 ? 0 : Data[0]);

    public override string ToString() => "#u8(" + string.Join(",", Data) + ")";
}
