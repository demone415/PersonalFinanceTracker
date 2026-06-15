namespace FinanceTracker.Application.Features.Categories;

/// <summary>Category as returned to clients. <see cref="IsSystem"/> categories are read-only for users.</summary>
public sealed record CategoryDto(Guid Id, string Name, string Icon, string Color, bool IsSystem);

/// <summary>Payload for creating a user category.</summary>
public sealed record CreateCategoryRequest(string Name, string Icon, string Color);

/// <summary>Payload for updating a user category.</summary>
public sealed record UpdateCategoryRequest(string Name, string Icon, string Color);
