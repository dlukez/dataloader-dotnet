using System;

namespace DataLoader
{
    /// <summary>
    /// Represents the beginning and end of a data loader block or operation.
    /// When disposed, pending loaders will be triggered.
    /// </summary>
    /// <remarks>
    /// When a new scope is created, the <see cref="DataLoaderContext.Current"/> property
    /// is updated to point to the attached context.
    /// </remarks>
    internal class DataLoaderScope : IDisposable
    {
        private readonly DataLoaderContext _loadCtx;
        private readonly DataLoaderContext _prevLoadCtx;

        /// <summary>
        /// Creates a new scope for a new <see cref="DataLoaderContext"/>
        /// </summary>
        public DataLoaderScope() : this(new DataLoaderContext())
        {
        }

        /// <summary>
        /// Creates a new scope for the given <see cref="DataLoaderContext"/>.
        /// </summary>
        public DataLoaderScope(DataLoaderContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            _loadCtx = context;
            _prevLoadCtx = DataLoaderContext.Current;
            DataLoaderContext.SetCurrentContext(_loadCtx);
        }

        /// <summary>
        /// The context contained in this scope. Contains data relevant to the current load operation.
        /// </summary>
        public DataLoaderContext Context => _loadCtx;

        /// <summary>
        /// Marks the end of this scope and the point at which pending loaders will be fired.
        /// </summary>
        public void Dispose()
        {
#if !NET45
            if (_loadCtx != DataLoaderContext.Current)
                throw new InvalidOperationException("This context for this scope does not match the current context");
#endif
            DataLoaderContext.SetCurrentContext(_prevLoadCtx);
        }
    }
}