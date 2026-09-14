using FluentAssertions;
using MyToDo.App.Data;
using MyToDo.App.Domain;
using Xunit;

namespace MyToDo.Tests.Data;

public sealed class TodoRepositoryTests
{
    [Fact]
    public async Task Create_trims_content_and_persists_audit_identity()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();

        var created = await fixture.Repository.CreateAsync("PC", "user", "  Ship app  ");

        created.Content.Should().Be("Ship app");
        created.DeviceName.Should().Be("PC");
        created.WindowsUsername.Should().Be("user");
        created.CreatedAtUtc.Offset.Should().Be(TimeSpan.Zero);
        created.Status.Should().Be(TodoStatus.Todo);
        (await fixture.Repository.QueryAsync(TodoStatus.Todo)).Should().ContainSingle(x => x.Id == created.Id);
    }

    [Fact]
    public async Task Create_rejects_empty_content()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();

        var action = () => fixture.Repository.CreateAsync("PC", "user", "  ");

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Rename_updates_content_and_update_time()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var created = await fixture.Repository.CreateAsync("PC", "user", "Original");

        await fixture.Repository.RenameAsync(created.Id, "  Renamed  ");

        var renamed = (await fixture.Repository.QueryAsync(TodoStatus.Todo)).Single();
        renamed.Content.Should().Be("Renamed");
        renamed.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Complete_restore_and_delete_preserve_row_and_audit_times()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var created = await fixture.Repository.CreateAsync("PC", "user", "Ship app");

        await fixture.Repository.CompleteAsync(created.Id);
        (await fixture.Repository.QueryAsync(TodoStatus.Completed)).Single()
            .CompletedAtUtc.Should().NotBeNull();

        await fixture.Repository.RestoreAsync(created.Id);
        (await fixture.Repository.QueryAsync(TodoStatus.Todo)).Single()
            .RestoredAtUtc.Should().NotBeNull();

        await fixture.Repository.DeleteAsync(created.Id);
        (await fixture.Repository.QueryAsync(TodoStatus.Deleted)).Single()
            .DeletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Clear_completed_soft_deletes_only_completed_rows()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var active = await fixture.Repository.CreateAsync("PC", "user", "Keep");
        var done = await fixture.Repository.CreateAsync("PC", "user", "Clean");
        await fixture.Repository.CompleteAsync(done.Id);

        await fixture.Repository.ClearCompletedAsync();

        (await fixture.Repository.QueryAsync(TodoStatus.Todo)).Select(x => x.Id)
            .Should().ContainSingle().Which.Should().Be(active.Id);
        (await fixture.Repository.QueryAsync(TodoStatus.Deleted)).Select(x => x.Id)
            .Should().Contain(done.Id);
    }

    [Fact]
    public async Task Mutating_a_missing_item_is_rejected()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();

        var action = () => fixture.Repository.CompleteAsync(404);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class RepositoryFixture : IAsyncDisposable
    {
        private RepositoryFixture(string databasePath, TodoRepository repository)
        {
            DatabasePath = databasePath;
            Repository = repository;
        }

        public string DatabasePath { get; }
        public TodoRepository Repository { get; }

        public static async Task<RepositoryFixture> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"mytodo-{Guid.NewGuid():N}.db");
            var repository = new TodoRepository(databasePath);
            await repository.InitializeAsync();
            return new RepositoryFixture(databasePath, repository);
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
}
