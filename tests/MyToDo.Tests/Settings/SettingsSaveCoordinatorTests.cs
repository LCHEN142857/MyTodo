using FluentAssertions;
using MyToDo.App.Settings;
using Xunit;

namespace MyToDo.Tests.Settings;

public sealed class SettingsSaveCoordinatorTests
{
    [Fact]
    public async Task Later_save_runs_after_an_earlier_save_fails()
    {
        var store = new FailOnceSettingsStore();
        var coordinator = new SettingsSaveCoordinator(store);

        var first = () => coordinator.SaveAsync(AppSettings.Default);
        await first.Should().ThrowAsync<IOException>();

        var latest = AppSettings.Default with { Opacity = 0.55 };
        await coordinator.SaveAsync(latest);

        store.Saved.Should().Equal(latest);
    }

    [Fact]
    public async Task Repeated_saves_persist_each_current_snapshot()
    {
        var store = new RecordingSettingsStore();
        var coordinator = new SettingsSaveCoordinator(store);
        var first = AppSettings.Default with { Opacity = 0.75 };
        var second = AppSettings.Default with { Opacity = 0.45 };

        await coordinator.SaveAsync(first);
        await coordinator.SaveAsync(second);

        store.Saved.Should().Equal(first, second);
    }

    private sealed class FailOnceSettingsStore : ISettingsStore
    {
        private bool _hasFailed;
        public List<AppSettings> Saved { get; } = [];

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            if (!_hasFailed)
            {
                _hasFailed = true;
                return Task.FromException(new IOException("Disk unavailable."));
            }

            Saved.Add(settings);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSettingsStore : ISettingsStore
    {
        public List<AppSettings> Saved { get; } = [];
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Saved.Add(settings);
            return Task.CompletedTask;
        }
    }
}
