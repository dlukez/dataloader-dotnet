using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    internal class DataLoaderSynchronizationContext : SynchronizationContext
    {
        private DataLoaderContext _context;

        internal DataLoaderSynchronizationContext(DataLoaderContext context)
        {
            _context = context;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            var prevCtx = SynchronizationContext.Current;
            var wasCurrentContext = prevCtx == this;
            if (!wasCurrentContext) SynchronizationContext.SetSynchronizationContext(this);
            try
            {
                d(state);
                _context.FlushLoadersOnThread();
            }
            finally { if (!wasCurrentContext) SynchronizationContext.SetSynchronizationContext(prevCtx); }
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - Send start");
            d(state);
            _context.FlushLoadersOnThread();
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - Send end");
        }
    }
}