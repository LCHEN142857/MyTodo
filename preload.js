const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('api', {
  getData: () => ipcRenderer.invoke('get-data'),
  saveData: (data) => ipcRenderer.invoke('save-data', data),
  setOpacity: (value) => ipcRenderer.invoke('set-opacity', value),
  setPinned: (pinned) => ipcRenderer.invoke('set-pinned', pinned),
  exitApp: () => ipcRenderer.invoke('exit-app')
});
