namespace FinanceTracker.Domain.Entities;

/// <summary>
/// A spending/income category. System categories (<see cref="IsSystem"/> = true,
/// <see cref="UserId"/> = null) are shared with everyone and are read-only for
/// regular users; user categories belong to a single owner.
/// </summary>
public class Category
{
    public Guid Id { get; private set; }

    /// <summary>Owner; <c>null</c> for a shared system category.</summary>
    public Guid? UserId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Lucide icon code (e.g. <c>shopping-cart</c>).</summary>
    public string Icon { get; private set; }

    /// <summary>HEX colour, e.g. <c>#22c55e</c>.</summary>
    public string Color { get; private set; }

    public bool IsSystem { get; private set; }

    private Category()
    {
        Name = Icon = Color = string.Empty;
    }

    /// <summary>Creates a user-owned category with an app-generated GUID v7 id.</summary>
    public Category(Guid userId, string name, string icon, string color)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Name = name;
        Icon = icon;
        Color = color;
        IsSystem = false;
    }

    /// <summary>Factory for the seeded system categories (fixed ids, no owner).</summary>
    public static Category CreateSystem(Guid id, string name, string icon, string color) =>
        new()
        {
            Id = id,
            UserId = null,
            Name = name,
            Icon = icon,
            Color = color,
            IsSystem = true,
        };

    public void Update(string name, string icon, string color)
    {
        Name = name;
        Icon = icon;
        Color = color;
    }
}
