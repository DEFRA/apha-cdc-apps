using CDC.Api.Application;
using FluentAssertions;

namespace CDC.Api.Tests.Application;

public class ConcurrentReadsTests
{
    private static readonly int[] ThreeItems = [1, 2, 3];

    [Fact]
    public async Task SelectAsync_ReturnsEachResultInTheOrderOfItsItem_WhenTheReadsFinishOutOfOrder()
    {
        int[] items = [1, 2, 3, 4, 5, 6, 7, 8];

        // The earlier items take longer, so they finish last.
        var results = await ConcurrentReads.SelectAsync(
            items,
            async (item, token) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds((items.Length - item) * 5), token);

                return item * 10;
            },
            CancellationToken.None);

        results.Should().Equal(10, 20, 30, 40, 50, 60, 70, 80);
    }

    [Fact]
    public async Task SelectAsync_ReadsSeveralItemsAtOnce_ButNeverMoreThanFour()
    {
        var running = 0;
        var mostRunning = 0;

        await ConcurrentReads.SelectAsync(
            Enumerable.Range(0, 16).ToList(),
            async (item, token) =>
            {
                var now = Interlocked.Increment(ref running);
                InterlockedMax(ref mostRunning, now);
                await Task.Delay(20, token);
                Interlocked.Decrement(ref running);

                return item;
            },
            CancellationToken.None);

        mostRunning.Should().BeInRange(2, 4);
    }

    [Fact]
    public async Task SelectAsync_ReturnsNothing_WhenThereAreNoItems()
    {
        var results = await ConcurrentReads.SelectAsync(
            Array.Empty<int>(),
            (item, token) => Task.FromResult(item),
            CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectAsync_Throws_WhenAReadFails()
    {
        var read = () => ConcurrentReads.SelectAsync(
            ThreeItems,
            (item, token) => item == 2 ? Task.FromException<int>(new InvalidOperationException("The read failed.")) : Task.FromResult(item),
            CancellationToken.None);

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("The read failed.");
    }

    [Fact]
    public async Task SelectAsync_Throws_WhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var read = () => ConcurrentReads.SelectAsync(
            ThreeItems,
            (item, token) => Task.FromResult(item),
            cancellation.Token);

        await read.Should().ThrowAsync<OperationCanceledException>();
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;

        do
        {
            current = Volatile.Read(ref target);
        }
        while (value > current && Interlocked.CompareExchange(ref target, value, current) != current);
    }
}
