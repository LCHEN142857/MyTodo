using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyToDo.App.Services;

public interface ISingleInstanceService : IAsyncDisposable
{
    Task<bool> TryAcquireAsync();
    Task<bool> WaitForActivationAsync(CancellationToken cancellationToken);
    Task SignalExistingAsync();
}
