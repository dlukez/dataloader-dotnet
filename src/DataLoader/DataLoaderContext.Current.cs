using System.Runtime.Remoting.Messaging;

namespace DataLoader
{
    public partial class DataLoaderContext
    {
#if FEATURE_ASYNC_LOCAL
        private static readonly AsyncLocal<DataLoaderContext> _localContext = new AsyncLocal<DataLoaderContext>();

        /// <summary>
        /// Represents the loader context current to this asynchronous flow.
        /// </summary>
        public static DataLoaderContext Current => _localContext.Value;

        /// <summary>
        /// Sets the <see cref="DataLoaderContext"/> visible via the <see cref="Current"/> Current property.
        /// </summary>
        /// <param name="context"></param>
        public static void SetContext(DataLoaderContext context)
        {
            _localContext.Value = context;
        }
#else
        private const string CallContextStorageId = "DataLoaderContext.Current";

        /// <summary>
        /// Represents the loader context current to this asynchronous flow.
        /// </summary>
        public static DataLoaderContext Current
        {
            get
            {
                var wrapper = CallContext.LogicalGetData(CallContextStorageId);
                if (wrapper is DataLoaderContextData)
                    return ((DataLoaderContextData)wrapper).Context;
                return null;
            }
        }

        /// <summary>
        /// Sets the <see cref="DataLoader.DataLoaderContext"/> visible via the <see cref="Current"/> Current property.
        /// </summary>
        /// <param name="context"></param>
        public static void SetContext(global::DataLoader.DataLoaderContext context)
        {
            CallContext.LogicalSetData(CallContextStorageId, new DataLoaderContextData(context));
        }

        private struct DataLoaderContextData
        {
            internal readonly DataLoaderContext Context;
            public DataLoaderContextData(DataLoaderContext context)
            {
                Context = context;
            }
        }
#endif
    }
}
