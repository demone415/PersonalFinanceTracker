using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Common;
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
        typeof(MonthlyBudget), // Подготовка.md: budget changes are logged too
        typeof(ChangeLog), // excluded below — prevents recursion
    ];

    /// <summary>
    /// Synthetic snapshot field that carries a receipt's line items folded into the
    /// linked accrual's entry, so the Журнал can show which positions were
    /// added/removed (e.g. when a QR receipt is fetched or an FNS receipt imported).
    /// </summary>
    private const string ItemsField = "Items";

    /// <summary>
    /// Snapshots serialize enums by their string name (e.g. <c>"Expense"</c>, not
    /// <c>3</c>) so the Журнал — and the seed data that mirrors this format — stays
    /// human-readable and resilient to enum re-numbering.
    /// </summary>
    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

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

        // In an HTTP request the caller is on the JWT. Background jobs (e.g. FNS
        // import) have no HTTP user, so fall back to the entity's own owner — every
        // tracked type is an IUserOwnedEntity — otherwise their changes would never
        // reach the per-user Журнал.
        var callerId = currentUser.UserId;

        // Receipt line-item add/remove deltas keyed by their receipt. ReceiptItem is
        // not a tracked type on its own — we fold its deltas into the owning accrual's
        // snapshot so a "receipt fetched/imported" entry shows the positions.
        var itemDeltas = CollectReceiptItemDeltas(eventData.Context);

        foreach (var entry in entries)
        {
            var userId = callerId ?? (entry.Entity as IUserOwnedEntity)?.UserId;
            if (userId is null) continue;

            var (action, before, after) = Snapshot(entry);

            if (entry.Entity is Accrual && ReceiptIdOf(entry) is Guid receiptId
                && itemDeltas.TryGetValue(receiptId, out var delta))
            {
                before = WithItems(before, delta.Removed);
                after = WithItems(after, delta.Added);
            }

            var log = new ChangeLog(
                userId.Value,
                entry.Entity.GetType().Name,
                GetEntityId(entry),
                action,
                before?.ToJsonString(),
                after?.ToJsonString());

            eventData.Context.Set<ChangeLog>().Add(log);
        }

        return new ValueTask<InterceptionResult<int>>(result);
    }

    private static (string action, JsonObject? before, JsonObject? after) Snapshot(EntityEntry entry)
    {
        return entry.State switch
        {
            EntityState.Added => ("Create", null, ToJson(entry.CurrentValues.ToObject())),
            EntityState.Deleted => ("Delete", ToJson(entry.OriginalValues.ToObject()), null),
            _ => ("Update", ToJson(entry.OriginalValues.ToObject()), ToJson(entry.CurrentValues.ToObject())),
        };
    }

    private static JsonObject? ToJson(object? obj) =>
        obj is null ? null : JsonSerializer.SerializeToNode(obj, SnapshotOptions)?.AsObject();

    /// <summary>
    /// Folds a receipt's added/removed line items into an accrual snapshot under
    /// <see cref="ItemsField"/>. <c>before.Items</c> holds the removed positions and
    /// <c>after.Items</c> the added ones, so the UI can render a +/- list (on a fresh
    /// fetch/import all positions land in <c>after</c>).
    /// </summary>
    private static JsonObject? WithItems(JsonObject? snapshot, IReadOnlyList<ItemLine> items)
    {
        if (items.Count == 0) return snapshot;
        snapshot ??= new JsonObject();

        var arr = new JsonArray();
        foreach (var it in items)
        {
            arr.Add(new JsonObject
            {
                ["Name"] = it.Name,
                ["Quantity"] = JsonValue.Create(it.Quantity),
                ["Sum"] = JsonValue.Create(it.Sum),
            });
        }

        snapshot[ItemsField] = arr;
        return snapshot;
    }

    private static Dictionary<Guid, ReceiptItemDelta> CollectReceiptItemDeltas(DbContext context)
    {
        var map = new Dictionary<Guid, ReceiptItemDelta>();

        foreach (var e in context.ChangeTracker.Entries<ReceiptItem>())
        {
            var added = e.State == EntityState.Added;
            var removed = e.State == EntityState.Deleted;
            if (!added && !removed) continue;

            var values = added ? e.CurrentValues : e.OriginalValues;
            var receiptId = (Guid)values[nameof(ReceiptItem.ReceiptId)]!;
            var line = new ItemLine(
                (string?)values[nameof(ReceiptItem.Name)] ?? string.Empty,
                (decimal)values[nameof(ReceiptItem.Quantity)]!,
                (decimal)values[nameof(ReceiptItem.Sum)]!);

            if (!map.TryGetValue(receiptId, out var delta))
                map[receiptId] = delta = new ReceiptItemDelta();
            (added ? delta.Added : delta.Removed).Add(line);
        }

        return map;
    }

    private static Guid? ReceiptIdOf(EntityEntry entry)
    {
        var values = entry.State == EntityState.Deleted ? entry.OriginalValues : entry.CurrentValues;
        return values[nameof(Accrual.ReceiptId)] as Guid?;
    }

    private static Guid GetEntityId(EntityEntry entry)
    {
        var keyValue = entry.Metadata.FindPrimaryKey()?.Properties
            .Select(p => entry.Property(p.Name).CurrentValue)
            .FirstOrDefault();
        return keyValue is Guid id ? id : Guid.Empty;
    }

    private readonly record struct ItemLine(string Name, decimal Quantity, decimal Sum);

    private sealed class ReceiptItemDelta
    {
        public List<ItemLine> Added { get; } = [];
        public List<ItemLine> Removed { get; } = [];
    }
}
