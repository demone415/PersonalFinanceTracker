using System.Net;
using System.Text;
using FinanceTracker.Application.Features.Receipts;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refit;

namespace FinanceTracker.UnitTests.Receipts;

/// <summary>
/// Provider behaviour (T4.1.1/T4.1.4): the ПроверкаЧека response <c>code</c> is
/// classified into a <see cref="ReceiptFetchOutcome"/> and <c>data.json</c> is
/// mapped to a <see cref="ReceiptData"/> with kopecks→rubles conversion.
/// </summary>
public class ProverkaCheckaProviderTests
{
    private const string SuccessBody = """
    {
      "code": 1,
      "data": {
        "json": {
          "user": "ООО Ромашка",
          "retailPlaceAddress": "Москва, ул. Ленина, 1",
          "userInn": "7700000000",
          "shiftNumber": 7,
          "requestNumber": 123,
          "operator": "Иванов И.И.",
          "totalSum": 34993,
          "taxationType": 2,
          "fiscalDriveNumber": "9282440300682838",
          "fiscalDocumentNumber": 46534,
          "fiscalSign": "1273019065",
          "items": [
            { "name": "Хлеб", "price": 3550, "quantity": 2, "sum": 7100 },
            { "name": "Молоко", "price": 8993, "quantity": 1, "sum": 8993 }
          ]
        }
      }
    }
    """;

    private static ProverkaCheckaProvider BuildProvider(HttpStatusCode status, string body)
    {
        var handler = new StubHttpMessageHandler(status, body);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://proverkacheka.com") };
        var api = RestService.For<IProverkaCheckaApi>(httpClient);
        var options = Options.Create(new ProverkaCheckaOptions { Token = "test-token" });
        return new ProverkaCheckaProvider(api, options, NullLogger<ProverkaCheckaProvider>.Instance);
    }

    [Fact]
    public async Task GetReceiptAsync_Code1_MapsToSuccessfulReceiptData()
    {
        var provider = BuildProvider(HttpStatusCode.OK, SuccessBody);

        var result = await provider.GetReceiptAsync("t=20200924T1837&s=349.93&fn=1&i=2&fp=3&n=1");

        Assert.True(result.IsSuccess);
        Assert.Equal(ReceiptFetchOutcome.Success, result.Outcome);
        var data = result.Data!;
        Assert.Equal("ООО Ромашка", data.Organization);
        Assert.Equal("Москва, ул. Ленина, 1", data.Address);
        Assert.Equal("7700000000", data.Inn);
        Assert.Equal(7, data.ShiftNumber);
        Assert.Equal("123", data.ExternalNumber);
        Assert.Equal(34993, data.TotalSumInKopecks);
        Assert.Equal(TaxationType.Usn, data.TaxationType);
        Assert.Equal(46534, data.FiscalDocumentNumber);

        Assert.Equal(2, data.Items.Count);
        Assert.Equal("Хлеб", data.Items[0].Name);
        Assert.Equal(35.50m, data.Items[0].Price);   // 3550 kopecks → rubles
        Assert.Equal(71.00m, data.Items[0].Sum);     // 7100 kopecks → rubles
    }

    [Fact]
    public async Task GetReceiptAsync_LegacyAddressSpelling_IsAccepted()
    {
        const string body = """
        { "code": 1, "data": { "json": { "user": "X", "retailPlaceAddres": "Старый адрес", "items": [] } } }
        """;
        var provider = BuildProvider(HttpStatusCode.OK, body);

        var result = await provider.GetReceiptAsync("t=1&s=1&fn=1&i=1&fp=1&n=1");

        Assert.True(result.IsSuccess);
        Assert.Equal("Старый адрес", result.Data!.Address);
    }

    [Theory]
    [InlineData(0, ReceiptFetchOutcome.Invalid)]
    [InlineData(2, ReceiptFetchOutcome.NotYetAvailable)]
    [InlineData(3, ReceiptFetchOutcome.RetryLimitReached)]
    [InlineData(4, ReceiptFetchOutcome.RetryTooSoon)]
    [InlineData(5, ReceiptFetchOutcome.ProviderError)]
    [InlineData(99, ReceiptFetchOutcome.ProviderError)]
    public async Task GetReceiptAsync_NonSuccessCode_MapsToOutcome(int code, ReceiptFetchOutcome expected)
    {
        var provider = BuildProvider(HttpStatusCode.OK, $$"""{ "code": {{code}} }""");

        var result = await provider.GetReceiptAsync("t=1&s=1&fn=1&i=1&fp=1&n=1");

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetReceiptAsync_Code1WithoutPayload_IsProviderError()
    {
        var provider = BuildProvider(HttpStatusCode.OK, """{ "code": 1 }""");

        var result = await provider.GetReceiptAsync("t=1&s=1&fn=1&i=1&fp=1&n=1");

        Assert.Equal(ReceiptFetchOutcome.ProviderError, result.Outcome);
    }

    [Fact]
    public async Task GetReceiptAsync_LooselyTypedScalars_AreParsed()
    {
        // The live provider flips id fields between string and number, quotes some
        // numbers, and may omit item scalars — none of which must break parsing.
        const string body = """
        {
          "code": 1,
          "data": {
            "json": {
              "user": "ООО Ромашка",
              "retailPlaceAddress": "Москва",
              "userInn": 7700000000,
              "totalSum": "34993",
              "fiscalDriveNumber": 9282440300682838,
              "fiscalDocumentNumber": "46534",
              "fiscalSign": 1273019065,
              "items": [
                { "name": "Хлеб", "sum": 7100 }
              ]
            }
          }
        }
        """;
        var provider = BuildProvider(HttpStatusCode.OK, body);

        var result = await provider.GetReceiptAsync("t=1&s=1&fn=1&i=1&fp=1&n=1");

        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.Equal("7700000000", data.Inn);
        Assert.Equal(34993, data.TotalSumInKopecks);
        Assert.Equal("9282440300682838", data.FiscalDriveNumber);
        Assert.Equal(46534, data.FiscalDocumentNumber);
        Assert.Equal("1273019065", data.FiscalSign);
        var item = Assert.Single(data.Items);
        Assert.Equal(0m, item.Price);     // absent → 0
        Assert.Equal(1m, item.Quantity);  // absent → 1
        Assert.Equal(71.00m, item.Sum);
    }

    [Fact]
    public async Task GetReceiptAsync_DataAsEmptyArray_DoesNotCrash()
    {
        // Some non-success responses send `data: []` instead of an object/null.
        var provider = BuildProvider(HttpStatusCode.OK, """{ "code": 2, "data": [] }""");

        var result = await provider.GetReceiptAsync("t=1&s=1&fn=1&i=1&fp=1&n=1");

        Assert.Equal(ReceiptFetchOutcome.NotYetAvailable, result.Outcome);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetReceiptAsync_MalformedBody_IsProviderError()
    {
        var provider = BuildProvider(HttpStatusCode.OK, "not json at all");

        var result = await provider.GetReceiptAsync("t=1&s=1&fn=1&i=1&fp=1&n=1");

        Assert.Equal(ReceiptFetchOutcome.ProviderError, result.Outcome);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetReceiptAsync_EmptyToken_Throws()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{ "code": 1 }""");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://proverkacheka.com") };
        var api = RestService.For<IProverkaCheckaApi>(httpClient);
        var provider = new ProverkaCheckaProvider(
            api, Options.Create(new ProverkaCheckaOptions { Token = "" }),
            NullLogger<ProverkaCheckaProvider>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetReceiptAsync("t=1&s=1&fn=1&i=1&fp=1&n=1"));
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
