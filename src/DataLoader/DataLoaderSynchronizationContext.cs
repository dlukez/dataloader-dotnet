using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Re-triggers the processing of loaders after awaiting a non-loader task.
    /// </summary>
    internal class DataLoaderSynchronizationContext : SynchronizationContext
    {
        private int _ops;

        public DataLoaderContext Context { get; }

        public DataLoaderSynchronizationContext(DataLoaderContext loadCtx)
        {
            Context = loadCtx;
        }

        public override SynchronizationContext CreateCopy()
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - CreateCopy called");
            return this;
        }

        public override void OperationStarted()
        {
            _ops++;
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Op started ({_ops} total)");
        }

        public override void OperationCompleted()
        {
            _ops--;
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Op completed ({_ops} remaining)");
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Send received (state = {state})");
            var task = new Task(() => d(state));
            task.RunSynchronously(Context._taskScheduler);
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Post received (state = {state})");
            Context._taskFactory.StartNew(() => d(state));
        }
    }

    /// <summary>
    /// Temporarily switches out the current SynchronizationContext until disposed.
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