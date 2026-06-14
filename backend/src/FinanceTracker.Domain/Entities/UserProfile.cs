namespace FinanceTracker.Domain.Entities;

/// <summary>
/// Application-level profile data for a GoTrue user (preferences, currency).
/// Its identity <b>is</b> the user: <see cref="Id"/> equals <c>auth.users.id</c>
/// (FK), so a profile is inherently isolated — a caller reads their own profile
/// by their own id, no separate UserId filter needed (ARCHITECTURE.md §1.2 auth).
/// </summary>
public class UserProfile
{
    /// <summary>Primary key, equal to the GoTrue <c>auth.users.id</c>.</summary>
    public Guid Id { get; private set; }

    public string? DisplayName { get; private set; }

    /// <summary>ISO-4217 currency code for display/aggregation; defaults to RUB.</summary>
    public string Currency { get; private set; } = "RUB";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private UserProfile()
    {
    }

    public UserProfile(Guid id, string? displayName = null, string currency = "RUB")
    {
        Id = id;
        DisplayName = displayName;
        Currency = currency;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void Update(string? displayName, string currency)
    {
        DisplayName = displayName;
        Currency = currency;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
