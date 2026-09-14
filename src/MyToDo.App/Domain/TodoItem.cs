namespace MyToDo.App.Domain;

public enum TodoStatus
{
    Todo,
    Completed,
    Deleted
}

public sealed record TodoItem(
    long Id,
    string DeviceName,
    string WindowsUsername,
    string Content,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? DeletedAtUtc,
    DateTimeOffset? RestoredAtUtc,
    DateTimeOffset? CompletedAtUtc,
    TodoStatus Status);
