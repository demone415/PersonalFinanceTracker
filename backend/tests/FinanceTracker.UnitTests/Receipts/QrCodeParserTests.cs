using FinanceTracker.Application.Features.Receipts;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.UnitTests.Receipts;

/// <summary>
/// QR validation before enqueue (T4.1.5): the parser accepts well-formed receipt
/// QR payloads and rejects anything missing a required field or malformed.
/// </summary>
public class QrCodeParserTests
{
    private const string ValidQr =
        "t=20200924T1837&s=349.93&fn=9282440300682838&i=46534&fp=1273019065&n=1";

    [Fact]
    public void TryParse_ValidQr_ReturnsParsedFields()
    {
        var ok = QrCodeParser.TryParse(ValidQr, out var data);

        Assert.True(ok);
        Assert.NotNull(data);
        Assert.Equal(349.93m, data!.Sum);
        Assert.Equal(34993, data.SumInKopecks);
        Assert.Equal("9282440300682838", data.FiscalDriveNumber);
        Assert.Equal(46534, data.FiscalDocumentNumber);
        Assert.Equal("1273019065", data.FiscalSign);
        Assert.Equal(1, data.OperationType);
        Assert.Equal(AccrualType.Income, data.AccrualType);
        Assert.Equal(new DateTimeOffset(2020, 9, 24, 18, 37, 0, TimeSpan.Zero), data.Timestamp);
        Assert.Equal(ValidQr, data.Raw);
    }

    [Fact]
    public void TryParse_TimestampWithSeconds_IsAccepted()
    {
        var ok = QrCodeParser.TryParse(
            "t=20200924T183745&s=10.00&fn=1&i=2&fp=3&n=3", out var data);

        Assert.True(ok);
        Assert.Equal(45, data!.Timestamp.Second);
        Assert.Equal(AccrualType.Expense, data.AccrualType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("s=349.93&fn=9282440300682838&i=46534&fp=1273019065&n=1")]   // missing t
    [InlineData("t=20200924T1837&fn=9282440300682838&i=46534&fp=1273019065&n=1")] // missing s
    [InlineData("t=20200924T1837&s=349.93&i=46534&fp=1273019065&n=1")]       // missing fn
    [InlineData("t=20200924T1837&s=349.93&fn=928&fp=1273019065&n=1")]        // missing i
    [InlineData("t=20200924T1837&s=349.93&fn=928&i=46534&n=1")]              // missing fp
    [InlineData("t=20200924T1837&s=349.93&fn=928&i=46534&fp=127&")]          // missing n
    public void TryParse_MissingRequiredField_Fails(string? raw)
    {
        Assert.False(QrCodeParser.TryParse(raw, out var data));
        Assert.Null(data);
    }

    [Theory]
    [InlineData("t=NOTADATE&s=349.93&fn=928&i=46534&fp=127&n=1")]   // bad timestamp
    [InlineData("t=20200924T1837&s=abc&fn=928&i=46534&fp=127&n=1")] // bad sum
    [InlineData("t=20200924T1837&s=349.93&fn=92A&i=46534&fp=127&n=1")] // non-digit fn
    [InlineData("t=20200924T1837&s=349.93&fn=928&i=0&fp=127&n=1")]   // fd must be > 0
    [InlineData("t=20200924T1837&s=349.93&fn=928&i=46534&fp=127&n=5")] // operation type out of range
    [InlineData("t=20200924T1837&s=349.93&fn=928&i=46534&fp=127&n=0")] // operation type out of range
    public void TryParse_MalformedField_Fails(string raw)
    {
        Assert.False(QrCodeParser.TryParse(raw, out _));
    }

    [Fact]
    public void Parse_InvalidQr_Throws()
    {
        Assert.Throws<FormatException>(() => QrCodeParser.Parse("not a qr"));
    }
}
