using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Application.Features.Categories;

/// <summary>
/// Feature service for categories (T1.3.2). Data isolation is enforced by the
/// context query filter (callers see their own categories plus shared system
/// ones); this service additionally guards mutations so regular users cannot
/// modify system categories.
/// </summary>
public sealed class CategoryService(
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Categories
            .OrderByDescending(c => c.IsSystem)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Icon, c.Color, c.IsSystem))
            .ToListAsync(cancellationToken);

    public async Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Categories
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Icon, c.Color, c.IsSystem))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Category), id);

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Authentication is required to create a category.");

        var category = new Category(userId, request.Name, request.Icon, request.Color);
        db.Categories.Add(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await FindOwnedAsync(id, cancellationToken);
        category.Update(request.Name, request.Icon, request.Color);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(category);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await FindOwnedAsync(id, cancellationToken);
        db.Categories.Remove(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Loads a category the caller may mutate; system categories are read-only for non-admins.</summary>
    private async Task<Category> FindOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), id);

        if (category.IsSystem && !currentUser.IsAdmin)
        {
            throw new ForbiddenAccessException("System categories cannot be modified.");
        }

        return category;
    }

    private static CategoryDto ToDto(Category c) => new(c.Id, c.Name, c.Icon, c.Color, c.IsSystem);
}
