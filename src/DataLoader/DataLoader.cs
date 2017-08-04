using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Represents a pending data load operation.
    /// </summary>
    public interface IDataLoader
    {
        Task ExecuteAsync();
    }

    /// <summary>
    /// Wraps an arbitrary query and integrates it into the loading chain.
    /// </summary>
    public interface IDataLoader<T> : IDataLoader
    {
        Task<T> LoadAsync();
    }

    /// <summary>
    /// Collects and loads keys in batches.
    /// </summary>
    public interface IDataLoader<TKey, TReturn> : IDataLoader
    {
        Task<TReturn> LoadAsync(TKey key);
    }

    /// <summary>
    /// Wraps an arbitrary query and integrates it into the loading chain.
    /// </summary>
    public class BasicDataLoader<T> : IDataLoader<T> where T : class
    {
        private readonly object _lock = new object();
        private readonly DataLoaderContext _boundContext;
        private readonly Func<Task<T>> _fetchDelegate;
        private TaskCompletionSource<T> _source;

        /// <summary>
        /// Creates a new <see cref="BasicDataLoader{T}"/>.
        /// </summary>
        public BasicDataLoader(Func<Task<T>> fetchDelegate) : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="BasicDataLoader{T}"/> bound to the specified context.
        /// </summary>
        internal BasicDataLoader(Func<Task<T>> fetchDelegate, DataLoaderContext boundContext)
        {
            _fetchDelegate = fetchDelegate;
            _boundContext = boundContext;
        }

        /// <summary>
        /// Gets the context visible to the loader which is either the loader is
        /// bound to if available, otherwise the current ambient context.
        /// </summary>
        /// <seealso cref="DataLoaderContext.Current"/>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Current;

        /// <summary>
        /// Loads data using the configured fetch delegate.
        /// </summary>
        public Task<T> LoadAsync()
        {
            var shouldEnqueue = false;
            if (_source == null)
            {
                var next = new TaskCompletionSource<T>();
                shouldEnqueue = Interlocked.CompareExchange(ref _source, next, null) == null;
            }

            var task = _source.Task;

            if (shouldEnqueue)
                Context.EnqueueLoader(this);

            return task;
        }

        /// <summary>
        /// Executes the fetch delegate and resolves the promise.
        /// </summary>
        public Task ExecuteAsync()
        {
            var tcs = Interlocked.Exchange(ref _source, null);
            if (tcs == null) return Task.CompletedTask;

            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Fetching basic");
            return _fetchDelegate().ContinueWith(
                Complete,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                Context.Scheduler);

            void Complete(Task<T> task)
            {
                Context.TriggerNext();
                tcs.SetResult(task.Result);
            }
        }
    }

    /// <summary>
    /// Collects and loads keys in batches.
    /// </summary>
    public class BatchDataLoader<TKey, TReturn> : IDataLoader<TKey, TReturn> where TReturn : class
    {
        private readonly object _lock = new object();
        private readonly DataLoaderContext _boundContext;
        private readonly Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> _fetchDelegate;
        private readonly ConcurrentDictionary<TKey, Task<TReturn>> _cache = new ConcurrentDictionary<TKey, Task<TReturn>>();
        private volatile ConcurrentDictionary<TKey, TaskCompletionSource<TReturn>> _batch;

        /// <summary>
        /// Creates a new <see cref="BatchDataLoader{TKey,TReturn}"/>.
        /// </summary>
        public BatchDataLoader(Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> fetchDelegate)
            : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="BatchDataLoader{TKey,TReturn}"/> bound to the specified context.
        /// </summary>
        internal BatchDataLoader(Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> fetchDelegate, DataLoaderContext context)
        {
            _boundContext = context;
            _fetchDelegate = fetchDelegate;
        }

        /// <summary>
        /// Gets the context the loader is bound to, otherwise the current ambient context.
        /// </summary>
        /// <seealso cref="DataLoaderContext.Current"/>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Current;

        /// <summary>
        /// Loads some data corresponding to the given key.
        /// </summary>
        /// <remarks>
        /// Each requested key is collected into a batch so that they can be fetched in a single call.
        /// When data for a key is loaded, it will be cached and used to fulfil any subsequent requests for the same key.
        /// </remarks>
        public Task<TReturn> LoadAsync(TKey key)
        {
            return _cache.GetOrAdd(key, _ =>
            {
                var nextBatch = _batch;
                var shouldEnqueue = false;
                if (nextBatch == null)
                {
                    nextBatch = new ConcurrentDictionary<TKey, TaskCompletionSource<TReturn>>();
                    shouldEnqueue = Interlocked.CompareExchange(ref _batch, nextBatch, null) == null;
                }

                var tcs = new TaskCompletionSource<TReturn>();
                nextBatch[key] = tcs;

                if (shouldEnqueue)
                    Context.EnqueueLoader(this);

                return tcs.Task;
            });
        }

        /// <summary>
        /// Fetches the current batch and resolves previously handed out promises.
        /// </summary>
        public Task ExecuteAsync()
        {
            var batch = Interlocked.Exchange(ref _batch, null);
            if (batch == null) return Task.CompletedTask;

            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Fetching batch ({batch.Count} keys)");
            return _fetchDelegate(batch.Keys).ContinueWith(
                Complete,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                Context.Scheduler);

            void Complete(Task<Dictionary<TKey, TReturn>> task)
            {
                Context.TriggerNext();
                foreach (var kvp in batch)
                {
                    task.Result.TryGetValue(kvp.Key, out var value);
                    kvp.Value.SetResult(value);
                }
            }
        }
    }
}
