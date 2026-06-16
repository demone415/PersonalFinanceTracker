using FinanceTracker.Application.Features.Receipts;

namespace FinanceTracker.UnitTests.Receipts;

/// <summary>
/// Fair cross-user ordering for the single-worker queue (T4.2.2): users are
/// interleaved one-per-pass so a burst from one user can't starve the others.
/// </summary>
public class RoundRobinReceiptOrderingTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-0000000000c3");

    private sealed record Item(Guid User, int Seq);

    [Fact]
    public void Interleave_AlternatesUsers_PreservingPerUserOrder()
    {
        // Arrival order: A,A,A,B,B,C — i.e. user A submitted a burst first.
        var items = new[]
        {
            new Item(A, 1), new Item(A, 2), new Item(A, 3),
            new Item(B, 1), new Item(B, 2),
            new Item(C, 1),
        };

        var ordered = RoundRobinReceiptOrdering.Interleave(items, i => i.User);

        // Pass 1: A,B,C · Pass 2: A,B · Pass 3: A
        Assert.Equal(
            new[]
            {
                new Item(A, 1), new Item(B, 1), new Item(C, 1),
                new Item(A, 2), new Item(B, 2),
                new Item(A, 3),
            },
            ordered);
    }

    [Fact]
    public void Interleave_KeepsEachUsersRelativeOrder()
    {
        var items = new[] { new Item(A, 1), new Item(A, 2), new Item(A, 3) };

        var ordered = RoundRobinReceiptOrdering.Interleave(items, i => i.User);

        Assert.Equal(new[] { 1, 2, 3 }, ordered.Select(i => i.Seq));
    }

    [Fact]
    public void Interleave_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(RoundRobinReceiptOrdering.Interleave(Array.Empty<Item>(), i => i.User));
    }
}
