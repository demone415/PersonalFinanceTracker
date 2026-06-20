using FinanceTracker.Application.Features.Accruals;
using FinanceTracker.Application.Features.Budgets;
using FinanceTracker.Application.Features.Categories;
using FinanceTracker.Application.Features.ChangeLog;
using FinanceTracker.Application.Features.Dashboard;
using FinanceTracker.Application.Features.Export;
using FinanceTracker.Application.Features.Import;
using FinanceTracker.Application.Features.Jobs;
using FinanceTracker.Application.Features.Profile;
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
        services.AddScoped<ChangeLogService>();
        services.AddScoped<ProfileService>();

        // Receipt fetching (Story 4.2): the QR-scan producer and the background
        // processor that turns a provider outcome into a receipt state transition.
        services.AddScoped<ReceiptScanService>();
        services.AddScoped<ReceiptFetchProcessor>();

        // Async CSV export (Story 6.2): the enqueue producer, the off-request
        // processor, the read-side job service, and the RFC-4180 CSV writer.
        services.AddScoped<AccrualExportService>();
        services.AddScoped<AccrualExportProcessor>();
        services.AddScoped<BackgroundTaskService>();
        services.AddSingleton<IAccrualCsvExporter, CsvAccrualExporter>();

        // Async FNS import (Story 6.1): the enqueue producer and the off-request
        // processor that parses the .xlsx and creates accruals + receipts.
        services.AddScoped<AccrualImportService>();
        services.AddScoped<AccrualImportProcessor>();

        // System clock — overridden with a fake in unit tests for deterministic retries.
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
