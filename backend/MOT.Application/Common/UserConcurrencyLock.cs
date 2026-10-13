using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MOT.Application.Common;

public static class UserConcurrencyLock
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public static async Task<IDisposable> LockAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var semaphore = _locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _semaphore.Release();
                _disposed = true;
            }
        }
    }
}
