using MyToDo.App.Domain;

namespace MyToDo.App.Data;

public interface ITodoRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TodoItem>> QueryAsync(TodoStatus status, CancellationToken cancellationToken = default);
    Task<TodoItem> CreateAsync(string deviceName, string windowsUsername, string content, CancellationToken cancellationToken = default);
    Task RenameAsync(long id, string content, CancellationToken cancellationToken = default);
    Task CompleteAsync(long id, CancellationToken cancellationToken = default);
    Task RestoreAsync(long id, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task ClearCompletedAsync(CancellationToken cancellationToken = default);
}
