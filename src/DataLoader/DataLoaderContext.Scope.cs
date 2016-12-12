using System;

namespace DataLoader
{
    public class DataLoaderContextScope : DataLoaderContext, IDisposable
    {
        private DataLoaderContext _previousContext;

        public DataLoaderContextScope()
        {
            _previousContext = DataLoaderContext.Current;
            DataLoaderContext.SetContext(this);
        }

        public void Dispose()
        {
            if (DataLoaderContext.Current != this)
                throw new InvalidOperationException($"{nameof(DataLoaderContext.Current)} has changed");

            DataLoaderContext.SetContext(_previousContext);
        }
    }
}