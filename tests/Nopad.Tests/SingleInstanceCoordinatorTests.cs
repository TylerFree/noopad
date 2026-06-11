using Noopad.Services;

namespace Nopad.Tests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void TryBecomePrimary_WhenNoInstanceIsRunning_AllowsNewPrimary()
    {
        using var coordinator = new SingleInstanceCoordinator(CreateInstanceName());

        Assert.True(coordinator.TryBecomePrimary());
    }

    [Fact]
    public async Task RunningInstance_RejectsSecondPrimary_AndReceivesLaunchRequest()
    {
        var instanceName = CreateInstanceName();
        using var primary = new SingleInstanceCoordinator(instanceName);
        var received = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestedPath = Path.Combine(Path.GetTempPath(), "noopad-test-file.txt");

        Assert.True(primary.TryBecomePrimary());
        primary.StartServer(args =>
        {
            received.TrySetResult(args);
            return Task.CompletedTask;
        });

        using var secondary = new SingleInstanceCoordinator(instanceName);
        Assert.False(secondary.TryBecomePrimary());
        Assert.True(await secondary.TrySendLaunchRequestAsync([requestedPath], TimeSpan.FromSeconds(5)));

        var args = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([requestedPath], args);
    }

    private static string CreateInstanceName()
    {
        return $"noopad-test-{Guid.NewGuid():N}";
    }
}
