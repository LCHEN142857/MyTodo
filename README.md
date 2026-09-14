# MyToDo

MyToDo is a compact Windows desktop todo widget for Windows 10 and Windows 11 (x64). It runs as a single instance; launching it again activates the existing window.

## Use

- Type a task and press **Enter**, or select **+**, to create it.
- Select a task to edit it inline. Press **Enter** or move focus away to save a non-empty change; press **Escape** to cancel.
- Use the search field to filter visible rows. Footer counts remain totals for the current page.
- Check a task to move it to **History**. Restore it from History to return to To Do.
- Delete individual rows, or use **Clean histories** to soft-delete all completed rows.
- The settings panel controls opacity and the desktop/topmost pin mode. **Exit** saves window settings.

## Local data

The app creates its SQLite database on first launch at `%LOCALAPPDATA%\MyToDo\mytodo.db` and stores window settings at `%LOCALAPPDATA%\MyToDo\settings.json`. Timestamps are stored as UTC ISO 8601 values. Deletion is soft: deleted rows remain in the database with status `deleted` and a deletion timestamp.

## Build and launch

Build on Windows with PowerShell 5.1+ and the .NET 8 SDK. From the repository root run:

```powershell
.\build.ps1
```

The script restores the solution, runs the complete test suite, and publishes a self-contained single-file `win-x64` app to `artifacts/publish/MyToDo.exe`. The script uses the repo-local `.tools\dotnet8\dotnet.exe` when present, then a system `dotnet`; set `$env:MYTODO_DOTNET` to override discovery in automation. Because the output is self-contained, target machines do not require .NET to launch it:

```powershell
.\artifacts\publish\MyToDo.exe
```
