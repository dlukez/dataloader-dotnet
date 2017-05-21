using System;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Represents the beginning and end of a data loader block or operation.
    /// </summary>
    /// <remarks>
    /// When a new scope is created, <see cref="DataLoaderContext.Current"/> is updated to point to the <see cref="DataLoaderContext">context</see>
    /// created or given in the constructor. When the scope is disposed of, the <code>Current</code> property is reset to its previous value. 
    /// </remarks>
    public class DataLoaderScope : IDisposable
    {
        private readonly bool _completeOnDisposal;
        private readonly DataLoaderContext _loadCtx;
        private readonly DataLoaderContext _prevLoadCtx;

        /// <summary>
        /// Creates a scope with a new <see cref="DataLoaderContext"/>.
        /// </summary>
        /// <remarks>The context will be completed when the scope disposed of.</remarks>
        public DataLoaderScope() : this(new DataLoaderContext(), true)
        {
        }

        /// <summary>
        /// Creates a scope with a new <see cref="DataLoaderContext"/>.
        /// </summary>
        /// <param name="completeOnDisposal">Configures whether to complete the context when the scope is disposed of. </param>
        public DataLoaderScope(bool completeOnDisposal) : this(new DataLoaderContext(), completeOnDisposal)
        {
        }

        /// <summary>
        /// Creates a scope with the given <see cref="DataLoaderContext"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="completeOnDisposal">Configures whether to complete the context when the scope is disposed of. </param>
        internal DataLoaderScope(DataLoaderContext context, bool completeOnDisposal)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            _completeOnDisposal = completeOnDisposal;
            _loadCtx = context;
            _prevLoadCtx = DataLoaderContext.Current;
            DataLoaderContext.SetCurrentContext(_loadCtx);
        }

        /// <summary>
        /// The context for in this scope. Contains data relevant to the current load operation.
        /// </summary>
        public DataLoaderContext Context => _loadCtx;

        /// <summary>
        /// Marks the end of this scope and the point at which pending loaders will be fired.
        /// </summary>
        public Task CompleteAsync()
        {
#if FEATURE_ASYNCLOCAL
            if (_loadCtx != DataLoaderContext.Current)
                throw new InvalidOperationException("This scope's context is no longer current");
#endif

            return _loadCtx.CompleteAsync();
        }

        public void Dispose()
        {
#if FEATURE_ASYNCLOCAL
            if (_loadCtx != DataLoaderContext.Current)
                throw new InvalidOperationException("This scope's context is no longer current");
#endif

            DataLoaderContext.SetCurrentContext(_prevLoadCtx);
        }
    }
}