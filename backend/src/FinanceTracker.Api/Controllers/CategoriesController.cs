using FinanceTracker.Application.Features.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

/// <summary>CRUD for categories (T1.3.2). All endpoints require authentication.</summary>
[ApiController]
[Authorize]
[Route("api/v1/categories")]
public sealed class CategoriesController : ControllerBase
{
    /// <summary>Lists the caller's categories plus the shared system categories.</summary>
    [HttpGet]
    public Task<IReadOnlyList<CategoryDto>> GetAll(
        [FromServices] CategoryService categories,
        CancellationToken cancellationToken) =>
        categories.GetAllAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<CategoryDto> GetById(
        Guid id,
        [FromServices] CategoryService categories,
        CancellationToken cancellationToken) =>
        categories.GetByIdAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromBody] CreateCategoryRequest request,
        [FromServices] CategoryService categories,
        CancellationToken cancellationToken)
    {
        var created = await categories.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<CategoryDto> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        [FromServices] CategoryService categories,
        CancellationToken cancellationToken) =>
        categories.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] CategoryService categories,
        CancellationToken cancellationToken)
    {
        await categories.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
