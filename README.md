# MyTodo

A transparent, always-on-your-desktop todo list app for Windows. Built with Electron.

## Background

MyTodo is designed to live on your desktop as a lightweight, semi-transparent overlay — not a full-sized window that gets in your way. It stays in the corner of your screen, lets you jot down tasks quickly, and gets out of the way when you don't need it. Think of it as a sticky note that knows how to manage your todos.

## Features

### Window
- Frameless transparent window with rounded corners
- Adjustable opacity (0%–100%) — make it as subtle or visible as you want
- Pin to top (always on top of all windows) or let it sit on the desktop layer
- Resizable from any edge

### Todo Management
- Quick add: type and hit Enter or click the + button
- Checkbox to complete a todo — completed items get a strikethrough and move to History
- Click any todo text to edit it inline, press Enter to save
- Real-time search filtering
- Delete button on hover (× icon)

### History
- View all completed todos in the History view
- Restore any history item back to the active todo list (restore icon)
- Delete individual history items
- "Clean histories" button to clear all history at once
- History count displayed at the bottom-left

### Settings (S button)
- Opacity slider (0%–100%) — adjusts background transparency only, buttons and text stay fully visible
- Exit button

### Smart Scrollbar
- No scrollbar when content fits the window
- When content overflows, hover near the right edge to reveal the scrollbar
- Scrollbar auto-hides 3 seconds after the mouse leaves

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | Electron 28 |
| Language | JavaScript (ES6+) |
| UI | HTML5 + CSS3 (no frontend framework) |
| Build Tool | electron-builder 24 |
| Inter-process Communication | Context Bridge + IPC |

### Project Structure

```
MyTodo/
├── main.js          # Main process: window creation, IPC handlers, data persistence
├── preload.js       # Context bridge: exposes safe APIs to the renderer
├── package.json     # Dependencies and electron-builder config
├── build.ps1        # One-click build script (PowerShell)
├── .npmrc            # npm mirror config (for faster downloads in China)
├── build/
│   ├── icon.png     # Source icon (1920×1920 PNG)
│   └── icon.ico     # Generated ICO file (multi-size: 16–256px)
└── src/
    ├── index.html   # App layout
    ├── styles.css   # All styling (dark theme, transparency, scrollbar)
    ├── renderer.js  # All UI logic (CRUD, search, edit, views, scrollbar)
    └── icon.ico     # Copy of icon for packaged app
```

### Architecture

```
┌─────────────────────────────────────────────┐
│  Main Process (main.js)                      │
│  ┌─────────────┐  ┌──────────────────────┐  │
│  │ BrowserWindow │  │  IPC Handlers        │  │
│  │ (transparent) │  │  get-data           │  │
│  │               │  │  save-data          │  │
│  │               │  │  set-opacity        │  │
│  │               │  │  set-pinned         │  │
│  │               │  │  exit-app           │  │
│  └─────────────┘  └──────────────────────┘  │
│         ▲                                     │
│         │ preload.js (contextBridge)          │
│         ▼                                     │
├─────────────────────────────────────────────┤
│  Renderer Process (src/)                     │
│  ┌─────────────┐  ┌──────────────────────┐  │
│  │ index.html   │  │  renderer.js        │  │
│  │ styles.css   │  │  - Todo CRUD        │  │
│  │              │  │  - Search filter     │  │
│  │              │  │  - Inline edit       │  │
│  │              │  │  - History mgmt     │  │
│  │              │  │  - Scrollbar logic   │  │
│  └─────────────┘  └──────────────────────┘  │
└─────────────────────────────────────────────┘
```

## Data Storage

Data is persisted as a JSON file on disk:

```
%APPDATA%\MyTodo\mytodo-data.json
```

### Data Structure

```json
{
  "todos": [
    {
      "id": "string",
      "text": "string",
      "completed": false,
      "createdAt": "ISO 8601 timestamp"
    }
  ],
  "history": [
    {
      "id": "string",
      "text": "string",
      "completedAt": "ISO 8601 timestamp"
    }
  ],
  "settings": {
    "opacity": 0.92,
    "isPinned": false
  }
}
```

- **Writes are debounced** (400ms) to avoid excessive disk I/O during rapid edits
- **Reads happen on startup** and on every IPC call (no in-memory cache in main process — simple and reliable)
- No database dependency — a JSON file is sufficient for this scale

## Getting Started

### Prerequisites

- [Node.js](https://nodejs.org/) 16+ (LTS recommended)
- PowerShell 5+ (comes with Windows 10/11)

### Installation

```bash
git clone <your-repo-url>
cd MyTodo
npm install
```

### Run in Development

```bash
npm run dev
```

Opens the app with DevTools attached for debugging.

### Run without DevTools

```bash
npm start
```

## Build

### One-click build (recommended)

```powershell
.\build.ps1
```

The script will:
1. Install dependencies if `node_modules` is missing
2. Generate `icon.ico` from `build/icon.png` if needed
3. Build portable exe and NSIS installer via electron-builder
4. Set the app icon on the unpacked executable via rcedit
5. Print a summary of output files

### Manual build

```bash
# Build both portable and NSIS installer
npm run build

# Or build individually
npm run build:portable   # dist\MyTodo-Portable-1.0.0.exe
npm run build:nsis       # dist\MyTodo-1.0.0-x64.exe
```

### Build Output

| File | Type | Size |
|------|------|------|
| `MyTodo-Portable-1.0.0.exe` | Standalone portable (no install needed) | ~66 MB |
| `MyTodo-1.0.0-x64.exe` | NSIS installer (with desktop shortcut) | ~66 MB |

Both are output to the `dist/` directory.

## Usage

| Action | How |
|--------|-----|
| Add todo | Type in the bottom input box → Enter or click + |
| Complete todo | Click the checkbox |
| Edit todo | Click the todo text → edit → Enter to save / Esc to cancel |
| Delete todo | Hover over the item → click × |
| Search | Type in the search box (real-time filtering) |
| View history | Click "History" button (top-right) |
| Restore history item | Hover → click the restore icon (↑ with bar) |
| Pin to top | Click the pin button (top-left) |
| Adjust opacity | Click S (top-right) → drag the slider |
| Exit | S → Exit button |

## License

MIT
