using System.ComponentModel;
using System.Windows.Threading;

namespace MyToDo.App.Services;

public interface IScrollActivityScheduler
{
    IDisposable Schedule(TimeSpan delay, Action callback);
}

public sealed class ScrollbarVisibilityController : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan HideDelay = TimeSpan.FromSeconds(3);
    private readonly IScrollActivityScheduler _scheduler;
    private IDisposable? _hideTimer;
    private bool _canScroll = true;
    private bool _isVisible;

    public ScrollbarVisibilityController() : this(new DispatcherScrollActivityScheduler()) { }

    public ScrollbarVisibilityController(IScrollActivityScheduler scheduler) => _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsVisible => _isVisible;

    public void SetScrollableHeight(double scrollableHeight)
    {
        _canScroll = scrollableHeight > 0;
        if (!_canScroll)
        {
            _hideTimer?.Dispose();
            _hideTimer = null;
            SetVisible(false);
        }
    }

    public void NotifyActivity()
    {
        if (!_canScroll) return;
        SetVisible(true);
        _hideTimer?.Dispose();
        _hideTimer = _scheduler.Schedule(HideDelay, Hide);
    }

    public void Dispose() => _hideTimer?.Dispose();

    private void Hide()
    {
        _hideTimer = null;
        SetVisible(false);
    }

    private void SetVisible(bool value)
    {
        if (_isVisible == value) return;
        _isVisible = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
    }

    private sealed class DispatcherScrollActivityScheduler : IScrollActivityScheduler
    {
        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            var timer = new DispatcherTimer { Interval = delay };
            EventHandler? tick = null;
            tick = (_, _) =>
            {
                timer.Stop();
                timer.Tick -= tick;
                callback();
            };
            timer.Tick += tick;
            timer.Start();
            return new DelegateDisposable(() =>
            {
                timer.Stop();
                timer.Tick -= tick;
            });
        }
    }

    private sealed class DelegateDisposable(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
