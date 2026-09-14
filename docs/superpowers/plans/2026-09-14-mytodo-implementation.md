# MyToDo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build, test, and package a compact single-instance Windows todo widget with SQLite persistence, To Do and History workflows, opacity control, and desktop/topmost placement.

**Architecture:** A .NET 8 WPF executable uses a small MVVM boundary: `MainViewModel` coordinates user workflows, `TodoRepository` owns all SQLite transactions, and dedicated services isolate settings, process coordination, and Win32 window behavior. The WPF view binds to observable state and contains only focus, hit-testing, and transient visual behavior that cannot sensibly live in the view model.

**Tech Stack:** C# 12, .NET 8 WPF, Microsoft.Data.Sqlite, xUnit, FluentAssertions, PowerShell 5.1+, Windows 10/11 x64.

## Global Constraints

- Target Windows 10 and Windows 11 x64 with `net8.0-windows`.
- Default window size is 320 x 520 DIPs; minimum size is 260 x 320 DIPs.
- Persist the database at `%LOCALAPPDATA%\MyToDo\mytodo.db` and settings at `%LOCALAPPDATA%\MyToDo\settings.json`.
- Store timestamps as UTC ISO 8601 text and retain soft-deleted rows.
- Todo states are exactly `todo`, `completed`, and `deleted`.
- Opacity is constrained to 35%-100%, defaulting to 92%.
- Search uses case-insensitive substring matching and never changes total footer counts.
- Publish as a self-contained, single-file `win-x64` executable.
- Write a failing automated test before each production behavior and run it once in the failing state.

## File Map

- `MyToDo.sln`: solution entry point.
- `src/MyToDo.App/MyToDo.App.csproj`: WPF executable and package references.
- `src/MyToDo.App/App.xaml(.cs)`: startup, dependency composition, single-instance lifecycle.
- `src/MyToDo.App/Domain/TodoItem.cs`: todo record and status values.
- `src/MyToDo.App/Data/ITodoRepository.cs`: persistence contract.
- `src/MyToDo.App/Data/TodoRepository.cs`: SQLite schema and transactions.
- `src/MyToDo.App/Settings/AppSettings.cs`: validated window preferences.
- `src/MyToDo.App/Settings/SettingsStore.cs`: atomic JSON settings persistence.
- `src/MyToDo.App/Services/SingleInstanceService.cs`: mutex and activation pipe.
- `src/MyToDo.App/Services/DesktopWindowService.cs`: Win32 placement and resize interop.
- `src/MyToDo.App/ViewModels/MainViewModel.cs`: navigation, collections, commands, filtering.
- `src/MyToDo.App/ViewModels/TodoItemViewModel.cs`: row editing state and row commands.
- `src/MyToDo.App/Infrastructure/*`: observable object and command helpers.
- `src/MyToDo.App/MainWindow.xaml(.cs)`: view, focus behavior, scrollbar inactivity timer.
- `src/MyToDo.App/Styles.xaml`: colors, typography, and control templates.
- `tests/MyToDo.Tests/*`: domain, repository, settings, service, and view-model tests.
- `build.ps1`: deterministic restore, test, and publish pipeline.
- `README.md`: user and developer instructions.
- `.gitignore`: generated .NET, test, and artifact output.

---

### Task 1: Solution Bootstrap And SQLite Repository

**Files:**
- Create: `MyToDo.sln`
- Create: `.gitignore`
- Create: `src/MyToDo.App/MyToDo.App.csproj`
- Create: `src/MyToDo.App/Domain/TodoItem.cs`
- Create: `src/MyToDo.App/Data/ITodoRepository.cs`
- Create: `src/MyToDo.App/Data/TodoRepository.cs`
- Create: `tests/MyToDo.Tests/MyToDo.Tests.csproj`
- Create: `tests/MyToDo.Tests/Data/TodoRepositoryTests.cs`

**Interfaces:**
- Produces: `TodoItem(long Id, string DeviceName, string WindowsUsername, string Content, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc, DateTimeOffset? DeletedAtUtc, DateTimeOffset? RestoredAtUtc, DateTimeOffset? CompletedAtUtc, TodoStatus Status)`.
- Produces: `TodoStatus { Todo, Completed, Deleted }` persisted as lowercase strings.
- Produces: `ITodoRepository.InitializeAsync()`, `QueryAsync(TodoStatus)`, `CreateAsync(string, string, string)`, `RenameAsync(long, string)`, `CompleteAsync(long)`, `RestoreAsync(long)`, `DeleteAsync(long)`, and `ClearCompletedAsync()`.

