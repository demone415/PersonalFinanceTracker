using ClosedXML.Excel;
using FinanceTracker.Application.Features.Import;
using FinanceTracker.Infrastructure.Persistence.Import;

namespace FinanceTracker.UnitTests.Import;

/// <summary>
/// The FNS «Налоги ФЛ» Excel parser (Story 6.1): grouping the flat one-row-per-item
/// sheet into receipts by «Номер чека», splitting the seller INN out of the
/// organisation cell, locating columns by header (order-independent), and parsing
/// dates/decimals. Driven against real workbooks built in-memory with ClosedXML.
/// </summary>
public class ClosedXmlFnsReceiptParserTests
{
    private static readonly string[] Headers =
    [
        "Дата и время чека", "Итого по чеку", "ИНН продавца", "Место расчета",
        "Номер чека", "Описание", "Категория", "Товар", "Цена", "Количество", "Сумма по товару",
    ];

    private static Stream BuildWorkbook(params string[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Receipts");
        for (var c = 0; c < Headers.Length; c++)
            ws.Cell(1, c + 1).Value = Headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = rows[r][c];

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static string[] Row(
        string date, string total, string seller, string place, string number,
        string goods, string price, string qty, string sum) =>
        [date, total, seller, place, number, "", "", goods, price, qty, sum];

    [Fact]
    public void GroupsRowsByReceiptNumber_AndParsesItems()
    {
        using var stream = BuildWorkbook(
            Row("25.09.2025 17:18", "369.98", "ИП ИП Иванов 7728029110", "Москва", "49",
                "AGAMA Креветки 400г", "184.99", "2", "369.98"),
            Row("25.09.2025 17:18", "369.98", "ИП ИП Иванов 7728029110", "Москва", "49",
                "Сыр 48% 210г", "169.99", "1", "169.99"),
            Row("29.01.2023 20:49", "24999", "ООО \"ДНС Ритейл\" 2540167061", "ТЦ Водный", "39",
                "Электронная книга ONYX", "24999.00", "1", "24999.00"));

        var receipts = new ClosedXmlFnsReceiptParser().Parse(stream);

        Assert.Equal(2, receipts.Count);

        var r49 = receipts.Single(r => r.ExternalNumber == "49");
        Assert.Equal("7728029110", r49.Inn);
        Assert.Equal("ИП ИП Иванов", r49.Organization);
        Assert.Equal(new DateTimeOffset(2025, 9, 25, 17, 18, 0, TimeSpan.Zero), r49.Date);
        Assert.Equal(2, r49.Items.Count);
        Assert.Equal(184.99m, r49.Items[0].Price);
        Assert.Equal(2m, r49.Items[0].Quantity);
        Assert.Equal(369.98m, r49.Total);

        var r39 = receipts.Single(r => r.ExternalNumber == "39");
        Assert.Equal("2540167061", r39.Inn);
        Assert.Equal("ООО \"ДНС Ритейл\"", r39.Organization);
        Assert.Single(r39.Items);
    }

    [Fact]
    public void SameNumber_DifferentSeller_AreSeparateReceipts()
    {
        using var stream = BuildWorkbook(
            Row("01.04.2026 10:00", "100", "Магнит 7700000001", "А", "1", "Хлеб", "100", "1", "100"),
            Row("02.05.2026 11:00", "200", "Пятёрочка 7700000002", "Б", "1", "Молоко", "200", "1", "200"));

        var receipts = new ClosedXmlFnsReceiptParser().Parse(stream);

        Assert.Equal(2, receipts.Count);
        Assert.All(receipts, r => Assert.Equal("1", r.ExternalNumber));
        Assert.Equal(["7700000001", "7700000002"], receipts.Select(r => r.Inn).Order());
    }

    [Fact]
    public void MissingRequiredColumn_Throws()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Receipts");
        ws.Cell(1, 1).Value = "Дата и время чека"; // no «Номер чека» / «Товар» / …
        ws.Cell(2, 1).Value = "01.01.2026 00:00";
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        Assert.Throws<FnsImportFormatException>(() => new ClosedXmlFnsReceiptParser().Parse(ms));
    }

    [Fact]
    public void NonXlsxStream_ThrowsFormatException()
    {
        using var garbage = new MemoryStream("this is not a workbook"u8.ToArray());
        Assert.Throws<FnsImportFormatException>(() => new ClosedXmlFnsReceiptParser().Parse(garbage));
    }
}
