using FluentAssertions;
using MyToDo.App.Services;
using MyToDo.App.Settings;
using Xunit;

namespace MyToDo.Tests.Settings;

public sealed class WindowBoundsTests
{
    [Fact]
    public void Offscreen_bounds_are_moved_inside_primary_work_area()
    {
        var saved = AppSettings.Default with { Left = 9000, Top = 9000 };
        var result = WindowBounds.EnsureVisible(saved, [new DisplayBounds(0, 0, 1920, 1040)]);

        result.Left.Should().BeInRange(0, 1660);
        result.Top.Should().BeInRange(0, 720);
    }
}
