using System.Threading;
using BachRadio.Rpc.Models;

namespace BachRadio.Rpc.Services;

public class StatusBuffer : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly TaskCompletionSource _firstDataTcs = new();
    private MusicStatus? _currentStatus;
    private DateTime _lastUpdate;
    private bool _hasData;

    public Task WaitForFirstData => _firstDataTcs.Task;

    public void Update(MusicStatus status)
    {
        _lock.EnterWriteLock();
        try
        {
            _currentStatus = status;
            _lastUpdate = DateTime.Now;
            if (!_hasData)
            {
                _hasData = true;
                _firstDataTcs.TrySetResult(); // Signals listeners
            }
        }
        finally { _lock.ExitWriteLock(); }
    }

    public (MusicStatus? status, DateTime lastUpdate, bool hasData) Get()
    {
        _lock.EnterReadLock();
        try { return (_currentStatus, _lastUpdate, _hasData); }
        finally { _lock.ExitReadLock(); }
    }

    public void Dispose() => _lock.Dispose();
}