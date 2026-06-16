using System.Text.Json;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Features.Receipts;
using FinanceTracker.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;

/// <summary>
/// <see cref="IReceiptProvider"/> implementation over the ПроверкаЧека Refit
/// client. Sends request method 2 (raw QR), classifies the response <c>code</c>
/// into a <see cref="ReceiptFetchOutcome"/>, and maps <c>data.json</c> to a
/// provider-agnostic <see cref="ReceiptData"/> (manual mapping, T4.1.4).
/// Transport resilience (retry + circuit breaker) is applied to the HttpClient.
/// </summary>
internal sealed class ProverkaCheckaProvider(
    IProverkaCheckaApi api,
    IOptions<ProverkaCheckaOptions> options,
    ILogger<ProverkaCheckaProvider> logger) : IReceiptProvider
{
    private readonly ProverkaCheckaOptions _options = options.Value;

    public async Task<ReceiptFetchResult> GetReceiptAsync(string qrRaw, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new InvalidOperationException(
                "ReceiptProvider:Token is not configured; cannot call ПроверкаЧека.");
        }

        var form = new Dictionary<string, object>
        {
            ["token"] = _options.Token,
            ["qrraw"] = qrRaw,
        };

        var response = await api.GetCheckAsync(form, ct);
        var rawJson = response.Data?.Json is { } ? JsonSerializer.Serialize(response.Data.Json) : null;

        var outcome = MapCode(response.Code);
        if (outcome != ReceiptFetchOutcome.Success)
        {
            logger.LogInformation("ProverkaCheka returned code {Code} ({Outcome}).", response.Code, outcome);
            return ReceiptFetchResult.Failed(outcome, rawJson);
        }

        if (response.Data?.Json is not { } json)
        {
            // code 1 but no body — treat as a provider error so it is retried.
            logger.LogWarning("ProverkaCheka returned success code with no data.json payload.");
            return ReceiptFetchResult.Failed(ReceiptFetchOutcome.ProviderError, rawJson);
        }

        return ReceiptFetchResult.Successful(MapData(json), rawJson);
    }

    private static ReceiptFetchOutcome MapCode(int code) => code switch
    {
        0 => ReceiptFetchOutcome.Invalid,
        1 => ReceiptFetchOutcome.Success,
        2 => ReceiptFetchOutcome.NotYetAvailable,
        3 => ReceiptFetchOutcome.RetryLimitReached,
        4 => ReceiptFetchOutcome.RetryTooSoon,
        _ => ReceiptFetchOutcome.ProviderError, // 5 and any unexpected code
    };

    private static ReceiptData MapData(ProverkaCheckaJson json)
    {
        var items = (json.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => new ReceiptItemData(
                Name: i.Name!.Trim(),
                Price: KopecksToRubles(i.Price),
                Quantity: i.Quantity,
                Sum: KopecksToRubles(i.Sum)))
            .ToList();

        return new ReceiptData(
            Organization: json.User,
            Address: json.Address,
            Inn: json.UserInn,
            Cashier: json.Operator,
            ShiftNumber: json.ShiftNumber,
            ExternalNumber: json.RequestNumber?.ToString(),
            TotalSumInKopecks: json.TotalSum ?? 0,
            TaxationType: MapTaxationType(json.TaxationType),
            FiscalDocumentNumber: json.FiscalDocumentNumber,
            FiscalDriveNumber: json.FiscalDriveNumber,
            FiscalSign: json.FiscalSign,
            Items: items);
    }

    private static decimal KopecksToRubles(long kopecks) => kopecks / 100m;

    private static TaxationType? MapTaxationType(int? code) =>
        code is not null && Enum.IsDefined(typeof(TaxationType), code.Value)
            ? (TaxationType)code.Value
            : null;
}
