using FluentAssertions;
using MyToDo.App.Services;
using Xunit;

namespace MyToDo.Tests.Services;

public sealed class ScrollbarVisibilityControllerTests
{
    [Fact]
    public void Scrollbar_hides_three_seconds_after_last_activity()
    {
        var clock = new ManualDelayScheduler();
        var controller = new ScrollbarVisibilityController(clock);

        controller.NotifyActivity();
        controller.IsVisible.Should().BeTrue();
        clock.Advance(TimeSpan.FromSeconds(2.9));
        controller.IsVisible.Should().BeTrue();
        clock.Advance(TimeSpan.FromSeconds(.1));
        controller.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void New_activity_restarts_the_hide_boundary()
    {
        var clock = new ManualDelayScheduler();
        var controller = new ScrollbarVisibilityController(clock);

        controller.NotifyActivity();
        clock.Advance(TimeSpan.FromSeconds(2));
        controller.NotifyActivity();
        clock.Advance(TimeSpan.FromSeconds(2.9));
        controller.IsVisible.Should().BeTrue();
        clock.Advance(TimeSpan.FromSeconds(.1));
        controller.IsVisible.Should().BeFalse();
    }

    private sealed class ManualDelayScheduler : IScrollActivityScheduler
    {
        private readonly List<(TimeSpan DueAt, Action Callback)> _callbacks = [];
        private TimeSpan _now;

        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            var entry = (_now + delay, callback);
            _callbacks.Add(entry);
            return new Cancellation(() => _callbacks.Remove(entry));
        }

        public void Advance(TimeSpan elapsed)
        {
            _now += elapsed;
            var due = _callbacks.Where(x => x.DueAt <= _now).ToArray();
            foreach (var entry in due) _callbacks.Remove(entry);
            foreach (var (_, callback) in due) callback();
        }

        private sealed class Cancellation(Action cancel) : IDisposable
        {
            public void Dispose() => cancel();
        }
    }
}
