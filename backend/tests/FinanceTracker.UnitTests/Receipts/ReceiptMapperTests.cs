using FinanceTracker.Application.Features.Receipts;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.UnitTests.Receipts;

/// <summary>
/// Manual mapping of a fetched receipt onto the domain entity (T4.1.4): fields,
/// item graph, and the transition to <see cref="ReceiptFetchStatus.Fetched"/>.
/// </summary>
public class ReceiptMapperTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static Receipt NewPendingReceipt() =>
        Receipt.CreateForQrScan(UserId, amountInKopecks: 34993,
            date: new DateTimeOffset(2020, 9, 24, 18, 37, 0, TimeSpan.Zero),
            qrRaw: "t=20200924T1837&s=349.93&fn=9282440300682838&i=46534&fp=1273019065&n=1",
            fd: 46534, fn: "9282440300682838", fpd: "1273019065");

    private static ReceiptData SampleData() => new(
        Organization: "ООО Ромашка",
        Address: "Москва, ул. Ленина, 1",
        Inn: "7700000000",
        Cashier: "Иванов И.И.",
        ShiftNumber: 7,
        ExternalNumber: "123",
        TotalSumInKopecks: 34993,
        TaxationType: TaxationType.Usn,
        FiscalDocumentNumber: 46534,
        FiscalDriveNumber: "9282440300682838",
        FiscalSign: "1273019065",
        Items:
        [
            new ReceiptItemData("Хлеб", Price: 35.50m, Quantity: 2m, Sum: 71.00m),
            new ReceiptItemData("Молоко", Price: 89.93m, Quantity: 1m, Sum: 89.93m),
        ]);

    [Fact]
    public void Apply_PopulatesFieldsAndMarksFetched()
    {
        var receipt = NewPendingReceipt();

        ReceiptMapper.Apply(receipt, SampleData(), rawJson: "{\"json\":true}");

        Assert.Equal(ReceiptFetchStatus.Fetched, receipt.FetchStatus);
        Assert.Equal("ООО Ромашка", receipt.Organization);
        Assert.Equal("Москва, ул. Ленина, 1", receipt.Address);
        Assert.Equal("7700000000", receipt.INN);
        Assert.Equal("Иванов И.И.", receipt.Cashier);
        Assert.Equal(7, receipt.ShiftNumber);
        Assert.Equal("123", receipt.ExternalNumber);
        Assert.Equal(TaxationType.Usn, receipt.TaxationType);
        Assert.Equal("{\"json\":true}", receipt.RawMetadata);
    }

    [Fact]
    public void Apply_MapsItemsWithReceiptId()
    {
        var receipt = NewPendingReceipt();

        ReceiptMapper.Apply(receipt, SampleData(), rawJson: null);

        Assert.Equal(2, receipt.Items.Count);
        var bread = receipt.Items.First();
        Assert.Equal("Хлеб", bread.Name);
        Assert.Equal(35.50m, bread.Price);
        Assert.Equal(2m, bread.Quantity);
        Assert.Equal(71.00m, bread.Sum);
        Assert.All(receipt.Items, i => Assert.Equal(receipt.Id, i.ReceiptId));
    }

    [Fact]
    public void Apply_IsIdempotentOnItems_DoesNotDuplicate()
    {
        var receipt = NewPendingReceipt();

        ReceiptMapper.Apply(receipt, SampleData(), rawJson: null);
        ReceiptMapper.Apply(receipt, SampleData(), rawJson: null);

        Assert.Equal(2, receipt.Items.Count);
    }
}
