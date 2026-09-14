using System.IO;
using System.Windows;
using MyToDo.App.Data;
using MyToDo.App.Services;
using MyToDo.App.Settings;
using MyToDo.App.ViewModels;

namespace MyToDo.App;

public partial class App : System.Windows.Application
{
    private ISingleInstanceService? _instance;
    private ISettingsStore? _settingsStore;
    private MainWindow? _window;
    private AppSettings _settings = AppSettings.Default;
    private ITodoRepository? _repository;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var appDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyToDo");
        var databasePath = Path.Combine(appDirectory, "mytodo.db");
        try
        {
            _instance = new SingleInstanceService("MyToDo");
            if (!await _instance.TryAcquireAsync())
            {
                await _instance.SignalExistingAsync();
                Shutdown();
                return;
            }

            _repository = new TodoRepository(databasePath);
            await _repository.InitializeAsync();
            _settingsStore = new SettingsStore(appDirectory);
            _settings = await _settingsStore.LoadAsync();
            var viewModel = await MainViewModel.CreateAsync(_repository);
            _window = new MainWindow(viewModel, _settings, _settingsStore, _repository, _instance);
            MainWindow = _window;
            _window.Show();
            _ = ListenForActivationAsync();
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show($"Unable to initialize the database at {databasePath}.\n\n{exception.Message}", "MyToDo", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task ListenForActivationAsync()
    {
        if (_instance is null || _window is null) return;
        while (_window.IsVisible && await _instance.WaitForActivationAsync(CancellationToken.None))
        {
            await Dispatcher.InvokeAsync(_window.ActivateFromSecondInstance);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_window is not null) _window.PersistSettingsAsync().GetAwaiter().GetResult();
        if (_instance is not null) _instance.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }
}
