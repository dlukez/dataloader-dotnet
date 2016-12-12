using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    public abstract class DataLoaderBase<TKey, TValue> : IDataLoader<TKey, TValue>
    {
        /// <summary>
        /// Gets the keys to retrieve in the next batch.
        /// </summary>
        public IEnumerable<TKey> Keys => _keys.AsEnumerable();
        private HashSet<TKey> _keys = new HashSet<TKey>();

        /// <summary>
        /// Gets the context to which this loader is bound.
        /// </summary>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Default;
        private DataLoaderContext _boundContext;

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

            return GetTaskInternal(key);
        }

        /// <summary>
        /// Gets a Task{IEnumerable{<typeparamref name="TValue"/>}} representing the result for
        /// the specified <typeparamref name="TKey"/>.
        /// </summary>
        protected abstract Task<IEnumerable<TValue>> GetTaskInternal(TKey key);

        /// <summary>
        /// Executes the fetch delegate for the batch.
        /// </summary>
        public virtual Task ExecuteAsync()
        {
            TKey[] keys;
            lock (_keys)
            {
                keys = _keys.ToArray();
                _keys.Clear();
                IsScheduled = false;
            }
            
            return ExecuteAsyncInternal(keys);
        }

        /// <summary>
        /// Makes the data request.
        /// </summary>
        protected abstract Task ExecuteAsyncInternal(IEnumerable<TKey> keys);

        /// <summary>
        /// Indicates whether this loader is waiting to fire.
        /// </summary>
        public bool IsScheduled { get; private set; }

        /// <summary>
        /// Notifies the loader's manager that this loader is ready to fire.
        /// </summary>
        private void ScheduleForExecution()
        {
            Context.AddToQueue(this);
            IsScheduled = true;
        }
    }
}