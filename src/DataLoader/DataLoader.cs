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
    public class DataLoader<TKey, TValue> : IDataLoader<TKey, TValue>
    {
        private readonly Fetch<TKey, TValue> _fetch;

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
        public DataLoader(FetchDelegate<TKey, TValue> fetchDelegate)
        {
            _fetch = new Fetch<TKey, TValue>(fetchDelegate);
        }

        /// <summary>
        /// Creates a new <see cref="DataLoader"/> bound to the specified context.
        /// </summary>
        public DataLoader(FetchDelegate<TKey, TValue> fetchDelegate, DataLoaderContext context) : this(fetchDelegate)
        {
            SetContext(context);
        }

        /// <summary>
        /// Loads a <typeparamref name="TValue"/> with the specified <typeparamref name="TKey"/>.
        /// </summary>
        public async Task<IEnumerable<TValue>> LoadAsync(TKey key)
        {
            lock (_keys)
            {
                _keys.Add(key);
                if (!IsScheduled)
                    ScheduleForExecution();
            }

            var lookup = await _fetch;
            //Trace.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} ({TaskScheduler.Current.GetType().Name}) - {new String(' ', Context.Level * 2)}Completing key {key} ({lookup[key].Count()} results)");
            return lookup[key];
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
            IsScheduled = false;
            TKey[] keys;
            lock (_keys)
            {
                keys = new TKey[_keys.Count];
                _keys.CopyTo(keys);
                _keys.Clear();
            }
            return Task.Run(() => _fetch.Execute(keys));
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