using FinanceTracker.Application.Features.Accruals;
using FinanceTracker.Application.Features.Budgets;
using FinanceTracker.Application.Features.Categories;
using FinanceTracker.Application.Features.Dashboard;
using FinanceTracker.Application.Features.Receipts;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinanceTracker.Application;

/// <summary>Composition root for the Application layer: feature services and validators.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateCategoryRequestValidator>();

        services.AddScoped<CategoryService>();
        services.AddScoped<BudgetService>();
        services.AddScoped<AccrualService>();
        services.AddScoped<DashboardService>();

        // Receipt fetching (Story 4.2): the QR-scan producer and the background
        // processor that turns a provider outcome into a receipt state transition.
        services.AddScoped<ReceiptScanService>();
        services.AddScoped<ReceiptFetchProcessor>();

        // System clock — overridden with a fake in unit tests for deterministic retries.
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
