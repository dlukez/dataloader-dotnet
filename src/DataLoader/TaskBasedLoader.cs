using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Collects keys into a batch to be executed in a single query.
    /// </summary>
    public class TaskBasedLoader<TKey, TValue> : IDataLoader<TKey, TValue>
    {
        private readonly FetchDelegate<TKey, TValue> _fetchDelegate;
        private TaskCompletionSource<ILookup<TKey, TValue>> _deferred;
        private Task<ILookup<TKey, TValue>> _last;

        /// <summary>
        /// Gets the keys to retrieve in the next batch.
        /// </summary>
        public IEnumerable<TKey> Keys => new ReadOnlyCollection<TKey>(_keys.ToList());
        private readonly HashSet<TKey> _keys = new HashSet<TKey>();

        /// <summary>
        /// Gets the context to which this loader is bound.
        /// </summary>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Default;
        private DataLoaderContext _boundContext;

        /// <summary>
        /// Creates a new <see cref="DataLoader"/>.
        /// </summary>
        public TaskBasedLoader(FetchDelegate<TKey, TValue> fetchDelegate)
        {
            _fetchDelegate = fetchDelegate;
            Reset();
        }

        /// <summary>
        /// Creates a new <see cref="DataLoader"/> bound to the specified context.
        /// </summary>
        public TaskBasedLoader(FetchDelegate<TKey, TValue> fetchDelegate, DataLoaderContext context) : this(fetchDelegate)
        {
            SetContext(context);
        }

        /// <summary>
        /// Loads a <typeparamref name="TValue"/> with the specified <typeparamref name="TKey"/>.
        /// </summary>
        public Task<IEnumerable<TValue>> LoadAsync(TKey key)
        {
            lock (_keys)
            {
                _keys.Add(key);
                if (!IsScheduled)
                    ScheduleForExecution();
            }

            var tcs = new TaskCompletionSource<IEnumerable<TValue>>();
            _last = _last.ContinueWith(_ =>
            {
                tcs.SetResult(_.Result[key]);
                return _.Result;
            }, TaskContinuationOptions.ExecuteSynchronously);
            return tcs.Task;
        }

        /// <summary>
        /// Binds the data loader to a particular loading context.
        /// </summary>
        /// <param name="context"></param>
        public void SetContext(DataLoaderContext context)
        {
            if (IsScheduled)
                throw new InvalidOperationException("Cannot set context - loader already primed");

            _boundContext = context;
        }

        /// <summary>
        /// Executes the fetch delegate for the batch.
        /// </summary>
        public Task ExecuteAsync()
        {
            ILookup<TKey, TValue> result;
            lock (_keys)
            {
                IsScheduled = false;
                result = _fetchDelegate(_keys);
                _keys.Clear();
            }

            _deferred.SetResult(result);
            return _last.ContinueWith(_ => Reset());
        }

        private void Reset()
        {
            _deferred = new TaskCompletionSource<ILookup<TKey, TValue>>();
            _last = _deferred.Task;
        }

        /// <summary>
        /// Indicates whether this loader is waiting to fire.
        ///</summary>
        public bool IsScheduled { get; private set; }

        private void ScheduleForExecution()
        {
            Context.Enqueue(this);
            IsScheduled = true;
        }
    }
}