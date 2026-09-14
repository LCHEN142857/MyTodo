# MyToDo Windows Desktop Application Design

## Product Scope

MyToDo is a compact, single-instance Windows desktop todo widget. It provides an
active todo list, completed-history view, live search, inline editing, adjustable
opacity, and desktop/topmost placement. Data persists locally in a lightweight
SQLite database. The first release targets 64-bit Windows 10 and Windows 11.

## Technology

- C# with .NET 8 and WPF for the native Windows user interface.
- `Microsoft.Data.Sqlite` for local SQLite access.
- MVVM-style separation between views, presentation state, and persistence.
- A self-contained `win-x64` single-file publish produced by PowerShell.

WPF is preferred over WinUI 3 and Electron because it provides mature native
support for transparent borderless windows, window handles, hit testing,
topmost behavior, and simple self-contained deployment without a bundled web
runtime.

## Window Behavior

- The initial client size is 320 x 520 device-independent pixels.
- The minimum size is 260 x 320 device-independent pixels.
- The window is borderless with subtly rounded corners and a custom draggable
  header.
- Native hit testing exposes resize handles on all four edges and four corners.
- The opacity setting applies to the application window and is persisted between
  launches. The supported range is 35% to 100%, defaulting to 92%.
- The default placement mode is desktop level: the window remains above the
  wallpaper while ordinary application windows cover it.
- The pin control toggles between desktop-level placement and topmost placement.
  The visual state of the control always reflects the active mode.
- The last window position, size, opacity, and placement mode are persisted. If
  the saved bounds are no longer visible on a connected display, the window is
  moved into the primary display's working area.
- Only one process instance is allowed. A second launch signals the existing
  process, which restores and activates its existing window, then exits.

Desktop-level placement uses the Windows shell window hierarchy when available.
If Explorer is restarting or the shell host cannot be located, the application
falls back to a normal non-topmost window instead of failing to start.

## Navigation And Layout

The application has two views in the same window: To Do and History. Navigation
does not open another window.

### To Do View

- The header shows `MyToDo` at the left.
- The right side contains a `History` navigation command, a pin icon, and an `S`
  settings command.
- Below the header is a creation row containing a text input and plus icon button.
- Pressing Enter in the creation input or activating the plus button creates an
  item. Whitespace-only input is ignored. Leading and trailing whitespace is
  removed.
- A search input filters visible active todos immediately using a
  case-insensitive substring match.
- Each row contains a square completion checkbox, todo text, and delete icon.
- Activating the checkbox marks the item completed and immediately removes it
  from this view.
- Activating the text replaces it in place with an input containing the existing
  text, selected in full. Enter saves a non-empty trimmed value and records the
  update time. Escape cancels. Losing focus saves a valid change and otherwise
  cancels the edit.
- The delete icon soft-deletes the item immediately.
- The footer displays the total count of active todos, independent of the search
  filter.

### History View

- The header shows `History` at the left.
- The right side contains a `To Do` navigation command and pin icon. Settings
  remain reachable through the same `S` command so window controls are not lost.
- A search input filters completed items immediately using a case-insensitive
  substring match.
- Each row displays completed text with a strikethrough, followed by a restore
  icon shaped as an upward arrow meeting a horizontal bar, and a delete icon.
- Restore changes the item back to active, records the restoration time, and
  immediately removes it from History.
- Delete soft-deletes the completed item immediately.
- The footer displays the total count of completed items, independent of the
  search filter, and a `Clean histories` command.
- `Clean histories` soft-deletes every currently completed item. When no history
  exists, the command is disabled.

## Settings

The `S` command opens a compact anchored settings panel rather than another
window. It contains:

- `Opacity Settings` with a slider from 35% to 100% and the current percentage.
- An `Exit` command that cleanly closes the application.

Closing the settings panel or application persists the chosen opacity. Because
the main window has no standard title bar, `Exit` is the explicit application
shutdown path.

## Scrolling

The todo and history lists fill the space between their search row and footer.
They scroll only when their content exceeds the available height, so the number
of visible rows adapts to window size rather than using a fixed threshold.