- [ ] **Step 1: Install the missing .NET 8 SDK and verify it**

Use Microsoft's `dotnet-install.ps1` to install SDK 8 into a task-local tools directory when `dotnet` is unavailable, prepend that directory for the current process, then run:

```powershell
dotnet --info
```

Expected: an 8.0 SDK is listed on Windows x64.

- [ ] **Step 2: Create the solution, WPF project, test project, references, and ignore rules**

The application project uses `Microsoft.Data.Sqlite`; tests use xUnit and FluentAssertions. Add both projects to `MyToDo.sln` and reference the application from tests. Ignore `bin/`, `obj/`, `TestResults/`, `.vs/`, and `artifacts/`.

- [ ] **Step 3: Write failing repository tests**

Tests create a unique temporary database and assert real SQLite behavior:

```csharp
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
```

- [ ] **Step 4: Run repository tests and verify the RED state**

Run: `dotnet test tests/MyToDo.Tests/MyToDo.Tests.csproj --filter TodoRepositoryTests`

Expected: compilation fails because the domain and repository types do not exist.

- [ ] **Step 5: Implement schema migration and repository transactions**

Create schema version 1 with `schema_info` and `todos`, a status check constraint, and status/order indexes. Parameterize all SQL. Each status mutation executes one transaction, updates its matching audit timestamp, and rejects missing IDs with `InvalidOperationException`. `CreateAsync` trims content and rejects empty input with `ArgumentException`.

- [ ] **Step 6: Run repository tests and the whole suite**

Run: `dotnet test MyToDo.sln`

Expected: all repository tests pass with zero warnings.

- [ ] **Step 7: Commit**

```powershell
git add MyToDo.sln .gitignore src/MyToDo.App tests/MyToDo.Tests
git commit -m "feat: add SQLite todo repository"
```

---

### Task 2: Settings And Single-Instance Services

**Files:**
- Create: `src/MyToDo.App/Settings/AppSettings.cs`
- Create: `src/MyToDo.App/Settings/ISettingsStore.cs`
- Create: `src/MyToDo.App/Settings/SettingsStore.cs`
- Create: `src/MyToDo.App/Services/ISingleInstanceService.cs`
- Create: `src/MyToDo.App/Services/SingleInstanceService.cs`
- Test: `tests/MyToDo.Tests/Settings/SettingsStoreTests.cs`
- Test: `tests/MyToDo.Tests/Services/SingleInstanceServiceTests.cs`

**Interfaces:**
- Produces: `AppSettings(double Left, double Top, double Width, double Height, double Opacity, bool IsTopmost)` and `AppSettings.Default`.
- Produces: `ISettingsStore.LoadAsync()` and `SaveAsync(AppSettings)`.
- Produces: `ISingleInstanceService.TryAcquireAsync()`, `WaitForActivationAsync(CancellationToken)`, `SignalExistingAsync()`, and `DisposeAsync()`.

- [ ] **Step 1: Write failing settings tests**

```csharp
[Fact]
public async Task Missing_or_corrupt_settings_return_valid_defaults()
{
    using var folder = new TemporaryFolder();
    var store = new SettingsStore(folder.Path);
    (await store.LoadAsync()).Should().Be(AppSettings.Default);
    await File.WriteAllTextAsync(store.FilePath, "not-json");
    (await store.LoadAsync()).Should().Be(AppSettings.Default);
}

[Theory]
[InlineData(.1, .35)]
[InlineData(2, 1)]
public void Normalize_clamps_opacity(double input, double expected) =>
    AppSettings.Default with { Opacity = input }.Normalize().Opacity.Should().Be(expected);
```

- [ ] **Step 2: Run settings tests and verify RED**

Run: `dotnet test MyToDo.sln --filter SettingsStoreTests`

Expected: compilation fails because settings types do not exist.

- [ ] **Step 3: Implement validated atomic settings persistence**

Serialize camel-case JSON to a temporary file, replace the destination atomically, normalize opacity and dimensions on read, and return defaults for missing or malformed JSON.

- [ ] **Step 4: Write and run a failing single-instance coordination test**

Create two services using a unique test name. Assert the first acquires ownership, the second cannot acquire, and `SignalExistingAsync` causes the first service's `WaitForActivationAsync` to complete. Run with `--filter SingleInstanceServiceTests` and expect missing-type compilation failure.

- [ ] **Step 5: Implement mutex and named-pipe activation**

Use a named mutex for ownership and a same-user named pipe for activation. Bound connection attempts to two seconds so a stale mutex cannot hang startup. Dispose the pipe loop via cancellation.

