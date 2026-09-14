using System.Windows.Input;
using MyToDo.App.Data;
using MyToDo.App.Domain;
using MyToDo.App.Infrastructure;

namespace MyToDo.App.ViewModels;

public sealed class TodoItemViewModel : ObservableObject
{
    private readonly ITodoRepository _repository;
    private readonly Func<TodoItemViewModel, TodoStatus, Task>? _statusChanged;
    private readonly Action<string>? _errorReported;
    private TodoItem _item;
    private string _content;
    private string _editText;
    private bool _isEditing;
    private string? _errorMessage;

    public TodoItemViewModel(TodoItem item, ITodoRepository repository)
        : this(item, repository, null, null)
    {
    }

    internal TodoItemViewModel(TodoItem item, ITodoRepository repository, Func<TodoItemViewModel, TodoStatus, Task>? statusChanged, Action<string>? errorReported)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _statusChanged = statusChanged;
        _errorReported = errorReported;
        _content = item.Content;
        _editText = item.Content;

        CompleteCommand = new AsyncRelayCommand(CompleteAsync);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        BeginEditCommand = new RelayCommand(BeginEdit);
        SaveEditCommand = new AsyncRelayCommand(SaveEditAsync);
        CancelEditCommand = new RelayCommand(CancelEdit);
    }

    public event EventHandler? RequestSelectAll;

    public long Id => _item.Id;
    public TodoStatus Status => _item.Status;
    public string Content
    {
        get => _content;
        private set => SetProperty(ref _content, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set => SetProperty(ref _isEditing, value);
    }

    public string EditText
    {
        get => _editText;
        set => SetProperty(ref _editText, value ?? string.Empty);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public AsyncRelayCommand CompleteCommand { get; }
    public AsyncRelayCommand RestoreCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand BeginEditCommand { get; }
    public AsyncRelayCommand SaveEditCommand { get; }
    public RelayCommand CancelEditCommand { get; }

    public void BeginEdit()
    {
        EditText = Content;
        IsEditing = true;
        RequestSelectAll?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveEditAsync()
    {
        var content = EditText.Trim();
        if (content.Length == 0)
        {
            CancelEdit();
            return;
        }

        try
        {
            await _repository.RenameAsync(Id, content);
            _item = _item with { Content = content, UpdatedAtUtc = DateTimeOffset.UtcNow };
            Content = content;
            EditText = content;
            IsEditing = false;
            ErrorMessage = null;
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }

    public void CancelEdit()
    {
        EditText = Content;
        IsEditing = false;
    }

    public async Task CompleteAsync() => await ChangeStatusAsync(TodoStatus.Completed, _repository.CompleteAsync);
    public async Task RestoreAsync() => await ChangeStatusAsync(TodoStatus.Todo, _repository.RestoreAsync);
    public async Task DeleteAsync() => await ChangeStatusAsync(TodoStatus.Deleted, _repository.DeleteAsync);

    private async Task ChangeStatusAsync(TodoStatus status, Func<long, CancellationToken, Task> operation)
    {
        try
        {
            await operation(Id, CancellationToken.None);
            _item = _item with { Status = status };
            OnPropertyChanged(nameof(Status));
            ErrorMessage = null;
            if (_statusChanged is not null)
            {
                await _statusChanged(this, status);
            }
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }

    private void ReportError(Exception exception)
    {
        ErrorMessage = exception.Message;
        _errorReported?.Invoke(exception.Message);
    }
}
