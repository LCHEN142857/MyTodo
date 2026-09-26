const state = {
  todos: [],
  history: [],
  view: 'todo',
  search: '',
  opacity: 0.92,
  isPinned: false,
  settingsOpen: false,
  editingId: null
};

const $ = (id) => document.getElementById(id);

const el = {
  todoInput: $('todo-input'),
  addBtn: $('add-btn'),
  searchInput: $('search-input'),
  todoList: $('todo-list'),
  historyList: $('history-list'),
  todoView: $('todo-view'),
  historyView: $('history-view'),
  addContainer: $('add-container'),
  todoListContainer: $('todo-list-container'),
  historyListContainer: $('history-list-container'),
  pinBtn: $('pin-btn'),
  historyBtn: $('history-btn'),
  todoBtn: $('todo-btn'),
  settingsBtn: $('settings-btn'),
  settingsPopup: $('settings-popup'),
  opacitySlider: $('opacity-slider'),
  opacityValue: $('opacity-value'),
  exitBtn: $('exit-btn'),
  countLabel: $('count-label'),
  cleanBtn: $('clean-btn'),
  historyLabel: $('history-label')
};

function genId() {
  return Date.now().toString(36) + Math.random().toString(36).substr(2, 5);
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

function debounce(fn, delay) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), delay);
  };
}

const saveData = debounce(async () => {
  await window.api.saveData({
    todos: state.todos,
    history: state.history,
    settings: { opacity: state.opacity, isPinned: state.isPinned }
  });
}, 400);

function getFilteredTodos() {
  if (!state.search) return state.todos;
  const q = state.search.toLowerCase();
  return state.todos.filter(t => t.text.toLowerCase().includes(q));
}

function getFilteredHistory() {
  if (!state.search) return state.history;
  const q = state.search.toLowerCase();
  return state.history.filter(h => h.text.toLowerCase().includes(q));
}

function applyBgOpacity(value) {
  document.documentElement.style.setProperty('--bg-opacity', value);
}

function renderTodoList() {
  const filtered = getFilteredTodos();

  if (filtered.length === 0) {
    el.todoList.innerHTML = '<div class="empty-state">' +
      (state.search ? 'No matching todos' : 'No todos yet') + '</div>';
  } else {
    el.todoList.innerHTML = filtered.map(todo => `
      <div class="todo-item" data-id="${todo.id}">
        <label class="checkbox-wrapper">
          <input type="checkbox" class="checkbox-input" data-id="${todo.id}" ${todo.completed ? 'checked' : ''}>
          <span class="checkbox-custom"></span>
        </label>
        <span class="todo-text ${todo.completed ? 'completed' : ''}" data-action="edit" data-id="${todo.id}">${escapeHtml(todo.text)}</span>
        <button class="delete-btn" data-id="${todo.id}" data-action="delete-todo" title="Delete">&times;</button>
      </div>
    `).join('');
  }

  el.countLabel.textContent = `${state.todos.length} To Do`;
  updateScrollbarState(el.todoListContainer);
}

function renderHistoryList() {
  const filtered = getFilteredHistory();

  if (filtered.length === 0) {
    el.historyList.innerHTML = '<div class="empty-state">' +
      (state.search ? 'No matching history' : 'No history') + '</div>';
  } else {
    el.historyList.innerHTML = filtered.map(item => `
      <div class="history-item" data-id="${item.id}">
        <span class="history-text">${escapeHtml(item.text)}</span>
        <div class="history-actions">
          <button class="restore-btn" data-id="${item.id}" data-action="restore" title="Restore">
            <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
              <line x1="4" y1="4" x2="20" y2="4"/>
              <path d="M12 8 L12 20 M7 13 L12 8 L17 13"/>
            </svg>
          </button>
          <button class="delete-btn" data-id="${item.id}" data-action="delete-history" title="Delete">&times;</button>
        </div>
      </div>
    `).join('');
  }

  el.countLabel.textContent = `${state.history.length} histories`;
  updateScrollbarState(el.historyListContainer);
}

function addTodo() {
  const text = el.todoInput.value.trim();
  if (!text) return;
  state.todos.push({
    id: genId(),
    text,
    completed: false,
    createdAt: new Date().toISOString()
  });
  el.todoInput.value = '';
  renderTodoList();
  saveData();
}