- [ ] **Step 6: Run all tests and commit**

Run: `dotnet test MyToDo.sln`

Expected: all tests pass.

```powershell
git add src/MyToDo.App/Settings src/MyToDo.App/Services tests/MyToDo.Tests
git commit -m "feat: persist settings and enforce single instance"
```

---

### Task 3: View Models And Todo Workflows

**Files:**
- Create: `src/MyToDo.App/Infrastructure/ObservableObject.cs`
- Create: `src/MyToDo.App/Infrastructure/RelayCommand.cs`
- Create: `src/MyToDo.App/Infrastructure/AsyncRelayCommand.cs`
- Create: `src/MyToDo.App/ViewModels/AppPage.cs`
- Create: `src/MyToDo.App/ViewModels/TodoItemViewModel.cs`
- Create: `src/MyToDo.App/ViewModels/MainViewModel.cs`
- Test: `tests/MyToDo.Tests/ViewModels/MainViewModelTests.cs`
- Test: `tests/MyToDo.Tests/ViewModels/TodoItemViewModelTests.cs`

**Interfaces:**
- Produces: `AppPage.ToDo` and `AppPage.History`.
- Produces bindable `MainViewModel.CurrentPage`, `NewTodoText`, `SearchText`, `TodoCount`, `HistoryCount`, `VisibleItems`, `ErrorMessage`, and create/navigate/clear commands.
- Produces bindable `TodoItemViewModel.Content`, `IsEditing`, `EditText`, and complete/restore/delete/begin-edit/save-edit/cancel-edit commands.
- Consumes: `ITodoRepository` from Task 1.

- [ ] **Step 1: Write failing filtering, creation, and navigation tests**

```csharp
[Fact]
public async Task Search_filters_visible_items_but_not_footer_count()
{
    var repository = await SeededRepository.CreateAsync("Alpha", "Beta");
    var vm = await MainViewModel.CreateAsync(repository);
    vm.SearchText = "alp";
    vm.VisibleItems.Select(x => x.Content).Should().Equal("Alpha");
    vm.TodoCount.Should().Be(2);
}

[Fact]
public async Task Completing_item_moves_it_from_todo_to_history()
{
    var repository = await SeededRepository.CreateAsync("Finish me");
    var vm = await MainViewModel.CreateAsync(repository);
    await vm.VisibleItems.Single().CompleteAsync();
    vm.TodoCount.Should().Be(0);
    await vm.ShowHistoryAsync();
    vm.VisibleItems.Select(x => x.Content).Should().Equal("Finish me");
    vm.HistoryCount.Should().Be(1);
}
```

- [ ] **Step 2: Run view-model tests and verify RED**

Run: `dotnet test MyToDo.sln --filter MainViewModelTests`

Expected: compilation fails because view-model types do not exist.

- [ ] **Step 3: Implement observable and command infrastructure plus MainViewModel**

Load status-specific collections from SQLite, maintain counts independent of `SearchText`, and refresh `ICollectionView` on a case-insensitive `Contains`. Mutations await the repository before changing collections. Expose database errors through `ErrorMessage` without applying optimistic UI state.

- [ ] **Step 4: Write failing inline-edit tests**

Assert `BeginEdit` copies and selects the existing content via a `RequestSelectAll` event, `SaveEditAsync` trims non-empty changes and exits editing, invalid blank edits cancel without repository writes, and `CancelEdit` restores existing content.

- [ ] **Step 5: Run inline-edit tests and verify RED, then implement the row view model**

Run: `dotnet test MyToDo.sln --filter TodoItemViewModelTests`

Expected before implementation: missing-type compilation failure. Implement the minimum editing and row command behavior, then rerun the same command and expect PASS.

- [ ] **Step 6: Run all tests and commit**

Run: `dotnet test MyToDo.sln`

```powershell
git add src/MyToDo.App/Infrastructure src/MyToDo.App/ViewModels tests/MyToDo.Tests/ViewModels
git commit -m "feat: add todo and history workflows"
```

---

### Task 4: WPF Application And Complete User Interface

**Files:**
- Create: `src/MyToDo.App/App.xaml`
- Create: `src/MyToDo.App/App.xaml.cs`
- Create: `src/MyToDo.App/MainWindow.xaml`
- Create: `src/MyToDo.App/MainWindow.xaml.cs`
- Create: `src/MyToDo.App/Styles.xaml`
- Create: `src/MyToDo.App/Converters/PageVisibilityConverter.cs`
- Test: `tests/MyToDo.Tests/Ui/UiContractTests.cs`

