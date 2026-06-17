using System.Text;
using FinanceTracker.Application.Features.Export;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.UnitTests.Export;

/// <summary>
/// Acceptance criterion #9 — «CsvExporter генерирует корректный CSV»: RFC 4180
/// escaping, invariant-culture numbers, the base-currency conversion column, and
/// a UTF-8 BOM so spreadsheet apps read Cyrillic correctly.
/// </summary>
public class CsvAccrualExporterTests
{
    private static readonly DateTimeOffset Date = new(2026, 6, 17, 9, 30, 0, TimeSpan.Zero);
    private readonly CsvAccrualExporter _exporter = new();

    private static string Decode(byte[] bytes)
    {
        // Strip the UTF-8 BOM (asserted separately) before inspecting the text.
        var preamble = Encoding.UTF8.GetPreamble();
        var hasBom = bytes.Length >= preamble.Length && bytes.Take(preamble.Length).SequenceEqual(preamble);
        return Encoding.UTF8.GetString(hasBom ? bytes[preamble.Length..] : bytes);
    }

    private static AccrualExportRow Row(
        decimal amount = 100m,
        string currency = "RUB",
        decimal? rate = null,
        string? category = null,
        string? description = null,
        bool includeInStats = true,
        Guid? groupId = null,
        IReadOnlyList<string>? tags = null) =>
        new(Date, AccrualType.Expense, amount, currency, rate, category, description,
            includeInStats, groupId, tags ?? []);

    [Fact]
    public void EmptyInput_WritesHeaderOnly()
    {
        var csv = Decode(_exporter.Export([]));

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Equal(
            "Date,Type,Amount,Currency,ExchangeRate,AmountInBaseCurrency,Category,Description,IncludeInStats,GroupId,Tags",
            lines[0]);
    }

    [Fact]
    public void Output_StartsWithUtf8Bom()
    {
        var bytes = _exporter.Export([Row()]);
        Assert.True(bytes.Take(3).SequenceEqual(Encoding.UTF8.GetPreamble()));
    }

    [Fact]
    public void RecordsAreCrlfTerminated()
    {
        var csv = Decode(_exporter.Export([Row()]));
        Assert.EndsWith("\r\n", csv);
        // Header + one data record, each ending with CRLF.
        Assert.Equal(2, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Theory]
    [InlineData("plain text", "plain text")]                 // no special chars → verbatim
    [InlineData("a,b", "\"a,b\"")]                            // comma → quoted
    [InlineData("line1\nline2", "\"line1\nline2\"")]          // newline → quoted
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]          // quote → quoted + doubled
    public void Description_IsEscapedPerRfc4180(string description, string expectedField)
    {
        var csv = Decode(_exporter.Export([Row(description: description)]));
        var dataLine = csv.Split("\r\n")[1];
        // Description is the 8th column (index 7).
        Assert.Contains(expectedField, dataLine);
    }

    [Fact]
    public void Numbers_UseInvariantCulture()
    {
        // A rate that would render with a comma in ru-RU must keep the dot here, or
        // it would collide with the field delimiter and corrupt the CSV.
        var csv = Decode(_exporter.Export([Row(amount: 1234.56m, rate: 1.5m)]));
        var fields = csv.Split("\r\n")[1].Split(',');

        Assert.Equal("1234.56", fields[2]); // Amount
        Assert.Equal("1.5", fields[4]);     // ExchangeRate
    }

    [Fact]
    public void AmountInBaseCurrency_NullRate_EqualsAmount()
    {
        var csv = Decode(_exporter.Export([Row(amount: 250m, currency: "RUB", rate: null)]));
        var fields = csv.Split("\r\n")[1].Split(',');

        Assert.Equal(string.Empty, fields[4]); // ExchangeRate empty
        Assert.Equal("250", fields[5]);        // AmountInBaseCurrency == Amount
    }

    [Fact]
    public void AmountInBaseCurrency_WithRate_IsAmountTimesRate()
    {
        var csv = Decode(_exporter.Export([Row(amount: 100m, currency: "USD", rate: 90m)]));
        var fields = csv.Split("\r\n")[1].Split(',');

        Assert.Equal("90", fields[4]);   // ExchangeRate
        Assert.Equal("9000", fields[5]); // 100 × 90
    }

    [Fact]
    public void Tags_AreJoinedWithSemicolon()
    {
        var csv = Decode(_exporter.Export([Row(tags: ["food", "lunch"])]));
        Assert.Contains("food; lunch", csv);
    }
}
