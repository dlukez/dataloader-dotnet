using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// A custom synchronization context to work with the async/await infrastructure.
    /// </summary>
    internal class DataLoaderSynchronizationContext : SynchronizationContext
    {
        private DataLoaderContext _context;

        /// <summary>
        /// Creates a new <see cref="DataLoaderSnchronizationContext"/>.
        /// </summary>
        internal DataLoaderSynchronizationContext(DataLoaderContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Synchronously invokes the given callback before preparing to fire any new loaders.
        /// </summary>
        /// <remarks>
        /// <para>This method is called by the async/await infrastructure.</para>
        /// <para>Usually this method would run asynchronously, however we call it synchronously instead
        /// because we want to let all the continuation code finish before firing additional loaders.</para>
        /// <para>The callback will execute with this as the current synchronization context.</para>
        /// </remarks>
        public override void Post(SendOrPostCallback d, object state) => Execute(d, state);

        /// <summary>
        /// Synchronously invokes the given callback before preparing to fire any new loaders.
        /// </summary>
        /// <remarks>The callback will execute with this as the current synchronization context.</remarks>
        public override void Send(SendOrPostCallback d, object state) => Execute(d, state);

        private void Execute(SendOrPostCallback d, object state)
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
    }
}