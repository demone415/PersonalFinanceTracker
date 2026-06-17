using Refit;

namespace FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;

/// <summary>
/// Refit description of the ПроверкаЧека HTTP API. We use request method 2
/// (raw QR string): a form-url-encoded body carrying <c>token</c> and
/// <c>qrraw</c>. The base address and resilience pipeline are configured on the
/// typed <see cref="HttpClient"/> in <c>DependencyInjection</c>.
/// </summary>
internal interface IProverkaCheckaApi
{
    /// <summary>
    /// <c>POST /api/v1/check/get</c>. Returns the raw response body so the provider
    /// can parse it leniently and log it verbatim on a deserialization failure —
    /// the provider's JSON is loosely typed. Business outcomes are conveyed by the
    /// body's <c>code</c> field (even on HTTP 200); transport/5xx failures throw
    /// <see cref="ApiException"/> and are retried by the resilience handler.
    /// </summary>
    [Post("/api/v1/check/get")]
    Task<string> GetCheckAsync(
        [Body(BodySerializationMethod.UrlEncoded)] IDictionary<string, object> form,
        CancellationToken ct = default);
}
