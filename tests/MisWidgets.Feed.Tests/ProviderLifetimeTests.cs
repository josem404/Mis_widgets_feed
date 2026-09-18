using MisWidgets.Feed.Core;

namespace MisWidgets.Feed.Tests;

public sealed class ProviderLifetimeTests
{
    [Fact]
    public async Task RequestStop_ReleasesWaitersAndIsIdempotent()
    {
        using var lifetime = new ProviderLifetime();
        Task waiter = Task.Run(lifetime.Wait);

        Assert.False(lifetime.IsStopRequested);

        lifetime.RequestStop();
        lifetime.RequestStop();

        await waiter.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(lifetime.IsStopRequested);
    }
}
