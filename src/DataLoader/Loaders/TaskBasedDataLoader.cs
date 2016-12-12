using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Collects keys into a batch to be executed in a single query.
    /// </summary>
    public class TaskBasedDataLoader<TKey, TValue> : DataLoaderBase<TKey, TValue>
    {
        private FetchDelegate<TKey, TValue> _fetchDelegate;
        private TaskCompletionSource<ILookup<TKey, TValue>> _source;
        private Task<ILookup<TKey, TValue>> _chain;

        /// <summary>
        /// Creates a new <see cref="TaskBasedDataLoader{TKey,TValue}"/>.
        /// </summary>
        public TaskBasedDataLoader(FetchDelegate<TKey, TValue> fetchDelegate)
        {
            _fetchDelegate = fetchDelegate;
            Reset();
        }
        
        /// <summary>
        /// Creates a new <see cref="TaskBasedDataLoader{TKey,TValue}"/> bound to the specified context.
        /// </summary>
        public TaskBasedDataLoader(FetchDelegate<TKey, TValue> fetchDelegate, DataLoaderContext context)
        {
            _fetchDelegate = fetchDelegate;
            SetContext(context);
            Reset();
        }

        protected override Task<IEnumerable<TValue>> GetTaskInternal(TKey key)
        {
            var tcs = new TaskCompletionSource<IEnumerable<TValue>>();
            _chain = _chain.ContinueWith(task =>
            {
                if (task.IsFaulted) tcs.SetException(task.Exception.InnerExceptions);
                else if (task.IsCanceled) tcs.SetCanceled();
                else tcs.SetResult(task.Result[key]);
                return task.Result;
            }, TaskContinuationOptions.ExecuteSynchronously);
            return tcs.Task;
        }

        protected override async Task ExecuteAsyncInternal(IEnumerable<TKey> keys)
        {
            try
            {
                var result = await _fetchDelegate(keys);
                _source.SetResult(result);
            }
            catch (Exception ex) { _source.SetException(ex); }
            finally { Reset(); }
        }

        private void Reset()
        {
            _source = new TaskCompletionSource<ILookup<TKey, TValue>>();
            _chain = _source.Task;
        }
    }
}