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
        /// Creates a scope for a new <see cref="DataLoaderContext"/>.
        /// </summary>
        /// <remarks>The context will be completed when the scope disposed of.</remarks>
        public DataLoaderScope() : this(new DataLoaderContext(), true)
        {
        }

        /// <summary>
        /// Creates a scope for a new <see cref="DataLoaderContext"/>.
        /// </summary>
        /// <param name="completeOnDisposal">Configures whether to complete the context when the scope is disposed of. </param>
        public DataLoaderScope(bool completeOnDisposal) : this(new DataLoaderContext(), completeOnDisposal)
        {
        }

        /// <summary>
        /// Creates a scope for the given <see cref="DataLoaderContext"/>.
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
        /// The context contained in this scope. Contains data relevant to the current load operation.
        /// </summary>
        public DataLoaderContext Context => _loadCtx;

        /// <summary>
        /// Represents the scope's completion.
        /// </summary>
        public Task Completion => _loadCtx.Completion;

        /// <summary>
        /// Marks the end of this scope and the point at which pending loaders will be fired.
        /// </summary>
        public void Dispose()
        {
#if !NET45
            if (_loadCtx != DataLoaderContext.Current)
                throw new InvalidOperationException("This context for this scope does not match the current context");
#endif
            if (_completeOnDisposal) _loadCtx.Complete();
            DataLoaderContext.SetCurrentContext(_prevLoadCtx);
        }
    }
}