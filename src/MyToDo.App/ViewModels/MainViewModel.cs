using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using MyToDo.App.Data;
using MyToDo.App.Domain;
using MyToDo.App.Infrastructure;

namespace MyToDo.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ITodoRepository _repository;
    private readonly ObservableCollection<TodoItemViewModel> _todoItems = [];
    private readonly ObservableCollection<TodoItemViewModel> _historyItems = [];
    private readonly ObservableCollection<TodoItemViewModel> _visibleItems = [];
    private readonly ReadOnlyObservableCollection<TodoItemViewModel> _readonlyVisibleItems;
    private readonly ICollectionView _visibleItemsView;
    private AppPage _currentPage = AppPage.ToDo;
    private string _newTodoText = string.Empty;
    private string _searchText = string.Empty;
    private string? _errorMessage;

    private MainViewModel(ITodoRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _readonlyVisibleItems = new ReadOnlyObservableCollection<TodoItemViewModel>(_visibleItems);
        _visibleItemsView = CollectionViewSource.GetDefaultView(_visibleItems);

        CreateCommand = new AsyncRelayCommand(CreateAsync);
        ShowTodoCommand = new AsyncRelayCommand(ShowTodoAsync);
        ShowHistoryCommand = new AsyncRelayCommand(ShowHistoryAsync);
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync);
    }

    public static async Task<MainViewModel> CreateAsync(ITodoRepository repository)
    {
        var viewModel = new MainViewModel(repository);
        await viewModel.LoadAsync();
        return viewModel;
    }

    public AppPage CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string NewTodoText
    {
        get => _newTodoText;
        set => SetProperty(ref _newTodoText, value ?? string.Empty);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                RefreshVisibleItems();
            }
        }
    }

    public int TodoCount => _todoItems.Count;
    public int HistoryCount => _historyItems.Count;
    public ReadOnlyObservableCollection<TodoItemViewModel> VisibleItems => _readonlyVisibleItems;
    public ICollectionView VisibleItemsView => _visibleItemsView;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public AsyncRelayCommand CreateCommand { get; }
    public AsyncRelayCommand ShowTodoCommand { get; }
    public AsyncRelayCommand ShowHistoryCommand { get; }
    public AsyncRelayCommand ClearHistoryCommand { get; }

    public async Task CreateAsync()
    {
        var content = NewTodoText.Trim();
        if (content.Length == 0)
        {
            return;
        }

        try
        {
            var created = await _repository.CreateAsync(Environment.MachineName, Environment.UserName, content);
            _todoItems.Insert(0, CreateRow(created));
            NewTodoText = string.Empty;
            ErrorMessage = null;
            OnPropertyChanged(nameof(TodoCount));
            RefreshVisibleItems();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    public Task ShowTodoAsync() => ShowPageAsync(AppPage.ToDo);
    public Task ShowHistoryAsync() => ShowPageAsync(AppPage.History);

    public async Task ClearHistoryAsync()
    {
        try
        {
            await _repository.ClearCompletedAsync();
            _historyItems.Clear();
            ErrorMessage = null;
            OnPropertyChanged(nameof(HistoryCount));
            RefreshVisibleItems();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var todos = await _repository.QueryAsync(TodoStatus.Todo);
            var history = await _repository.QueryAsync(TodoStatus.Completed);
            foreach (var todo in todos)
            {
                _todoItems.Add(CreateRow(todo));
            }

            foreach (var completed in history)
            {
                _historyItems.Add(CreateRow(completed));
            }

            OnPropertyChanged(nameof(TodoCount));
            OnPropertyChanged(nameof(HistoryCount));
            RefreshVisibleItems();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task ShowPageAsync(AppPage page)
    {
        var status = page == AppPage.ToDo ? TodoStatus.Todo : TodoStatus.Completed;
        try
        {
            var items = await _repository.QueryAsync(status);
            var target = page == AppPage.ToDo ? _todoItems : _historyItems;
            target.Clear();
            foreach (var item in items)
            {
                target.Add(CreateRow(item));
            }

            CurrentPage = page;
            ErrorMessage = null;
            OnPropertyChanged(nameof(TodoCount));
            OnPropertyChanged(nameof(HistoryCount));
            RefreshVisibleItems();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    private TodoItemViewModel CreateRow(TodoItem item) => new(item, _repository, HandleStatusChangeAsync, ReportError);

    private Task HandleStatusChangeAsync(TodoItemViewModel item, TodoStatus status)
    {
        _todoItems.Remove(item);
        _historyItems.Remove(item);
        if (status == TodoStatus.Todo)
        {
            _todoItems.Insert(0, item);
        }
        else if (status == TodoStatus.Completed)
        {
            _historyItems.Insert(0, item);
        }

        OnPropertyChanged(nameof(TodoCount));
        OnPropertyChanged(nameof(HistoryCount));
        RefreshVisibleItems();
        return Task.CompletedTask;
    }

    private void RefreshVisibleItems()
    {
        var source = CurrentPage == AppPage.ToDo ? _todoItems : _historyItems;
        var matches = source.Where(item => string.IsNullOrWhiteSpace(SearchText) || item.Content.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToArray();
        _visibleItems.Clear();
        foreach (var item in matches)
        {
            _visibleItems.Add(item);
        }

        _visibleItemsView.Refresh();
    }

    private void ReportError(string message) => ErrorMessage = message;
}
