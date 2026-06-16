namespace FinanceTracker.Application.Features.Receipts;

/// <summary>
/// Fair ordering of due receipts across users (T4.2.2). The global queue has a
/// single worker, so when several users have receipts waiting we interleave them
/// round-robin — one per user per pass — instead of letting whoever scanned a
/// burst first drain the whole quota. Pure and deterministic for unit testing.
/// </summary>
public static class RoundRobinReceiptOrdering
{
    /// <summary>
    /// Returns <paramref name="items"/> reordered so users alternate. The first
    /// appearance order of each user, and each user's internal order, are
    /// preserved (callers pre-sort by FIFO due-time before calling).
    /// </summary>
    public static IReadOnlyList<T> Interleave<T>(IEnumerable<T> items, Func<T, Guid> userSelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(userSelector);

        var buckets = new List<Queue<T>>();
        var indexByUser = new Dictionary<Guid, int>();

        foreach (var item in items)
        {
            var user = userSelector(item);
            if (!indexByUser.TryGetValue(user, out var bucket))
            {
                bucket = buckets.Count;
                indexByUser[user] = bucket;
                buckets.Add(new Queue<T>());
            }

            buckets[bucket].Enqueue(item);
        }

        var ordered = new List<T>();
        bool dequeuedAny;
        do
        {
            dequeuedAny = false;
            foreach (var bucket in buckets)
            {
                if (bucket.Count > 0)
                {
                    ordered.Add(bucket.Dequeue());
                    dequeuedAny = true;
                }
            }
        }
        while (dequeuedAny);

        return ordered;
    }
}
