using System.Globalization;
using System.Text;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Features.Export;

/// <summary>
/// One accrual flattened for CSV export. <see cref="AmountInBaseCurrency"/>
/// follows the canonical currency-aggregation contract (a <c>null</c> rate ⇒ the
/// row is already in the base currency, i.e. 1:1) — kept here so the export
/// matches every dashboard/budget aggregate. See CLAUDE.md "Currency aggregation
/// contract".
/// </summary>
public sealed record AccrualExportRow(
    DateTimeOffset Date,
    AccrualType Type,
    decimal Amount,
    string Currency,
    decimal? ExchangeRate,
    string? CategoryName,
    string? Description,
    bool IncludeInStats,
    Guid? GroupId,
    IReadOnlyList<string> Tags)
{
    public decimal AmountInBaseCurrency => ExchangeRate.HasValue ? Amount * ExchangeRate.Value : Amount;
}

/// <summary>Generates a CSV document from exported accrual rows.</summary>
public interface IAccrualCsvExporter
{
    /// <summary>
    /// Renders <paramref name="rows"/> to a UTF-8 CSV document (with BOM, so
    /// Excel detects the encoding and Cyrillic text is preserved).
    /// </summary>
    byte[] Export(IEnumerable<AccrualExportRow> rows);
}

/// <summary>
/// RFC 4180 CSV exporter (acceptance criterion #9 «CsvExporter — генерирует
/// корректный CSV»). Fields are comma-separated and CRLF-terminated; any field
/// containing a comma, double-quote or line break is wrapped in double-quotes
/// with embedded quotes doubled. Numbers use the invariant culture so the decimal
/// point never collides with the comma delimiter and the file is locale-stable.
/// </summary>
public sealed class CsvAccrualExporter : IAccrualCsvExporter
{
    private static readonly string[] Header =
    [
        "Date", "Type", "Amount", "Currency", "ExchangeRate",
        "AmountInBaseCurrency", "Category", "Description", "IncludeInStats",
        "GroupId", "Tags",
    ];

    public byte[] Export(IEnumerable<AccrualExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var sb = new StringBuilder();
        AppendRecord(sb, Header);

        foreach (var row in rows)
        {
            AppendRecord(sb,
            [
                row.Date.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                row.Type.ToString(),
                row.Amount.ToString(CultureInfo.InvariantCulture),
                row.Currency,
                row.ExchangeRate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.AmountInBaseCurrency.ToString(CultureInfo.InvariantCulture),
                row.CategoryName ?? string.Empty,
                row.Description ?? string.Empty,
                row.IncludeInStats ? "true" : "false",
                row.GroupId?.ToString() ?? string.Empty,
                string.Join("; ", row.Tags),
            ]);
        }

        // UTF-8 with BOM: lets spreadsheet apps detect the encoding so Cyrillic
        // descriptions/categories render correctly. GetBytes never emits the
        // preamble, so prepend it explicitly.
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(sb.ToString());

        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    private static void AppendRecord(StringBuilder sb, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Escape(fields[i]));
        }

        sb.Append("\r\n");
    }

    private static string Escape(string field)
    {
        if (field.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return field;

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
