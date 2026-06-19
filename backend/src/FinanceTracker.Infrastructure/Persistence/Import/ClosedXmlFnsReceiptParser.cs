using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using FinanceTracker.Application.Features.Import;

namespace FinanceTracker.Infrastructure.Persistence.Import;

/// <summary>
/// <see cref="IFnsReceiptParser"/> over ClosedXML. The FNS «Налоги ФЛ» export is a
/// flat sheet — one row per receipt line item — with the receipt-level columns
/// (date, total, seller, address, number) repeated across the rows of one receipt.
/// Rows are grouped by «Номер чека» (+ seller INN + date) into receipts; columns
/// are located by header name, so column reordering is tolerated.
/// </summary>
public sealed partial class ClosedXmlFnsReceiptParser : IFnsReceiptParser
{
    // Header names (lower-cased for case-insensitive matching).
    private const string HDate = "дата и время чека";
    private const string HTotal = "итого по чеку";
    private const string HSeller = "инн продавца";
    private const string HPlace = "место расчета";
    private const string HNumber = "номер чека";
    private const string HDescription = "описание";
    private const string HCategory = "категория";
    private const string HGoods = "товар";
    private const string HPrice = "цена";
    private const string HQuantity = "количество";
    private const string HSum = "сумма по товару";

    private static readonly string[] DateFormats =
        ["dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy"];

    public IReadOnlyList<ParsedReceipt> Parse(Stream excel)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(excel);
        }
        catch (Exception ex)
        {
            throw new FnsImportFormatException($"Не удалось прочитать книгу Excel: {ex.Message}");
        }

        using (workbook)
        {
            var ws = workbook.Worksheets.FirstOrDefault()
                ?? throw new FnsImportFormatException("В файле нет ни одного листа.");

            var headerRow = ws.FirstRowUsed()
                ?? throw new FnsImportFormatException("Лист пуст — нет строки заголовков.");

            var columns = MapColumns(headerRow);
            int Col(string header) => columns.TryGetValue(header, out var c)
                ? c
                : throw new FnsImportFormatException($"В файле нет обязательного столбца «{header}».");

            int cNumber = Col(HNumber), cDate = Col(HDate), cTotal = Col(HTotal),
                cGoods = Col(HGoods), cPrice = Col(HPrice), cQty = Col(HQuantity), cSum = Col(HSum);
            columns.TryGetValue(HSeller, out var cSeller);
            columns.TryGetValue(HPlace, out var cPlace);
            columns.TryGetValue(HDescription, out var cDesc);
            columns.TryGetValue(HCategory, out var cCat);

            var ordered = new List<MutableReceipt>();
            var byKey = new Dictionary<string, MutableReceipt>(StringComparer.Ordinal);

            var lastRow = ws.LastRowUsed()!.RowNumber();
            for (var r = headerRow.RowNumber() + 1; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                var number = Text(row.Cell(cNumber));
                var goods = Text(row.Cell(cGoods));

                // Skip fully-blank rows; a real line always has a receipt number.
                if (number.Length == 0 && goods.Length == 0) continue;
                if (number.Length == 0) continue;

                var date = ParseDate(row.Cell(cDate));
                var (org, inn) = ParseSeller(cSeller > 0 ? Text(row.Cell(cSeller)) : "");
                var key = $"{number}|{inn ?? ""}|{date.UtcDateTime:O}";

                if (!byKey.TryGetValue(key, out var receipt))
                {
                    receipt = new MutableReceipt
                    {
                        ExternalNumber = number,
                        Inn = inn,
                        Organization = org,
                        Address = cPlace > 0 ? NullIfBlank(Text(row.Cell(cPlace))) : null,
                        Date = date,
                        Total = ParseDecimal(row.Cell(cTotal)),
                        Description = cDesc > 0 ? NullIfBlank(Text(row.Cell(cDesc))) : null,
                        Category = cCat > 0 ? NullIfBlank(Text(row.Cell(cCat))) : null,
                    };
                    byKey[key] = receipt;
                    ordered.Add(receipt);
                }

                if (goods.Length > 0)
                {
                    receipt.Items.Add(new ParsedReceiptItem(
                        goods,
                        ParseDecimal(row.Cell(cPrice)),
                        ParseDecimal(row.Cell(cQty)),
                        ParseDecimal(row.Cell(cSum))));
                }
            }

            return ordered
                .Select(m => new ParsedReceipt(
                    m.ExternalNumber, m.Inn, m.Organization, m.Address, m.Date,
                    m.Total, m.Category, m.Description, m.Items))
                .ToList();
        }
    }

    private static Dictionary<string, int> MapColumns(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cell in headerRow.CellsUsed())
        {
            var name = cell.GetString().Trim().ToLowerInvariant();
            if (name.Length > 0) map[name] = cell.Address.ColumnNumber;
        }
        return map;
    }

    private static string Text(IXLCell cell) => cell.GetString().Trim();

    private static string? NullIfBlank(string s) => s.Length == 0 ? null : s;

    private static DateTimeOffset ParseDate(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var dt))
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));

        var s = cell.GetString().Trim();
        foreach (var fmt in DateFormats)
            if (DateTime.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var p))
                return new DateTimeOffset(DateTime.SpecifyKind(p, DateTimeKind.Utc));

        throw new FnsImportFormatException($"Неразборчивая дата чека: «{s}».");
    }

    private static decimal ParseDecimal(IXLCell cell)
    {
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var d))
            return (decimal)d;

        var s = cell.GetString().Trim()
            .Replace(" ", "").Replace(" ", "").Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    /// <summary>
    /// The seller column bundles the organisation name and its INN, e.g.
    /// <c>ООО "ДНС Ритейл" 2540167061</c>. Splits the trailing 10/12-digit INN out
    /// from the name.
    /// </summary>
    private static (string? Organization, string? Inn) ParseSeller(string raw)
    {
        var s = raw.Trim();
        if (s.Length == 0) return (null, null);

        var m = InnRegex().Match(s);
        if (!m.Success) return (s, null);

        // Strip the INN out; keep the organisation name verbatim (do not strip its
        // own quotes, e.g. ООО "ДНС Ритейл"). Only trailing separators are cleaned.
        var org = s.Remove(m.Index, m.Length).Trim().TrimEnd(',', ' ', '\t');
        return (org.Length == 0 ? null : org, m.Value);
    }

    [GeneratedRegex(@"\b\d{10,12}\b")]
    private static partial Regex InnRegex();

    private sealed class MutableReceipt
    {
        public required string ExternalNumber { get; init; }
        public string? Inn { get; init; }
        public string? Organization { get; init; }
        public string? Address { get; init; }
        public DateTimeOffset Date { get; init; }
        public decimal Total { get; init; }
        public string? Description { get; init; }
        public string? Category { get; init; }
        public List<ParsedReceiptItem> Items { get; } = [];
    }
}
