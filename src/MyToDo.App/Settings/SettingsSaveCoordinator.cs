namespace MyToDo.App.Settings;

public sealed class SettingsSaveCoordinator(ISettingsStore store)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await store.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
