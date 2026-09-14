using FluentAssertions;
using MyToDo.App.Data;
using MyToDo.App.Domain;
using MyToDo.App.ViewModels;
using Xunit;

namespace MyToDo.Tests.ViewModels;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task Search_filters_visible_items_but_not_footer_count()
    {
        await using var repository = await SeededRepository.CreateAsync("Alpha", "Beta");
        var vm = await MainViewModel.CreateAsync(repository.Repository);

        vm.SearchText = "alp";

        vm.VisibleItems.Select(x => x.Content).Should().Equal("Alpha");
        vm.TodoCount.Should().Be(2);
    }

    [Fact]
    public async Task Renaming_items_reapplies_search_filter_in_both_directions()
    {
        await using var repository = await SeededRepository.CreateAsync("Alpha", "Beta");
        var vm = await MainViewModel.CreateAsync(repository.Repository);
        var nonMatchingItem = vm.VisibleItems.Single(x => x.Content == "Beta");
        vm.SearchText = "alp";

        var matchingItem = vm.VisibleItems.Single();
        matchingItem.BeginEdit();
        matchingItem.EditText = "Gamma";
        await matchingItem.SaveEditAsync();

        vm.VisibleItems.Should().BeEmpty();

        nonMatchingItem.BeginEdit();
        nonMatchingItem.EditText = "Alphabet";
        await nonMatchingItem.SaveEditAsync();

        vm.VisibleItems.Select(x => x.Content).Should().Equal("Alphabet");
    }

    [Fact]
    public async Task Completing_item_moves_it_from_todo_to_history()
    {
        await using var repository = await SeededRepository.CreateAsync("Finish me");
        var vm = await MainViewModel.CreateAsync(repository.Repository);

        await vm.VisibleItems.Single().CompleteAsync();

        vm.TodoCount.Should().Be(0);
        await vm.ShowHistoryAsync();
        vm.VisibleItems.Select(x => x.Content).Should().Equal("Finish me");
        vm.HistoryCount.Should().Be(1);
    }

    [Fact]
    public async Task Navigating_to_history_reloads_completed_items_from_repository()
    {
        await using var repository = await SeededRepository.CreateAsync("Completed elsewhere");
        var vm = await MainViewModel.CreateAsync(repository.Repository);
        var item = (await repository.Repository.QueryAsync(TodoStatus.Todo)).Single();
        await repository.Repository.CompleteAsync(item.Id);

        await vm.ShowHistoryAsync();

        vm.VisibleItems.Select(x => x.Content).Should().Equal("Completed elsewhere");
        vm.HistoryCount.Should().Be(1);
    }

    [Fact]
    public async Task Creating_item_trims_content_and_updates_unfiltered_count()
    {
        await using var repository = await SeededRepository.CreateAsync();
        var vm = await MainViewModel.CreateAsync(repository.Repository);
        vm.NewTodoText = "  Buy milk  ";

        await vm.CreateAsync();

        vm.VisibleItems.Select(x => x.Content).Should().Equal("Buy milk");
        vm.TodoCount.Should().Be(1);
        vm.NewTodoText.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_failure_keeps_visible_items_unchanged_and_exposes_error()
    {
        var repository = new FailingCreateRepository();
        var vm = await MainViewModel.CreateAsync(repository);
        vm.NewTodoText = "Will fail";

        await vm.CreateAsync();

        vm.TodoCount.Should().Be(1);
        vm.VisibleItems.Select(x => x.Content).Should().Equal("Existing");
        vm.ErrorMessage.Should().Be("Database unavailable.");
    }

    private sealed class SeededRepository : IAsyncDisposable
    {
        private SeededRepository(string databasePath, TodoRepository repository)
        {
            DatabasePath = databasePath;
            Repository = repository;
        }

        public string DatabasePath { get; }
        public TodoRepository Repository { get; }

        public static async Task<SeededRepository> CreateAsync(params string[] contents)
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"mytodo-vm-{Guid.NewGuid():N}.db");
            var repository = new TodoRepository(databasePath);
            await repository.InitializeAsync();
            foreach (var content in contents)
            {
                await repository.CreateAsync("PC", "user", content);
            }

            return new SeededRepository(databasePath, repository);
        }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(DatabasePath))
            {
                File.Delete(DatabasePath);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingCreateRepository : ITodoRepository
    {
        private readonly TodoItem _existing = new(1, "PC", "user", "Existing", DateTimeOffset.UtcNow, null, null, null, null, TodoStatus.Todo);

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TodoItem>> QueryAsync(TodoStatus status, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TodoItem>>(status == TodoStatus.Todo ? [_existing] : []);
        public Task<TodoItem> CreateAsync(string deviceName, string windowsUsername, string content, CancellationToken cancellationToken = default) => Task.FromException<TodoItem>(new InvalidOperationException("Database unavailable."));
        public Task RenameAsync(long id, string content, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CompleteAsync(long id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RestoreAsync(long id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(long id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearCompletedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
