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
    public class DataLoaderScope : IDisposable
    {
        private DataLoaderContext _thisCtx;
#if !NET45
        private DataLoaderContext _prevCtx;
#endif

        /// <summary>
        /// Creates a new scope with a new <see cref="DataLoaderContext"/>
        /// </summary>
        public DataLoaderScope() : this(new DataLoaderContext())
        {
        }

        /// <summary>
        /// Creates a new scope attached to the given <see cref="DataLoaderContext"/>.
        /// </summary>
        public DataLoaderScope(DataLoaderContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            _thisCtx = context;
#if !NET45
            _prevCtx = DataLoaderContext.Current;
            DataLoaderContext.SetCurrentContext(_thisCtx);
#endif
        }

        /// <summary>
        /// The context reflected by this scope. Contains data relevant to the current load operation.
        /// </summary>
        public DataLoaderContext Context => _thisCtx;

        /// <summary>
        /// Marks the end of this scope and the point at which pending loaders will be fired.
        /// </summary>
        public void Dispose()
        {
#if !NET45
            if (_thisCtx != DataLoaderContext.Current)
                throw new InvalidOperationException("This context for this scope does not match the current context");
            DataLoaderContext.SetCurrentContext(_prevCtx);
#endif
            if (!_thisCtx.IsRunning) _thisCtx.Start();
        }
    }
}