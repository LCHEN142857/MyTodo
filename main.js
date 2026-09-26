const { app, BrowserWindow, ipcMain, screen } = require('electron');
const path = require('path');
const fs = require('fs');

app.setAppUserModelId('com.mytodo.app');

let mainWindow = null;
let dataFilePath = null;

const DEFAULT_OPACITY = 0.92;

function getDataFilePath() {
  const userDataPath = app.getPath('userData');
  return path.join(userDataPath, 'mytodo-data.json');
}

function loadData() {
  try {
    if (fs.existsSync(dataFilePath)) {
      const raw = fs.readFileSync(dataFilePath, 'utf-8');
      const data = JSON.parse(raw);
      if (!data.todos) data.todos = [];
      if (!data.history) data.history = [];
      if (!data.settings) data.settings = {};
      return data;
    }
  } catch (err) {
    console.error('Failed to load data:', err);
  }
  return {
    todos: [],
    history: [],
    settings: { opacity: DEFAULT_OPACITY, isPinned: false }
  };
}

function saveData(data) {
  try {
    const dir = path.dirname(dataFilePath);
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }
    fs.writeFileSync(dataFilePath, JSON.stringify(data, null, 2), 'utf-8');
  } catch (err) {
    console.error('Failed to save data:', err);
  }
}

function createWindow() {
  const data = loadData();
  const opacity = data.settings?.opacity ?? DEFAULT_OPACITY;
  const isPinned = data.settings?.isPinned ?? false;

  const primaryDisplay = screen.getPrimaryDisplay();
  const { width, height } = primaryDisplay.workAreaSize;

  mainWindow = new BrowserWindow({
    width: 340,
    height: 520,
    minWidth: 260,
    minHeight: 320,
    x: width - 360,
    y: height - 540,
    frame: false,
    transparent: true,
    resizable: true,
    show: false,
    skipTaskbar: false,
    icon: path.join(__dirname, 'src', 'icon.ico'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false
    }
  });

  if (isPinned) {
    mainWindow.setAlwaysOnTop(true, 'floating');
  } else {
    mainWindow.setAlwaysOnTop(false);
  }

  mainWindow.loadFile(path.join(__dirname, 'src', 'index.html'));

  mainWindow.once('ready-to-show', () => {
    mainWindow.show();
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });

  if (process.argv.includes('--dev')) {
    mainWindow.webContents.openDevTools({ mode: 'detach' });
  }
}

app.whenReady().then(() => {
  dataFilePath = getDataFilePath();
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

ipcMain.handle('get-data', () => {
  return loadData();
});

ipcMain.handle('save-data', (event, data) => {
  saveData(data);
});

ipcMain.handle('set-opacity', (event, value) => {
  const data = loadData();
  data.settings = data.settings || {};
  data.settings.opacity = value;
  saveData(data);
});

ipcMain.handle('set-pinned', (event, pinned) => {
  if (mainWindow) {
    if (pinned) {
      mainWindow.setAlwaysOnTop(true, 'floating');
    } else {
      mainWindow.setAlwaysOnTop(false);
    }
  }
  const data = loadData();
  data.settings = data.settings || {};
  data.settings.isPinned = pinned;
  saveData(data);
});

ipcMain.handle('exit-app', () => {
  app.quit();
});
