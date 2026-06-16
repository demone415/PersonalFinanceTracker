using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;

/// <summary>
/// Binds the <c>ReceiptProvider</c> configuration section: the ПроверкаЧека base
/// URL and API token, plus the daily quota caps enforced by the rate limiter
/// (ARCHITECTURE.md §4 / §11.4). The token is a server-side secret (§11.10).
/// </summary>
public sealed class ProverkaCheckaOptions
{
    public const string SectionName = "ReceiptProvider";

    /// <summary>API base address, e.g. <c>https://proverkacheka.com</c>.</summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://proverkacheka.com";

    /// <summary>
    /// ПроверкаЧека API token (personal-cabinet key). Kept server-side only (§11.10).
    /// Not required to start the app (dev has none and the fetch queue is inactive
    /// until Story 4.2); the provider guards against an empty token at call time.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Shared provider quota for the whole system per UTC day (provider hard limit ≈ 15).</summary>
    [Range(1, 1000)]
    public int DailyGlobalLimit { get; set; } = 15;

    /// <summary>Per-user daily cap so one user cannot monopolise the shared quota.</summary>
    [Range(1, 1000)]
    public int PerUserDailyLimit { get; set; } = 5;

    /// <summary>Overall timeout for a single provider call (resilience pipeline outer timeout).</summary>
    [Range(1, 120)]
    public int RequestTimeoutSeconds { get; set; } = 30;
}
