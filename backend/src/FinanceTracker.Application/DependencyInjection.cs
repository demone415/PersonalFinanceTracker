using FinanceTracker.Application.Features.Categories;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application;

/// <summary>Composition root for the Application layer: feature services and validators.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateCategoryRequestValidator>();

        services.AddScoped<CategoryService>();

        return services;
    }
}