**Interfaces:**
- Consumes: all bindable properties and commands from Task 3.
- Produces: `MainWindow.ActivateFromSecondInstance()` for Task 2 startup wiring.

- [ ] **Step 1: Write a failing UI contract test**

Load `MainWindow.xaml` as XML and assert named controls and bindings exist: `RootWindow`, `DragRegion`, `NewTodoInput`, `AddTodoButton`, `SearchInput`, `TodoList`, `HistoryButton`, `TodoButton`, `SettingsButton`, `PinButton`, `CleanHistoriesButton`, `OpacitySlider`, and `ExitButton`. Assert every icon-only button has a tooltip and automation name.

- [ ] **Step 2: Run the UI contract test and verify RED**

Run: `dotnet test MyToDo.sln --filter UiContractTests`

Expected: failure because `MainWindow.xaml` does not exist.

- [ ] **Step 3: Implement the visual system and responsive layout**

Use a restrained neutral palette with white/charcoal surfaces, blue primary actions, green completed accents, and red destructive states. Use 6px corner radii or less. Keep header and footer fixed and give the list the remaining grid row. Bind To Do/History visibility, counts, settings popup, opacity, and all commands. Use vector `Path` icons with tooltips for add, pin, restore, and delete; keep the specified literal `S`, `History`, `To Do`, and `Clean histories` labels.

- [ ] **Step 4: Implement creation and inline-edit focus behavior**

Enter invokes create/save, Escape cancels, and losing edit focus saves valid text. Handle `RequestSelectAll` by focusing the row editor and calling `SelectAll`. Header drag begins only from empty header space so buttons remain clickable.

- [ ] **Step 5: Compose startup and shutdown**

On first-instance startup, initialize SQLite, load settings, construct the view model/window, and begin activation listening. On a second instance, signal and exit. `Exit` saves settings, disposes services, and shuts down. Startup database errors show the exact database path in a message box and exit.

- [ ] **Step 6: Run tests, build, and commit**

Run: `dotnet test MyToDo.sln` then `dotnet build MyToDo.sln -c Release --no-restore`

Expected: tests and WPF compilation pass with zero errors.

```powershell
git add src/MyToDo.App tests/MyToDo.Tests/Ui
git commit -m "feat: build MyToDo WPF interface"
```

---

### Task 5: Native Window Placement, Resizing, And Scrollbar Timing

**Files:**
- Create: `src/MyToDo.App/Services/IDesktopWindowService.cs`
- Create: `src/MyToDo.App/Services/DesktopWindowService.cs`
- Create: `src/MyToDo.App/Services/ScrollbarVisibilityController.cs`
- Modify: `src/MyToDo.App/MainWindow.xaml`
- Modify: `src/MyToDo.App/MainWindow.xaml.cs`
- Test: `tests/MyToDo.Tests/Services/ScrollbarVisibilityControllerTests.cs`
- Test: `tests/MyToDo.Tests/Settings/WindowBoundsTests.cs`

**Interfaces:**
- Produces: `IDesktopWindowService.AttachToDesktop(IntPtr)`, `SetTopmost(IntPtr, bool)`, and `EnsureVisible(AppSettings, IReadOnlyList<DisplayBounds>)`.
- Produces: `ScrollbarVisibilityController.NotifyActivity()`, `IsVisible`, and a three-second injected timer boundary.
- Consumes: the WPF window handle, `AppSettings`, and persisted `IsTopmost`.

- [ ] **Step 1: Write failing pure tests for bounds recovery and scrollbar timing**

```csharp
[Fact]
public void Offscreen_bounds_are_moved_inside_primary_work_area()
{
    var saved = AppSettings.Default with { Left = 9000, Top = 9000 };
    var result = WindowBounds.EnsureVisible(saved, [new(0, 0, 1920, 1040)]);
    result.Left.Should().BeInRange(0, 1660);
    result.Top.Should().BeInRange(0, 720);
}

[Fact]
public void Scrollbar_hides_three_seconds_after_last_activity()
{
    var clock = new ManualDelayScheduler();
    var controller = new ScrollbarVisibilityController(clock);
    controller.NotifyActivity();
    controller.IsVisible.Should().BeTrue();
    clock.Advance(TimeSpan.FromSeconds(2.9));
    controller.IsVisible.Should().BeTrue();
    clock.Advance(TimeSpan.FromSeconds(.1));
    controller.IsVisible.Should().BeFalse();
}
```

- [ ] **Step 2: Run service tests and verify RED**

