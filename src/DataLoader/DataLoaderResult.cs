using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Represents the result of a deferred load operation.
    /// </summary>
    public class DataLoaderResult<TKey, TReturn>
    {
        private readonly Queue<Action> _continuations = new Queue<Action>();
        private readonly TKey _key;
        private IEnumerable<TReturn> _value;

        internal DataLoaderResult(TKey key)
        {
            _key = key;
        }

        internal void Complete(ILookup<TKey, TReturn> lookup)
        {
            _value = lookup[_key];
            while (_continuations.Count > 0)
                _continuations.Dequeue().Invoke();
        }

        public TKey Key => _key;

        public IEnumerable<TReturn> Value => _value;

        public DataLoaderResultAwaiter GetAwaiter() => new DataLoaderResultAwaiter(this);

        /// <summary>
        /// Provides support for awaiting the result.
        /// </summary>
        public struct DataLoaderResultAwaiter : INotifyCompletion
        {
            private DataLoaderResult<TKey, TReturn> _deferred;

            internal DataLoaderResultAwaiter(DataLoaderResult<TKey, TReturn> deferred)
            {
                _deferred = deferred;
            }

            public bool IsCompleted => _deferred._value != null;

            public IEnumerable<TReturn> GetResult() => _deferred._value;

            public void OnCompleted(Action continuation) => _deferred._continuations.Enqueue(continuation);
        }
    }
}