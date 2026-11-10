using System.Globalization;
using System.Text;
namespace Safqa.API;

public static class Csv
{
    /// UTF-8 with BOM (so Excel opens Arabic correctly); guards against CSV/formula injection in text cells.
    public static byte[] Build(string[] headers, IEnumerable<object?[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendJoin(',', headers.Select(Cell)).Append("\r\n");
        foreach (var r in rows) sb.AppendJoin(',', r.Select(Cell)).Append("\r\n");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    static string Cell(object? v)
    {
        var s = v switch
        {
            null => "",
            DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => v.ToString() ?? ""
        };
        if (v is string && s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s;
        return s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