Run: `dotnet test MyToDo.sln --filter "WindowBoundsTests|ScrollbarVisibilityControllerTests"`

Expected: compilation fails because the service/controller types do not exist.

- [ ] **Step 3: Implement deterministic bounds and scrollbar controllers**

Keep timing testable through an injected delay scheduler. Restart the hide delay on right-edge pointer entry, wheel activity, touchpad scroll, or thumb drag. Suppress the visual scrollbar entirely when `ScrollableHeight` is zero while preserving wheel input.

- [ ] **Step 4: Implement Win32 placement and native resize hit testing**

Use `WindowChrome` for 8-DIP resize borders and rounded custom chrome. For desktop mode, locate `Progman`/`WorkerW` and parent/order the window above wallpaper but below ordinary applications. For topmost mode, detach from the shell parent when necessary and call `SetWindowPos(HWND_TOPMOST)`. If shell lookup fails, use `HWND_NOTOPMOST` as the documented fallback. Reapply desktop attachment when Explorer recreates its shell windows.

- [ ] **Step 5: Wire saved bounds, opacity, and placement**

Normalize the saved bounds against `System.Windows.Forms.Screen.AllScreens`; apply them before showing the window. Persist actual bounds, opacity, and pin mode during clean shutdown. Update the pin button's automation name and visual state after every toggle.

- [ ] **Step 6: Run automated and manual integration checks, then commit**

Run: `dotnet test MyToDo.sln` and `dotnet build MyToDo.sln -c Release --no-restore`.

Launch the Release executable and verify resize from every edge/corner, desktop-level placement, topmost toggle, opacity extremes, mouse-wheel scrolling while the bar is hidden, and hide delay of approximately three seconds.

```powershell
git add src/MyToDo.App tests/MyToDo.Tests
git commit -m "feat: add native widget window behavior"
```

---

### Task 6: Packaging, Documentation, And Final Acceptance

**Files:**
- Create: `build.ps1`
- Modify: `README.md`
- Test: `tests/MyToDo.Tests/Packaging/PackagingContractTests.cs`

**Interfaces:**
- Produces: `build.ps1 [-Configuration Release] [-Runtime win-x64]`.
- Produces: `artifacts/publish/MyToDo.exe`.

- [ ] **Step 1: Write a failing packaging contract test**

Assert `build.ps1` exists and invokes restore, the complete test suite, and `dotnet publish` with `SelfContained=true`, `PublishSingleFile=true`, `RuntimeIdentifier=win-x64`, and output under `artifacts/publish`. Assert README documents data locations, build prerequisites, launch, and soft deletion.

- [ ] **Step 2: Run packaging test and verify RED**

Run: `dotnet test MyToDo.sln --filter PackagingContractTests`

Expected: failure because `build.ps1` and complete README content do not exist.

- [ ] **Step 3: Implement the build script**

Use `$PSScriptRoot` for stable paths, `$ErrorActionPreference = 'Stop'`, explicit argument validation, and separate restore/test/publish invocations. Remove only the resolved `artifacts/publish` directory after verifying it is a child of the repository's `artifacts` directory. Print the final executable path and size.

- [ ] **Step 4: Replace README with user and build documentation**

Document Windows support, controls, single-instance behavior, database/settings paths, soft-delete semantics, `.\build.ps1`, and the artifact path. State that target machines do not require .NET because the build is self-contained.

- [ ] **Step 5: Run the full verification pipeline**

Run:

```powershell
.\build.ps1
dotnet test MyToDo.sln -c Release --no-restore
Get-Item artifacts\publish\MyToDo.exe
```

Expected: zero failed tests, successful publish, and a non-empty `MyToDo.exe`.

- [ ] **Step 6: Execute packaged-app acceptance checks**

Launch `artifacts\publish\MyToDo.exe` and verify: initial database creation; create by plus and Enter; live search; inline edit Enter/Escape/blur; complete-to-History; restore; per-row soft delete; Clean histories; correct unfiltered counts; responsive minimum size; conditional scrollbar and three-second hide; opacity persistence; desktop/topmost toggle; second launch activation; and clean Exit.

Query the resulting SQLite file with an integration helper or repository test to confirm device/user fields and all relevant timestamps/status values are retained after the UI workflows.

- [ ] **Step 7: Run final source checks and commit**

Run: `git diff --check`, `dotnet test MyToDo.sln -c Release --no-restore`, and `git status --short`.

```powershell
git add build.ps1 README.md tests/MyToDo.Tests/Packaging
git commit -m "build: package and document MyToDo"
```
