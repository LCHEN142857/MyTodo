using System.ComponentModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MyToDo.App.Data;
using MyToDo.App.Services;
using MyToDo.App.Settings;
using MyToDo.App.ViewModels;

namespace MyToDo.App;

public partial class MainWindow : Window
{
    private readonly ISettingsStore _settingsStore;
    private readonly ITodoRepository _repository;
    private readonly ISingleInstanceService _instance;
    private MainViewModel? _subscribedViewModel;
    private Task? _persistTask;
    private readonly HashSet<TodoItemViewModel> _subscribedRows = [];

    public MainWindow(MainViewModel viewModel, AppSettings settings, ISettingsStore settingsStore, ITodoRepository repository, ISingleInstanceService instance)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settingsStore = settingsStore;
        _repository = repository;
        _instance = instance;
        Left = settings.Left;
        Top = settings.Top;
        Width = settings.Width;
        Height = settings.Height;
        Opacity = settings.Opacity;
        Topmost = settings.IsTopmost;
        Loaded += (_, _) => AttachRowHandlers();
        Unloaded += (_, _) => DetachRowHandlers();
    }

    public void ActivateFromSecondInstance()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
        Topmost = true;
        Topmost = false;
    }

    public Task PersistSettingsAsync() => _persistTask ??= _settingsStore.SaveAsync(new AppSettings(Left, Top, Width, Height, Opacity, Topmost));

    private void AttachRowHandlers()
    {
        if (DataContext is not MainViewModel vm) return;
        if (ReferenceEquals(_subscribedViewModel, vm)) return;
        DetachRowHandlers();
        _subscribedViewModel = vm;
        ((INotifyCollectionChanged)vm.VisibleItems).CollectionChanged += VisibleItems_CollectionChanged;
        foreach (var row in vm.VisibleItems) SubscribeRow(row);
    }

    private void VisibleItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var row in _subscribedRows.ToArray()) UnsubscribeRow(row);
            if (_subscribedViewModel is not null) foreach (var row in _subscribedViewModel.VisibleItems) SubscribeRow(row);
            return;
        }
        if (e.OldItems is not null) foreach (TodoItemViewModel row in e.OldItems) UnsubscribeRow(row);
        if (e.NewItems is not null) foreach (TodoItemViewModel row in e.NewItems) SubscribeRow(row);
    }

    private void SubscribeRow(TodoItemViewModel row)
    {
        if (_subscribedRows.Add(row)) row.RequestSelectAll += Row_RequestSelectAll;
    }

    private void UnsubscribeRow(TodoItemViewModel row)
    {
        if (_subscribedRows.Remove(row)) row.RequestSelectAll -= Row_RequestSelectAll;
    }

    private void DetachRowHandlers()
    {
        if (_subscribedViewModel is not null) ((INotifyCollectionChanged)_subscribedViewModel.VisibleItems).CollectionChanged -= VisibleItems_CollectionChanged;
        foreach (var row in _subscribedRows.ToArray()) UnsubscribeRow(row);
        _subscribedViewModel = null;
    }

    private void Row_RequestSelectAll(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (sender is not TodoItemViewModel row) return;
            var editor = FindEditor(row);
            editor?.Focus();
            editor?.SelectAll();
        });
    }

    private TextBox? FindEditor(TodoItemViewModel row)
    {
        var container = TodoList.ItemContainerGenerator.ContainerFromItem(row) as ListBoxItem;
        return container is null ? null : FindVisualChild<TextBox>(container);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (FindVisualChild<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.OriginalSource == this) DragMove(); }
    private void DragRegion_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is Grid) DragMove(); }
    private void NewTodoInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && DataContext is MainViewModel vm) { vm.CreateCommand.Execute(null); e.Handled = true; } }
    private void SettingsButton_Click(object sender, RoutedEventArgs e) => SettingsPopup.Visibility = SettingsPopup.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void PinButton_Click(object sender, RoutedEventArgs e) => Topmost = !Topmost;
    private void ExitButton_Click(object sender, RoutedEventArgs e) => Close();
    private void CompleteCheckBox_Click(object sender, RoutedEventArgs e) { if (sender is CheckBox box && box.DataContext is TodoItemViewModel row) row.CompleteCommand.Execute(null); }
    private void TodoText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TodoItemViewModel row) return;
        row.BeginEditCommand.Execute(null);
        Dispatcher.BeginInvoke(() =>
        {
            var editor = FindEditor(row);
            editor?.Focus();
            editor?.SelectAll();
        });
    }
    private void EditText_KeyDown(object sender, KeyEventArgs e) { if (sender is TextBox box && box.DataContext is TodoItemViewModel row) { if (e.Key == Key.Enter) { row.SaveEditCommand.Execute(null); e.Handled = true; } else if (e.Key == Key.Escape) { row.CancelEditCommand.Execute(null); e.Handled = true; } } }
    private void EditText_LostFocus(object sender, RoutedEventArgs e) { if (sender is TextBox box && box.DataContext is TodoItemViewModel row && row.IsEditing) row.SaveEditCommand.Execute(null); }
}
