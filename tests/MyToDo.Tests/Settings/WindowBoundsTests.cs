using FluentAssertions;
using MyToDo.App.Services;
using MyToDo.App.Settings;
using Xunit;

namespace MyToDo.Tests.Settings;

public sealed class WindowBoundsTests
{
    [Fact]
    public void Pixel_display_bounds_are_converted_to_dips_before_visibility_checks()
    {
        var result = DisplayBounds.FromPixels(1920, 0, 1920, 1080, 1.5, 1.5, isPrimary: true);

        result.Left.Should().BeApproximately(1280, 0.001);
        result.Top.Should().Be(0);
        result.Width.Should().BeApproximately(1280, 0.001);
        result.Height.Should().BeApproximately(720, 0.001);
        result.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void Virtual_display_offset_and_size_use_one_consistent_dpi_transform()
    {
        var result = DisplayBounds.FromPixels(1920, 0, 2560, 1440, 1.5, 1.5);

        result.Left.Should().BeApproximately(1280, 0.001);
        result.Width.Should().BeApproximately(1706.667, 0.001);
    }

    [Fact]
    public void Offscreen_bounds_are_moved_inside_primary_work_area()
    {
        var saved = AppSettings.Default with { Left = 9000, Top = 9000 };
        var result = WindowBounds.EnsureVisible(saved, [new DisplayBounds(0, 0, 1920, 1040)]);

        result.Left.Should().BeInRange(0, 1660);
        result.Top.Should().BeInRange(0, 720);
    }

    [Fact]
    public void Offscreen_bounds_use_the_primary_work_area_when_it_is_not_first()
    {
        var saved = AppSettings.Default with { Left = -9000, Top = -9000 };
        var result = WindowBounds.EnsureVisible(saved,
        [
            new DisplayBounds(1920, 0, 1920, 1040),
            new DisplayBounds(0, 0, 1920, 1040, IsPrimary: true)
        ]);

        result.Left.Should().BeInRange(0, 1660);
        result.Top.Should().BeInRange(0, 720);
    }
}
