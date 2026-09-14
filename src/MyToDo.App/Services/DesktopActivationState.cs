namespace MyToDo.App.Services;

public sealed class DesktopActivationState
{
    public bool IsRestorePending { get; private set; }

    public void MarkDetached() => IsRestorePending = true;

    public void ScheduleRestore(Action<Action> schedule, Action restore)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(restore);
        if (IsRestorePending) schedule(() => Restore(restore));
    }

    public void Restore(Action restore)
    {
        ArgumentNullException.ThrowIfNull(restore);
        if (!IsRestorePending) return;
        IsRestorePending = false;
        restore();
    }
}
