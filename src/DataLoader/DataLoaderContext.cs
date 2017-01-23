using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Defines a context for <see cref="DataLoader"/> instances.
    /// </summary>
    public sealed class DataLoaderContext
    {
        private int _nextCacheId = 1;
        private PromiseChain _chain = new PromiseChain();
        private readonly ConcurrentDictionary<object, IDataLoader> _cache =
            new ConcurrentDictionary<object, IDataLoader>();

        /// <summary>
        /// Creates a new <see cref="DataLoaderContext"/>. 
        /// </summary>
        public DataLoaderContext()
        {   
        }        

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TKey, TReturn> GetLoader<TKey, TReturn>(object key, FetchDelegate<TKey, TReturn> fetch)
        {
            return (IDataLoader<TKey, TReturn>) _cache.GetOrAdd(key, _ =>
                new DataLoader<TKey, TReturn>(fetch, this));
        }

        /// <summary>
        /// Queues a loader to be executed.
        /// </summary>
        internal void AddPendingLoader(IDataLoader loader)
        {
            if (!_cache.Values.Contains(loader))
                _cache.TryAdd(_nextCacheId++, loader);

            _chain.Append(loader.ExecuteAsync);
        }

        /// <summary>
        /// Indicates whether loaders have been started.
        /// </summary>
        public bool IsLoading => _chain.IsExecuting;

        /// <summary>
        /// Starts firing pending loaders to fulfil any previously handed out promises.
        /// </summary>
        public void StartLoading()
        {
            _chain.Trigger();
        }

        /// <summary>
        /// Represents whether this context has finished executing loaders.
        /// </summary>
        public Task Completion => _chain.Completion;

#region Ambient context

#if NET45

        internal static DataLoaderContext Current => null;

        internal static void SetCurrentContext(DataLoaderContext context) {}

#else

        private static readonly AsyncLocal<DataLoaderContext> LocalContext = new AsyncLocal<DataLoaderContext>();

        /// <summary>
        /// Represents ambient data local to the current load operation.
        /// <seealso cref="DataLoaderContext.Run{T}(Func{Task{T}})"/>
        /// </summary>
        public static DataLoaderContext Current => LocalContext.Value;

        /// <summary>
        /// Sets the <see cref="DataLoaderContext"/> visible from the <see cref="DataLoaderContext.Current"/>  property.
        /// </summary>
        /// <param name="context"></param>
        internal static void SetCurrentContext(DataLoaderContext context)
        {
            LocalContext.Value = context;
        }

#endif

#endregion

        /// <summary>
        /// Runs code within a new loader context before firing any pending
        /// <see cref="DataLoader{TKey,TReturn}">DataLoader</see> instances.
        /// </summary>
        public static Task<T> Run<T>(Func<Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            using (var scope = new DataLoaderScope())
            {
                var task = func();
                if (task == null) throw new InvalidOperationException("No task provided");
                return task;
            }
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending
        /// <see cref="DataLoader{TKey,TReturn}"/> requests.
        /// </summary>
        public static Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            using (var scope = new DataLoaderScope())
            {
                var task = func(scope.Context);
                if (task == null) throw new InvalidOperationException("No task provided.");
                return task;
            }
        }
    }
}