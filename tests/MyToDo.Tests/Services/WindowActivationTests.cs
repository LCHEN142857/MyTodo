using FluentAssertions;
using MyToDo.App.Services;
using Xunit;

namespace MyToDo.Tests.Services;

public sealed class WindowActivationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Activation_preserves_the_saved_topmost_state(bool initialTopmost)
    {
        var observed = new List<bool>();

        WindowActivation.Activate(initialTopmost, observed.Add, () => { });

        observed.Should().Equal(initialTopmost ? [] : [true, false]);
    }

    [Fact]
    public void Non_topmost_activation_preserves_the_saved_pin_state_after_foregrounding()
    {
        var events = new List<string>();

        WindowActivation.Activate(false, value => events.Add($"topmost:{value}"), () => events.Add("activate"));

        events.Should().Equal("topmost:True", "activate", "topmost:False");
    }
}
