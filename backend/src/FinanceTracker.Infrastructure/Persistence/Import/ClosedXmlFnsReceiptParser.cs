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

    public FnsParseResult Parse(Stream excel)
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
            var warnings = new List<string>();
            var rowsFailed = 0;

            var lastRow = ws.LastRowUsed()!.RowNumber();
            for (var r = headerRow.RowNumber() + 1; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                var number = Text(row.Cell(cNumber));
                var goods = Text(row.Cell(cGoods));

                // Skip fully-blank rows silently; a real line always has a receipt number.
                if (number.Length == 0 && goods.Length == 0) continue;

                // An item with no receipt number can't be grouped — report and skip it
                // instead of dropping it silently.
                if (number.Length == 0)
                {
                    warnings.Add($"Строка {r}: позиция «{Trunc(goods)}» без номера чека — пропущена.");
                    rowsFailed++;
                    continue;
                }

                // A row with an unreadable date can't be keyed (the date is part of the
                // receipt identity) — report and skip it.
                if (!TryParseDate(row.Cell(cDate), out var date))
                {
                    warnings.Add($"Строка {r}: неразборчивая дата чека «{Text(row.Cell(cDate))}» (чек {number}) — пропущена.");
                    rowsFailed++;
                    continue;
                }

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
                        Total = ParseDecimal(row.Cell(cTotal), r, "Итого по чеку", number, warnings),
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
                        ParseDecimal(row.Cell(cPrice), r, "Цена", number, warnings),
                        ParseDecimal(row.Cell(cQty), r, "Количество", number, warnings),
                        ParseDecimal(row.Cell(cSum), r, "Сумма по товару", number, warnings)));
                }
            }

            var receipts = ordered
                .Select(m => new ParsedReceipt(
                    m.ExternalNumber, m.Inn, m.Organization, m.Address, m.Date,
                    m.Total, m.Category, m.Description, m.Items))
                .ToList();

            return new FnsParseResult(receipts, warnings, rowsFailed);
        }
    }

    private static string Trunc(string s) => s.Length <= 40 ? s : s[..40] + "…";

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

    // The FNS export carries no time-zone; the wall-clock value is local Moscow
    // time. We label it UTC verbatim (no offset conversion) so imported receipts
    // keep the exact timestamp shown in the export and the dedup key is stable.
    // Consequence: an imported receipt's stored instant can differ by the MSK
    // offset from a QR-scanned one — acceptable while imports are RU-only.
    private static bool TryParseDate(IXLCell cell, out DateTimeOffset date)
    {
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var dt))
        {
            date = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
            return true;
        }

        var s = cell.GetString().Trim();
        foreach (var fmt in DateFormats)
            if (DateTime.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var p))
            {
                date = new DateTimeOffset(DateTime.SpecifyKind(p, DateTimeKind.Utc));
                return true;
            }

        date = default;
        return false;
    }

    /// <summary>
    /// Reads a numeric cell, falling back to 0 when it is blank or unparseable. A
    /// non-blank unparseable value is reported as a warning (rather than silently
    /// becoming 0) so the user can see which amount we couldn't read.
    /// </summary>
    private static decimal ParseDecimal(
        IXLCell cell, int rowNumber, string header, string receiptNumber, List<string> warnings)
    {
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var d))
            return (decimal)d;

        var raw = cell.GetString().Trim();
        if (raw.Length == 0) return 0m;

        var s = raw
            .Replace(" ", "").Replace(" ", "").Replace(',', '.');
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return v;

        warnings.Add($"Строка {rowNumber}: неразборчивое число в «{header}» «{raw}» (чек {receiptNumber}) — принято за 0.");
        return 0m;
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
