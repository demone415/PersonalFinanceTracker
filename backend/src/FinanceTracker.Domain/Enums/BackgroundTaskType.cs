namespace FinanceTracker.Domain.Enums;

/// <summary>
/// The kind of asynchronous import/export job tracked by a
/// <see cref="Entities.BackgroundTask"/> (ARCHITECTURE.md §BackgroundTask, Epic 6).
/// </summary>
public enum BackgroundTaskType
{
    /// <summary>Import of an FNS «Налоги ФЛ» receipts export — an Excel (.xlsx) workbook (Story 6.1).</summary>
    ImportFns = 0,

    /// <summary>Export of filtered accruals to CSV (Story 6.2).</summary>
    ExportCsv = 1,
}
