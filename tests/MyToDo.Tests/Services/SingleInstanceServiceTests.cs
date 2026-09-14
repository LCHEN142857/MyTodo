using FluentAssertions;
using MyToDo.App.Services;
using Xunit;

namespace MyToDo.Tests.Services;

public sealed class SingleInstanceServiceTests
{
    [Fact]
    public async Task Second_instance_cannot_acquire_and_signal_completes_first_waiter()
    {
        var name = "MyToDo.Tests." + Guid.NewGuid().ToString("N");
        await using var first = new SingleInstanceService(name);
        await using var second = new SingleInstanceService(name);
        (await first.TryAcquireAsync()).Should().BeTrue();
        (await second.TryAcquireAsync()).Should().BeFalse();
        var waiting = first.WaitForActivationAsync(new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token);
        await second.SignalExistingAsync();
        (await waiting).Should().BeTrue();
    }

    [Fact]
    public async Task Acquisition_is_bounded_when_mutex_is_held()
    {
        var name = "MyToDo.Tests." + Guid.NewGuid().ToString("N");
        using var acquired = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = Task.Run(() =>
        {
            using var mutex = new Mutex(true, "Local\\" + name);
            acquired.Set();
            release.Wait();
        });
        acquired.Wait();
        try
        {
            await using var service = new SingleInstanceService(name);
            var started = DateTime.UtcNow;
            (await service.TryAcquireAsync()).Should().BeFalse();
            (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(3));
        }
        finally
        {
            release.Set();
            await holder;
        }
    }
}
