using FluentAssertions;
using MyToDo.App.Services;
using Xunit;

namespace MyToDo.Tests.Services;

public sealed class DesktopActivationStateTests
{
    [Fact]
    public void Already_foreground_activation_restores_only_when_the_scheduled_callback_runs()
    {
        var state = new DesktopActivationState();
        Action? scheduled = null;
        var restoreCalls = 0;
        state.MarkDetached();

        state.ScheduleRestore(callback => scheduled = callback, () => restoreCalls++);

        restoreCalls.Should().Be(0);
        state.IsRestorePending.Should().BeTrue();
        scheduled.Should().NotBeNull();

        scheduled!();

        restoreCalls.Should().Be(1);
        state.IsRestorePending.Should().BeFalse();
    }

    [Fact]
    public void Deactivation_and_scheduled_restore_cannot_restore_twice()
    {
        var state = new DesktopActivationState();
        Action? scheduled = null;
        var restoreCalls = 0;
        state.MarkDetached();
        state.ScheduleRestore(callback => scheduled = callback, () => restoreCalls++);

        state.Restore(() => restoreCalls++);
        scheduled!();

        restoreCalls.Should().Be(1);
        state.IsRestorePending.Should().BeFalse();
    }
}