function toggleTodo(id) {
  const todo = state.todos.find(t => t.id === id);
  if (!todo) return;
  todo.completed = true;

  const itemEl = el.todoList.querySelector(`.todo-item[data-id="${id}"]`);
  if (itemEl) {
    const textEl = itemEl.querySelector('.todo-text');
    const checkboxEl = itemEl.querySelector('.checkbox-input');
    if (textEl) textEl.classList.add('completed');
    if (checkboxEl) checkboxEl.checked = true;
  }

  setTimeout(() => {
    state.todos = state.todos.filter(t => t.id !== id);
    state.history.unshift({
      id: todo.id,
      text: todo.text,
      completedAt: new Date().toISOString()
    });
    renderTodoList();
    if (state.view === 'history') renderHistoryList();
    saveData();
  }, 500);
}

function deleteTodo(id) {
  state.todos = state.todos.filter(t => t.id !== id);
  if (state.editingId === id) state.editingId = null;
  renderTodoList();
  saveData();
}

function startEditTodo(id) {
  const todo = state.todos.find(t => t.id === id);
  if (!todo || todo.completed) return;
  if (state.editingId === id) return;

  state.editingId = id;

  const itemEl = el.todoList.querySelector(`.todo-item[data-id="${id}"]`);
  if (!itemEl) return;

  const textEl = itemEl.querySelector('.todo-text');
  if (!textEl) return;

  const input = document.createElement('input');
  input.type = 'text';
  input.className = 'todo-edit-input';
  input.value = todo.text;
  input.dataset.id = id;

  textEl.replaceWith(input);
  input.focus();
  input.select();

  function finishEdit(save) {
    if (state.editingId !== id) return;
    state.editingId = null;

    if (save) {
      const newText = input.value.trim();
      if (newText && newText !== todo.text) {
        todo.text = newText;
        saveData();
      }
    }

    renderTodoList();
  }

  input.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      finishEdit(true);
    } else if (e.key === 'Escape') {
      e.preventDefault();
      finishEdit(false);
    }
  });

  input.addEventListener('blur', () => {
    finishEdit(true);
  });
}

function restoreHistory(id) {
  const item = state.history.find(h => h.id === id);
  if (!item) return;
  state.history = state.history.filter(h => h.id !== id);
  state.todos.push({
    id: item.id,
    text: item.text,
    completed: false,
    createdAt: new Date().toISOString()
  });
  renderHistoryList();
  if (state.view === 'todo') renderTodoList();
  saveData();
}

function deleteHistory(id) {
  state.history = state.history.filter(h => h.id !== id);
  renderHistoryList();
  saveData();
}

function cleanHistory() {
  state.history = [];
  renderHistoryList();
  saveData();
}

function switchView(view) {
  state.view = view;
  state.search = '';
  el.searchInput.value = '';

  if (view === 'todo') {
    el.todoView.style.display = '';
    el.historyView.style.display = 'none';
    el.addContainer.style.display = '';
    el.historyBtn.style.display = '';
    el.todoBtn.style.display = 'none';
    el.pinBtn.style.display = '';
    el.historyLabel.style.display = 'none';
    el.cleanBtn.style.display = 'none';
    el.searchInput.placeholder = 'Search...';
    renderTodoList();
  } else {
    el.todoView.style.display = 'none';
    el.historyView.style.display = '';
    el.addContainer.style.display = 'none';
    el.historyBtn.style.display = 'none';
    el.todoBtn.style.display = '';
    el.pinBtn.style.display = 'none';
    el.historyLabel.style.display = '';
    el.cleanBtn.style.display = '';
    el.searchInput.placeholder = 'Search history...';
    renderHistoryList();
  }

  closeSettings();
}

function togglePin() {
  state.isPinned = !state.isPinned;
  el.pinBtn.classList.toggle('active', state.isPinned);
  window.api.setPinned(state.isPinned);
  saveData();
}

function toggleSettings() {
  if (state.settingsOpen) {
    closeSettings();
  } else {
    state.settingsOpen = true;
    el.settingsPopup.style.display = '';
  }
}

function closeSettings() {
  state.settingsOpen = false;
  el.settingsPopup.style.display = 'none';
}

function setOpacity(percent) {
  state.opacity = percent / 100;
  el.opacityValue.textContent = percent + '%';
  el.opacitySlider.value = percent;
  applyBgOpacity(state.opacity);
  saveData();
}

function onSearch() {
  state.search = el.searchInput.value;
  if (state.view === 'todo') {
    renderTodoList();
  } else {
    renderHistoryList();
  }
}

