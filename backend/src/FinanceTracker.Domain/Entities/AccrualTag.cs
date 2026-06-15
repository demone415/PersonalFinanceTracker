namespace FinanceTracker.Domain.Entities;

public class AccrualTag
{
    public Guid AccrualId { get; private set; }
    public string Tag { get; private set; }

    private AccrualTag() { Tag = string.Empty; }

    public AccrualTag(Guid accrualId, string tag)
    {
        AccrualId = accrualId;
        Tag = tag;
    }
}
