using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Defines  for <see cref="DataLoader"/> instances.
    /// </summary>
    public partial class DataLoaderContext
    {
        /// <summary>
        /// Represents the default loader context.
        /// </summary>
        public static readonly DataLoaderContext Default = new DataLoaderContext();

        private readonly Queue<IDataLoader> _loadQueue;
        private readonly Dictionary<object, IDataLoader> _loaderCache;

        public DataLoaderContext()
        {
            _loadQueue = new Queue<IDataLoader>();
            _loaderCache = new Dictionary<object, IDataLoader>();
        }

        public bool UsePrototypeDataLoader { get; set; }

        /// <summary>
        /// Retrieves a <see cref="DataLoader"/> for the given key, creating and storing one if none is found.
        /// </summary>
        public IDataLoader<TKey, TValue> GetCachedLoader<TKey, TValue>(object key, FetchDelegate<TKey, TValue> fetch)
        {
            IDataLoader loader;
            lock (_loaderCache)
            {
                if (!_loaderCache.TryGetValue(key, out loader))
                {
                    loader = UsePrototypeDataLoader
                        ? (IDataLoader) new FetchBasedDataLoader<TKey, TValue>(fetch, this)
                        : (IDataLoader) new TaskBasedDataLoader<TKey, TValue>(fetch, this);
                    _loaderCache.Add(key, loader);
                }
            }
            return (IDataLoader<TKey, TValue>) loader;
        }

        /// <summary>
        /// Queues the loader for execution.
        /// </summary>
        internal void AddToQueue(IDataLoader loader)
        {
            lock (_loadQueue) _loadQueue.Enqueue(loader);
        }

        /// <summary>
        /// Represents the current depth at which we are processing
        /// loaders in the loader hierarchy.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Executes queued loaders serially until there are none remaining.
        /// </summary>
        public async Task Start()
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} ({TaskScheduler.Current.GetType().Name}) - Firing loaders");

            while (true)
            {
                IDataLoader loader;
                lock (_loadQueue)
                {
                    if (_loadQueue.Count == 0) break;
                    loader = _loadQueue.Dequeue();
                }
                await loader.ExecuteAsync();
            }
        }

        /// <summary>
        /// Runs the specified delegate then executes queued loaders.
        /// </summary>
        public static async Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func, bool usePrototypeDataLoader = false)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            Console.WriteLine("Current task: " + Task.CurrentId);

            using (var loadCtx = new DataLoaderContextScope())
            {
                loadCtx.UsePrototypeDataLoader = usePrototypeDataLoader;
                var result = func(loadCtx);
                await loadCtx.Start();
                return await result;
            }
        }
    }
}