using System.Text.Json;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinanceTracker.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core interceptor that records create/update/delete events for
/// tracked entities into the <c>change_logs</c> table (T1.4.4).
/// Only logs entities listed in <see cref="TrackedTypes"/>.
/// </summary>
public sealed class ChangeLogInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> TrackedTypes =
    [
        typeof(Accrual),
        typeof(ChangeLog), // excluded below — prevents recursion
    ];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null) return new ValueTask<InterceptionResult<int>>(result);

        var entries = eventData.Context.ChangeTracker
            .Entries()
            .Where(e => TrackedTypes.Contains(e.Entity.GetType())
                        && e.Entity is not ChangeLog
                        && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0) return new ValueTask<InterceptionResult<int>>(result);

        var userId = currentUser.UserId;
        if (userId is null) return new ValueTask<InterceptionResult<int>>(result);

        foreach (var entry in entries)
        {
            var (action, before, after) = Snapshot(entry);
            var log = new ChangeLog(
                userId.Value,
                entry.Entity.GetType().Name,
                GetEntityId(entry),
                action,
                before,
                after);

            eventData.Context.Set<ChangeLog>().Add(log);
        }

        return new ValueTask<InterceptionResult<int>>(result);
    }

    private static (string action, string? before, string? after) Snapshot(EntityEntry entry)
    {
        return entry.State switch
        {
            EntityState.Added => ("Create", null, Serialize(entry.CurrentValues.ToObject())),
            EntityState.Deleted => ("Delete", Serialize(entry.OriginalValues.ToObject()), null),
            _ => ("Update", Serialize(GetOriginal(entry)), Serialize(entry.CurrentValues.ToObject())),
        };
    }

    private static object GetOriginal(EntityEntry entry)
    {
        var original = entry.OriginalValues.ToObject();
        return original;
    }

    private static Guid GetEntityId(EntityEntry entry)
    {
        var keyValue = entry.Metadata.FindPrimaryKey()?.Properties
            .Select(p => entry.Property(p.Name).CurrentValue)
            .FirstOrDefault();
        return keyValue is Guid id ? id : Guid.Empty;
    }

    private static string? Serialize(object? obj) =>
        obj is null ? null : JsonSerializer.Serialize(obj);
}
