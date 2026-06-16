using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;

/// <summary>
/// Wire model for the ПроверкаЧека <c>POST /api/v1/check/get</c> response
/// (see proverka_cheka_documentation_api.docx). Only the fields the app maps are
/// modelled; everything else (HTML render, request echo) is ignored.
/// </summary>
internal sealed class ProverkaCheckaResponse
{
    /// <summary>Result code: 0 invalid, 1 success, 2 not-yet, 3 retry-limit, 4 too-soon, 5 server error.</summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("data")]
    public ProverkaCheckaData? Data { get; init; }
}

internal sealed class ProverkaCheckaData
{
    [JsonPropertyName("json")]
    public ProverkaCheckaJson? Json { get; init; }
}

/// <summary>The fiscal document, under <c>data.json</c>. Sums are in kopecks.</summary>
internal sealed class ProverkaCheckaJson
{
    /// <summary>Selling organisation name.</summary>
    [JsonPropertyName("user")]
    public string? User { get; init; }

    /// <summary>Retail point address. (The doc abbreviates it <c>retailPlaceAddres</c>;
    /// the live API uses <c>retailPlaceAddress</c> — both are accepted.)</summary>
    [JsonPropertyName("retailPlaceAddress")]
    public string? RetailPlaceAddress { get; init; }

    [JsonPropertyName("retailPlaceAddres")]
    public string? RetailPlaceAddressLegacy { get; init; }

    [JsonPropertyName("userInn")]
    public string? UserInn { get; init; }

    [JsonPropertyName("shiftNumber")]
    public int? ShiftNumber { get; init; }

    /// <summary>Receipt number within the shift (the "№" field).</summary>
    [JsonPropertyName("requestNumber")]
    public int? RequestNumber { get; init; }

    [JsonPropertyName("operator")]
    public string? Operator { get; init; }

    [JsonPropertyName("totalSum")]
    public long? TotalSum { get; init; }

    /// <summary>Taxation type code: 1 ОСН, 2 УСН, 4 ЕНВД, 8 ЕСХН, 16 ПСН, 32 НПД.</summary>
    [JsonPropertyName("taxationType")]
    public int? TaxationType { get; init; }

    [JsonPropertyName("fiscalDriveNumber")]
    public string? FiscalDriveNumber { get; init; }

    [JsonPropertyName("fiscalDocumentNumber")]
    public long? FiscalDocumentNumber { get; init; }

    [JsonPropertyName("fiscalSign")]
    public string? FiscalSign { get; init; }

    [JsonPropertyName("items")]
    public IReadOnlyList<ProverkaCheckaItem>? Items { get; init; }

    /// <summary>Address as reported, tolerating either spelling.</summary>
    public string? Address => RetailPlaceAddress ?? RetailPlaceAddressLegacy;
}

internal sealed class ProverkaCheckaItem
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Unit price in kopecks.</summary>
    [JsonPropertyName("price")]
    public long Price { get; init; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; init; }

    /// <summary>Line total in kopecks.</summary>
    [JsonPropertyName("sum")]
    public long Sum { get; init; }
}
