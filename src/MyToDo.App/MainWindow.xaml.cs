using System.ComponentModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using CheckBox = System.Windows.Controls.CheckBox;
using Brush = System.Windows.Media.Brush;
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
    private readonly IDesktopWindowService _desktopWindowService;
    private readonly ScrollbarVisibilityController _scrollbarVisibility;
    private readonly DispatcherTimer _shellRefreshTimer;
    private MainViewModel? _subscribedViewModel;
    private Task? _persistTask;
    private readonly HashSet<TodoItemViewModel> _subscribedRows = [];
    private ScrollViewer? _todoScrollViewer;

    public MainWindow(MainViewModel viewModel, AppSettings settings, ISettingsStore settingsStore, ITodoRepository repository, ISingleInstanceService instance)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settingsStore = settingsStore;
        _repository = repository;
        _instance = instance;
        _desktopWindowService = new DesktopWindowService();
        _scrollbarVisibility = new ScrollbarVisibilityController();
        _scrollbarVisibility.PropertyChanged += (_, _) => UpdateScrollbarVisibility();
        _shellRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _shellRefreshTimer.Tick += (_, _) => ReapplyDesktopAttachment();

        var visibleSettings = _desktopWindowService.EnsureVisible(settings, GetDisplayBounds());
        Left = visibleSettings.Left;
        Top = visibleSettings.Top;
        Width = visibleSettings.Width;
        Height = visibleSettings.Height;
        Opacity = visibleSettings.Opacity;
        Topmost = visibleSettings.IsTopmost;
        UpdatePinVisualState();
        SourceInitialized += Window_SourceInitialized;
        Loaded += Window_Loaded;
        Unloaded += (_, _) => DetachRowHandlers();
        Closed += (_, _) =>
        {
            _shellRefreshTimer.Stop();
            _scrollbarVisibility.Dispose();
        };
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

    private static IReadOnlyList<DisplayBounds> GetDisplayBounds() => System.Windows.Forms.Screen.AllScreens
        .Select(screen => new DisplayBounds(screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height))
        .ToArray();

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        ApplyPlacement();
        _shellRefreshTimer.Start();
    }

    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        AttachRowHandlers();
        Dispatcher.BeginInvoke(() =>
        {
            _todoScrollViewer = FindVisualChild<ScrollViewer>(TodoList);
            UpdateScrollableHeight();
            UpdateScrollbarVisibility();
        });
    }

    private void ApplyPlacement()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (Topmost) _desktopWindowService.SetTopmost(handle, true);
        else _desktopWindowService.AttachToDesktop(handle);
    }

    private void ReapplyDesktopAttachment()
    {
        if (!Topmost) _desktopWindowService.AttachToDesktop(new WindowInteropHelper(this).Handle);
    }

    private void UpdatePinVisualState()
    {
        if (PinButton is null || PinIcon is null) return;
        var action = Topmost ? "Disable" : "Enable";
        PinButton.ToolTip = $"{action} always on top";
        System.Windows.Automation.AutomationProperties.SetName(PinButton, $"{action} always on top");
        PinIcon.Fill = (Brush)FindResource(Topmost ? "PrimaryBrush" : "TextBrush");
    }

    private void UpdateScrollableHeight()
    {
        if (_todoScrollViewer is not null) _scrollbarVisibility.SetScrollableHeight(_todoScrollViewer.ScrollableHeight);
    }

    private void UpdateScrollbarVisibility()
    {
        if (_todoScrollViewer is null) return;
        _todoScrollViewer.VerticalScrollBarVisibility = _scrollbarVisibility.IsVisible ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;
    }

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
    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        var handle = new WindowInteropHelper(this).Handle;
        _desktopWindowService.SetTopmost(handle, Topmost);
        if (!Topmost) _desktopWindowService.AttachToDesktop(handle);
        UpdatePinVisualState();
    }
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
    private void TodoList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(TodoList);
        if (TodoList.ActualWidth - point.X <= 28) _scrollbarVisibility.NotifyActivity();
    }
    private void TodoList_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => _scrollbarVisibility.NotifyActivity();
    private void TodoList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        _scrollbarVisibility.SetScrollableHeight(Math.Max(0, e.ExtentHeight - e.ViewportHeight));
        _scrollbarVisibility.NotifyActivity();
    }
}
