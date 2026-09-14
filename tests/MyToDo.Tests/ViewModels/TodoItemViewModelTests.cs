using FluentAssertions;
using MyToDo.App.Data;
using MyToDo.App.Domain;
using MyToDo.App.ViewModels;
using Xunit;

namespace MyToDo.Tests.ViewModels;

public sealed class TodoItemViewModelTests
{
    [Fact]
    public void Begin_edit_copies_content_and_requests_select_all()
    {
        var row = CreateRow();
        var selectAllRequests = 0;
        row.RequestSelectAll += (_, _) => selectAllRequests++;

        row.BeginEdit();

        row.IsEditing.Should().BeTrue();
        row.EditText.Should().Be("Original");
        selectAllRequests.Should().Be(1);
    }

    [Fact]
    public async Task Save_edit_trims_non_empty_text_and_exits_editing()
    {
        var repository = new RecordingRepository();
        var row = CreateRow(repository);
        row.BeginEdit();
        row.EditText = "  Renamed  ";

        await row.SaveEditAsync();

        row.Content.Should().Be("Renamed");
        row.EditText.Should().Be("Renamed");
        row.IsEditing.Should().BeFalse();
        repository.RenamedContent.Should().Be("Renamed");
    }

    [Fact]
    public async Task Blank_edit_cancels_without_repository_write()
    {
        var repository = new RecordingRepository();
        var row = CreateRow(repository);
        row.BeginEdit();
        row.EditText = "  ";

        await row.SaveEditAsync();

        row.Content.Should().Be("Original");
        row.EditText.Should().Be("Original");
        row.IsEditing.Should().BeFalse();
        repository.RenameCalls.Should().Be(0);
    }

    [Fact]
    public void Cancel_edit_restores_existing_content()
    {
        var row = CreateRow();
        row.BeginEdit();
        row.EditText = "Discarded";

        row.CancelEdit();

        row.Content.Should().Be("Original");
        row.EditText.Should().Be("Original");
        row.IsEditing.Should().BeFalse();
    }

    private static TodoItemViewModel CreateRow(ITodoRepository? repository = null) =>
        new(new TodoItem(1, "PC", "user", "Original", DateTimeOffset.UtcNow, null, null, null, null, TodoStatus.Todo), repository ?? new RecordingRepository());

    private sealed class RecordingRepository : ITodoRepository
    {
        public int RenameCalls { get; private set; }
        public string? RenamedContent { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TodoItem>> QueryAsync(TodoStatus status, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TodoItem>>([]);
        public Task<TodoItem> CreateAsync(string deviceName, string windowsUsername, string content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RenameAsync(long id, string content, CancellationToken cancellationToken = default)
        {
            RenameCalls++;
            RenamedContent = content;
            return Task.CompletedTask;
        }

        public Task CompleteAsync(long id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RestoreAsync(long id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(long id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearCompletedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
