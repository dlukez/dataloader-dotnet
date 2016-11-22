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
        public IDataLoader<TKey, TValue> GetDataLoader<TKey, TValue>(object key, FetchDelegate<TKey, TValue> fetch)
        {

            IDataLoader loader;
            lock (_loaderCache)
            {
                if (!_loaderCache.TryGetValue(key, out loader))
                {
                    loader = UsePrototypeDataLoader ? (IDataLoader) new DataLoader<TKey, TValue>(fetch, this) : new TaskBasedLoader<TKey, TValue>(fetch, this);
                    _loaderCache.Add(key, loader);
                }
            }
            return (IDataLoader<TKey, TValue>)loader;
        }

        /// <summary>
        /// Adds an item to the pending loader.
        /// </summary>
        internal void Enqueue(IDataLoader loader)
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
        public async Task FireAsync()
        {
            Trace.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} ({TaskScheduler.Current.GetType().Name}) - Firing loaders");
            var marker = 0;
            while (true)
            {
                IDataLoader loader;
                lock (_loadQueue)
                {
                    if (marker == 0)
                    {
                        Level++;
                        marker = _loadQueue.Count;
                    }
                    
                    if (_loadQueue.Count == 0) break;
                    loader = _loadQueue.Dequeue();
                }
                
                await loader.ExecuteAsync().ConfigureAwait(false);
                marker--;
            }
        }

        /// <summary>
        /// Runs the specified delegate then executes queued loaders.
        /// </summary>
        public static async Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            using (var loadCtx = new DataLoaderContextScope())
            {
                var result = func(loadCtx);
                await loadCtx.FireAsync().ConfigureAwait(false);
                return await result.ConfigureAwait(false);
            }
        }
    }

    /// <summary>Provides a task scheduler that runs tasks on the current thread.</summary>
    internal sealed class CurrentThreadTaskScheduler : TaskScheduler
    {
        /// <summary>Runs the provided Task synchronously on the current thread.</summary>
        /// <param name="task">The task to be executed.</param>
        protected override void QueueTask(Task task)
        {
            TryExecuteTask(task);
        }

        /// <summary>Runs the provided Task synchronously on the current thread.</summary>
        /// <param name="task">The task to be executed.</param>
        /// <param name="taskWasPreviouslyQueued">Whether the Task was previously queued to the scheduler.</param>
        /// <returns>True if the Task was successfully executed; otherwise, false.</returns>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return TryExecuteTask(task);
        }

        /// <summary>Gets the Tasks currently scheduled to this scheduler.</summary>
        /// <returns>An empty enumerable, as Tasks are never queued, only executed.</returns>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return Enumerable.Empty<Task>();
        }

        /// <summary>Gets the maximum degree of parallelism for this scheduler.</summary>
        public override int MaximumConcurrencyLevel { get { return 1; } }
    }
}