function updateScrollbarState(container) {
  const hasOverflow = container.scrollHeight > container.clientHeight;
  if (!hasOverflow) {
    container.classList.remove('show-scrollbar');
  }
}

function setupScrollbar(container) {
  let hideTimer = null;

  function clearHideTimer() {
    if (hideTimer) {
      clearTimeout(hideTimer);
      hideTimer = null;
    }
  }

  function startHideTimer() {
    clearHideTimer();
    hideTimer = setTimeout(() => {
      container.classList.remove('show-scrollbar');
      hideTimer = null;
    }, 3000);
  }

  container.addEventListener('mousemove', (e) => {
    const hasOverflow = container.scrollHeight > container.clientHeight;
    if (!hasOverflow) return;

    const rect = container.getBoundingClientRect();
    const distFromRight = rect.right - e.clientX;

    if (distFromRight < 34) {
      container.classList.add('show-scrollbar');
      clearHideTimer();
    } else {
      if (!hideTimer) {
        startHideTimer();
      }
    }
  });

  container.addEventListener('mouseleave', () => {
    startHideTimer();
  });

  container.addEventListener('scroll', () => {
    if (container.scrollHeight > container.clientHeight) {
      container.classList.add('show-scrollbar');
      clearHideTimer();
      startHideTimer();
    }
  });
}

function setupEventListeners() {
  el.addBtn.addEventListener('click', addTodo);

  el.todoInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      addTodo();
    }
  });

  el.searchInput.addEventListener('input', onSearch);

  el.todoList.addEventListener('change', (e) => {
    if (e.target.classList.contains('checkbox-input')) {
      const id = e.target.dataset.id;
      toggleTodo(id);
    }
  });

  el.todoList.addEventListener('click', (e) => {
    const deleteBtn = e.target.closest('[data-action="delete-todo"]');
    const editText = e.target.closest('[data-action="edit"]');

    if (deleteBtn) {
      deleteTodo(deleteBtn.dataset.id);
    } else if (editText) {
      e.stopPropagation();
      startEditTodo(editText.dataset.id);
    }
  });

  el.historyList.addEventListener('click', (e) => {
    const restoreBtn = e.target.closest('[data-action="restore"]');
    const deleteBtn = e.target.closest('[data-action="delete-history"]');
    if (restoreBtn) {
      restoreHistory(restoreBtn.dataset.id);
    } else if (deleteBtn) {
      deleteHistory(deleteBtn.dataset.id);
    }
  });

  el.historyBtn.addEventListener('click', () => switchView('history'));
  el.todoBtn.addEventListener('click', () => switchView('todo'));
  el.pinBtn.addEventListener('click', togglePin);
  el.settingsBtn.addEventListener('click', (e) => {
    e.stopPropagation();
    toggleSettings();
  });
  el.cleanBtn.addEventListener('click', cleanHistory);
  el.exitBtn.addEventListener('click', () => window.api.exitApp());

  el.opacitySlider.addEventListener('input', (e) => {
    setOpacity(parseInt(e.target.value));
  });

  window.addEventListener('pointerdown', (e) => {
    if (state.settingsOpen && !el.settingsPopup.contains(e.target) && !el.settingsBtn.contains(e.target)) {
      closeSettings();
    }
  });

  el.settingsPopup.addEventListener('pointerdown', (e) => {
    e.stopPropagation();
  });

  window.addEventListener('blur', () => {
    if (state.settingsOpen) {
      closeSettings();
    }
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      if (state.editingId) {
        return;
      }
      if (state.settingsOpen) {
        closeSettings();
      } else if (document.activeElement === el.todoInput) {
        el.todoInput.blur();
      } else if (document.activeElement === el.searchInput) {
        el.searchInput.value = '';
        state.search = '';
        onSearch();
        el.searchInput.blur();
      }
    }
  });

  setupScrollbar(el.todoListContainer);
  setupScrollbar(el.historyListContainer);
}

async function init() {
  const data = await window.api.getData();
  state.todos = data.todos || [];
  state.history = data.history || [];
  state.opacity = data.settings?.opacity ?? 0.92;
  state.isPinned = data.settings?.isPinned ?? false;

  el.pinBtn.classList.toggle('active', state.isPinned);

  const opacityPercent = Math.round(state.opacity * 100);
  el.opacitySlider.value = opacityPercent;
  el.opacityValue.textContent = opacityPercent + '%';
  applyBgOpacity(state.opacity);

  setupEventListeners();
  renderTodoList();
}

init();
