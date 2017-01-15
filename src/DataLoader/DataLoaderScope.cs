using System;

namespace DataLoader
{
    public class DataLoaderScope : IDisposable
    {
        private DataLoaderContext _thisCtx;
        private DataLoaderContext _prevCtx;

        public DataLoaderScope() : this(new DataLoaderContext())
        {
        }

        public DataLoaderScope(DataLoaderContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            _thisCtx = context;
            _prevCtx = DataLoaderContext.Current;
            DataLoaderContext.SetCurrentContext(_thisCtx);
        }

        public DataLoaderContext Context => _thisCtx;

        public void Dispose()
        {
            if (_thisCtx != DataLoaderContext.Current)
                throw new InvalidOperationException("This context for this scope does not match the current context");

            if (!_thisCtx.IsRunning) _thisCtx.Start();
            DataLoaderContext.SetCurrentContext(_prevCtx);
        }
    }
}