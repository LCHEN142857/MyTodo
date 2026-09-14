using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace MyToDo.App.Services;

public sealed class SingleInstanceService : ISingleInstanceService
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(2);
    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly Mutex _mutex;
    private readonly SemaphoreSlim _activation = new(0, 1);
    private readonly CancellationTokenSource _dispose = new();
    private readonly ManualResetEventSlim _releaseMutex = new();
    private readonly object _lifecycleLock = new();
    private Task? _listener;
    private TaskCompletionSource<bool>? _acquisition;
    private Thread? _ownershipThread;
    private bool _disposed;

    public SingleInstanceService(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Instance name is required.", nameof(name));
        _mutexName = "Local\\" + name;
        _pipeName = name;
        _mutex = new Mutex(false, _mutexName);
    }

    public Task<bool> TryAcquireAsync()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_acquisition is not null) return _acquisition.Task;

            _acquisition = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ownershipThread = new Thread(AcquireAndOwnMutex) { IsBackground = true };
            _ownershipThread.Start();
            return _acquisition.Task;
        }
    }

    public async Task<bool> WaitForActivationAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _dispose.Token);
            await _activation.WaitAsync(linked.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (ObjectDisposedException) { return false; }
    }

    public async Task SignalExistingAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var deadline = DateTime.UtcNow + ConnectionTimeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Impersonation);
                using var timeout = new CancellationTokenSource(deadline - DateTime.UtcNow);
                await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
                await client.WriteAsync(new byte[] { 1 }).ConfigureAwait(false);
                await client.FlushAsync().ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) { return; }
            catch (IOException) { await Task.Delay(25).ConfigureAwait(false); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _dispose.Cancel();
        _releaseMutex.Set();
        if (_listener is not null)
        {
            try { await _listener.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }
        if (_ownershipThread is not null)
        {
            await Task.Run(_ownershipThread.Join).ConfigureAwait(false);
        }
        else
        {
            _mutex.Dispose();
        }
        _activation.Dispose();
        _dispose.Dispose();
        _releaseMutex.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                if (server.IsConnected) { _activation.Release(); }
            }
            catch (OperationCanceledException) { return; }
            catch (IOException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (SemaphoreFullException) { }
        }
    }

    private void AcquireAndOwnMutex()
    {
        var acquired = false;
        try
        {
            try { acquired = _mutex.WaitOne(ConnectionTimeout); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired)
            {
                _acquisition!.TrySetResult(false);
                return;
            }

            _listener = Task.Run(() => ListenAsync(_dispose.Token));
            _acquisition!.TrySetResult(true);
            _releaseMutex.Wait();
            _mutex.ReleaseMutex();
        }
        catch (ObjectDisposedException)
        {
            _acquisition!.TrySetResult(false);
        }
        finally
        {
            _mutex.Dispose();
        }
    }
}
