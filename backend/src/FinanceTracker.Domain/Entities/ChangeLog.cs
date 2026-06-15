using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Entities;

public class ChangeLog : IUserOwnedEntity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string EntityType { get; private set; }
    public Guid EntityId { get; private set; }
    public string Action { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string? ValuesBefore { get; private set; }
    public string? ValuesAfter { get; private set; }

    private ChangeLog() { EntityType = Action = string.Empty; }

    public ChangeLog(
        Guid userId,
        string entityType,
        Guid entityId,
        string action,
        string? valuesBefore,
        string? valuesAfter)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        Timestamp = DateTimeOffset.UtcNow;
        ValuesBefore = valuesBefore;
        ValuesAfter = valuesAfter;
    }
}
