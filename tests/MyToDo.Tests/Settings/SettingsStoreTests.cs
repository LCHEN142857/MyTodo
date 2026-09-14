using FluentAssertions;
using MyToDo.App.Settings;
using Xunit;

namespace MyToDo.Tests.Settings;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task Missing_or_corrupt_settings_return_valid_defaults()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var store = new SettingsStore(folder.FullName);
            (await store.LoadAsync()).Should().Be(AppSettings.Default);
            await File.WriteAllTextAsync(store.FilePath, "not-json");
            (await store.LoadAsync()).Should().Be(AppSettings.Default);
        }
        finally { folder.Delete(true); }
    }

    [Theory]
    [InlineData(.1, .35)]
    [InlineData(2, 1)]
    public void Normalize_clamps_opacity(double input, double expected) =>
        (AppSettings.Default with { Opacity = input }).Normalize().Opacity.Should().Be(expected);

    [Fact]
    public async Task Save_and_load_normalizes_dimensions_and_uses_camel_case_json()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var store = new SettingsStore(folder.FullName);
            await store.SaveAsync(new AppSettings(-1, -2, 0, 0, .5, true));
            var json = await File.ReadAllTextAsync(store.FilePath);
            json.Should().Contain("\"left\"").And.NotContain("Left");
            (await store.LoadAsync()).Should().Be(new AppSettings(-1, -2, 260, 320, .5, true));
        }
        finally { folder.Delete(true); }
    }
}
