using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    public class DataLoaderSynchronizationContext : SynchronizationContext
    {
        private readonly DataLoaderContext _context;

        public DataLoaderSynchronizationContext(DataLoaderContext context)
        {
            _context = context;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Post received");
            _context.Enqueue(() => d(state));
        }
    }

    public class SynchronizationContextSwitcher : IDisposable
    {
        private readonly SynchronizationContext _prevCtx;

        public SynchronizationContextSwitcher(SynchronizationContext syncCtx)
        {
            _prevCtx = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(syncCtx);
        }

        public void Dispose()
        {
            SynchronizationContext.SetSynchronizationContext(_prevCtx);
        }
    }
}