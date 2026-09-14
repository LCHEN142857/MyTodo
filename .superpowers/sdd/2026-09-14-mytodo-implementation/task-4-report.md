# Task 4 Report: WPF Application And Complete User Interface

## Delivered

- Added the WPF application resource dictionary, neutral visual system, converter, and borderless responsive `MainWindow`.
- Added startup composition for the single-instance service, SQLite initialization, settings loading, activation signaling, startup database error reporting, and shutdown settings persistence.
- Added To Do and History navigation, create/search/count bindings, settings popup with opacity slider and Exit, pin behavior, row complete/restore/delete commands, and inline edit keyboard/focus behavior.
- Added XML UI contract coverage for required named controls, bindings, and icon-button accessibility metadata.

## Verification

- `dotnet test MyToDo.sln --filter UiContractTests` passed (1 test).
- `dotnet test MyToDo.sln` passed (24 tests).
- `dotnet build MyToDo.sln -c Release --no-restore` passed with 0 warnings and 0 errors.

The UI contract test was first run before XAML creation and failed because `MainWindow.xaml` was absent, then passed after implementation.