The vertical scrollbar is hidden when scrolling is unnecessary. When scrolling
is possible, it appears while the pointer is near the right edge, while the user
is scrolling, or while the thumb is being dragged. A three-second inactivity
timer hides it after pointer and scrolling activity ends. The mouse wheel remains
functional while the scrollbar is hidden.

## Persistence Model

The database lives at `%LOCALAPPDATA%\MyToDo\mytodo.db`. Its directory is created
on first launch. Schema migrations run transactionally before the main window is
shown.

The `todos` table contains:

| Column | Meaning |
| --- | --- |
| `id` | Internal unique integer identifier |
| `device_name` | Device name at creation time |
| `windows_username` | Windows username at creation time |
| `content` | Current todo text |
| `created_at_utc` | Creation timestamp |
| `updated_at_utc` | Most recent content update timestamp, nullable |
| `deleted_at_utc` | Soft-deletion timestamp, nullable |
| `restored_at_utc` | Most recent completed-to-active restoration timestamp, nullable |
| `completed_at_utc` | Most recent active-to-completed timestamp, nullable |
| `status` | `todo`, `completed`, or `deleted` |

Timestamps are stored as UTC ISO 8601 text. Display ordering uses newest-created
first for active todos and newest-completed first for History. The database also
contains a schema-version table. Deleted records are retained and excluded from
both user-facing views.

Application settings are stored separately in
`%LOCALAPPDATA%\MyToDo\settings.json`; they do not belong to todo records.

## Components And Data Flow

- `MainWindow` owns native window behavior, custom chrome, resizing, placement,
  single-instance activation, and view composition.
- `MainViewModel` owns navigation, search text, commands, counts, and observable
  active/history collections.
- `TodoRepository` is the only component that reads or writes SQLite. It exposes
  operations for create, rename, complete, restore, delete, bulk-delete history,
  and query-by-status.
- `SettingsStore` reads and writes window preferences atomically.
- `DesktopWindowService` isolates Windows shell and topmost interop.
- `SingleInstanceService` owns process coordination and activation signaling.

User actions call view-model commands. Each mutating command first completes its
database transaction and then updates the in-memory collection and counts. A
failed database operation leaves the visible item in its prior state and shows a
compact error message in the window.

Search filtering operates on the loaded active or completed collection and does
not query on each keystroke. Switching views reloads the relevant status from the
repository so the database remains the source of truth.

## Failure Handling

- Database initialization failure shows an actionable startup error containing
  the database path and exits without showing a partially functioning window.
- Write failures retain the previous UI state and show a non-blocking error area;
  retrying the user action retries the transaction.
- Invalid empty todo text is ignored during creation and rejected during editing.
- Shell interop failure falls back to a normal non-topmost window.
- Corrupt settings are replaced with defaults without affecting the todo database.
- Bulk history cleanup is a single transaction, preventing partially cleaned
  history.

## Accessibility And Interaction

- Every icon button has a tooltip and automation name.
- All commands are keyboard reachable with a visible focus indicator.
- Enter creates or saves, Escape cancels inline editing, and Tab follows visual
  order.
- Text and controls reflow within the supported minimum width; todo text wraps
  without covering action buttons.
- Icons use a consistent Windows-style vector icon set rendered by WPF paths.

## Testing And Delivery

Automated tests cover repository migrations and CRUD/status transitions against
a temporary SQLite database, view-model filtering/counting/navigation, inline
edit rules, settings validation, and single-instance message behavior where it
can be isolated from the OS.

Windows integration checks cover launch, database creation, second-instance
activation, resizing, opacity, topmost toggling, desktop-level fallback, and the
published executable. A final manual UI pass checks To Do and History workflows,
smallest supported size, scrolling and its three-second hide delay, and display
scaling.

`build.ps1` restores dependencies, runs the test suite, and publishes a
self-contained, single-file `win-x64` executable into `artifacts\publish`. The
deliverable includes the executable and a concise README describing launch,
storage locations, and build prerequisites.
