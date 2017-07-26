using System;
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
        Task FetchAndCompleteAsync();
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
        private BasicDataLoaderResult _result;

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
        public async Task<T> LoadAsync()
        {
            BasicDataLoaderResult result;

            lock (_lock)
            {
                if (_result == null)
                {
                    _result = new BasicDataLoaderResult();
                    Context.Enqueue(this);
                }

                result = _result;
            }

            return await result;
        }

        /// <summary>
        /// Executes the fetch delegate and resolves the promise.
        /// </summary>
        public Task FetchAndCompleteAsync()
        {
            BasicDataLoaderResult result;

            lock (_lock)
            {
                result = _result;
                _result = null;
            }

            return _fetchDelegate().ContinueWith(
                t => result.Complete(t.Result),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                Context.TaskScheduler);
        }

        /// <summary>
        /// Represents the result of a basic load operation.
        /// </summary>
        private class BasicDataLoaderResult : ICriticalNotifyCompletion
        {
            private readonly ConcurrentQueue<Action> _continuations = new ConcurrentQueue<Action>();
            private T _value;

            public BasicDataLoaderResult GetAwaiter() => this;

            public bool IsCompleted => _value != null;

            public T GetResult() => _value ?? throw new InvalidOperationException();

            [SecuritySafeCritical]
            public void OnCompleted(Action continuation) => _continuations.Enqueue(continuation);

            [SecurityCritical]
            public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);

            public void Complete(T value)
            {
                _value = value ?? throw new ArgumentNullException();
                while (_continuations.TryDequeue(out var continuation))
                    continuation();
            }
        }
    }

    /// <summary>
    /// Collects and loads keys in batches.
    /// </summary>
    /// <remarks>
    /// When a call is made to a load method, each key is stored and a task is handed back that represents the future result.
    /// The request is deferred until the loader is invoked, which can occur in the following circumstances:
    /// <list type="bullet">
    /// <item>The delegate supplied to <see cref="o:DataLoaderContext.Run"/> returned.</item>
    /// <item><see cref="DataLoaderContext.Process"/> was explicitly called on the governing <see cref="DataLoaderContext"/>.</item>
    /// <item>The loader was invoked explicitly by calling <see cref="FetchAndCompleteAsync"/>.</item>
    /// </list>
    /// </remarks>
    public class BatchDataLoader<TKey, TReturn> : IDataLoader<TKey, TReturn> where TReturn : class
    {
        private readonly object _lock = new object();
        private readonly DataLoaderContext _boundContext;
        private readonly Func<IEnumerable<TKey>, Task<IDictionary<TKey, TReturn>>> _fetchDelegate;

        private readonly ConcurrentDictionary<TKey, TReturn> _cache = new ConcurrentDictionary<TKey, TReturn>();
        private Batch _batch;

        /// <summary>
        /// Creates a new <see cref="BatchDataLoader{TKey,TReturn}"/>.
        /// </summary>
        public BatchDataLoader(Func<IEnumerable<TKey>, Task<IDictionary<TKey, TReturn>>> fetchDelegate) : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="BatchDataLoader{TKey,TReturn}"/> bound to the specified context.
        /// </summary>
        internal BatchDataLoader(Func<IEnumerable<TKey>, Task<IDictionary<TKey, TReturn>>> fetchDelegate, DataLoaderContext context)
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
        public async Task<TReturn> LoadAsync(TKey key)
        {
            if (_cache.TryGetValue(key, out var task)) return task;

            BatchItem result;

            lock (_lock)
            {
                if (_batch == null)
                {
                    _batch = new Batch();
                    Context.Enqueue(this);
                }

                result = _batch.Add(key);
            }

            return _cache[key] = await result;
        }

        /// <summary>
        /// Fetches the current batch and resolves previously handed out promises.
        /// </summary>
        public Task FetchAndCompleteAsync()
        {
            Batch batch;
            lock (_lock)
            {
                if (_batch == null)
                    return Task.CompletedTask;

                batch = _batch;
                _batch = null;
            }

            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Fetching batch ({batch.Count} keys)");
            return _fetchDelegate(batch.Keys).ContinueWith(
                t =>
                {
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Completing batch ({batch.Count} keys)");
                    batch.Complete(t.Result);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                Context.TaskScheduler);
        }

        /// <summary>
        /// Contains a batch of keys to be fetched and provides methods to complete them asynchronously.
        /// </summary>
        private class Batch
        {
            private readonly ConcurrentBag<TKey> _keys = new ConcurrentBag<TKey>();
            private readonly ConcurrentQueue<Action> _continuations = new ConcurrentQueue<Action>();
            private IDictionary<TKey, TReturn> _value;

            public int Count => _keys.Count;

            public IEnumerable<TKey> Keys => _keys.AsEnumerable();

            public BatchItem Add(TKey key)
            {
                _keys.Add(key);
                return new BatchItem(key, this);
            }

            internal bool IsCompleted => _value != null;

            internal TReturn GetResult(TKey key)
            {
                if (_value == null)
                    throw new InvalidOperationException();

                return _value[key];
            }

            internal void OnCompleted(Action continuation) => _continuations.Enqueue(continuation);

            public void Complete(IDictionary<TKey, TReturn> value)
            {
                _value = value ?? throw new ArgumentNullException();
                while (_continuations.TryDequeue(out var continuation))
                    continuation();
            }
        }

        /// <summary>
        /// Represents the result of a batch load operation for a given key.
        /// </summary>
        private struct BatchItem : ICriticalNotifyCompletion
        {
            private readonly TKey _key;
            private Batch _batch;

            internal BatchItem(TKey key, Batch batch)
            {
                _key = key;
                _batch = batch;
            }

            public BatchItem GetAwaiter() => this;

            public bool IsCompleted => _batch.IsCompleted;

            public TReturn GetResult() => _batch.GetResult(_key);

            [SecuritySafeCritical]
            public void OnCompleted(Action continuation) => _batch.OnCompleted(continuation);

            [SecurityCritical]
            public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);
        }
    }
}
