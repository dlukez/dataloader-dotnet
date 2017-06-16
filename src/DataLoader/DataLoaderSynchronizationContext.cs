using System;
using System.Threading;

namespace DataLoader
{
    /// <summary>
    /// Re-triggers the processing of loaders after awaiting a non-loader task.
    /// </summary>
    internal class DataLoaderSynchronizationContext : SynchronizationContext
    {
        private readonly DataLoaderContext _loadCtx;

        public DataLoaderSynchronizationContext(DataLoaderContext loadCtx)
        {
            _loadCtx = loadCtx;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            SynchronizationContext.SetSynchronizationContext(this);
            d(state);
            if (!_loadCtx._isProcessing) _loadCtx.Process();
        }
    }

    /// <summary>
    /// Temporarily switches out the current DataLoaderContext and SynchronizationContext until disposed.
    /// </summary>
    internal class SynchronizationContextSwitcher : IDisposable
    {
        private readonly SynchronizationContext _prevSyncCtx;

        public SynchronizationContextSwitcher(DataLoaderSynchronizationContext syncCtx)
        {
            _prevSyncCtx = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(syncCtx);
        }

        public void Dispose()
        {
            SynchronizationContext.SetSynchronizationContext(_prevSyncCtx);
        }
    }
}