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
    public class DeferredResult<TKey, TReturn>
    {
        private readonly Queue<Action> _continuations = new Queue<Action>();
        private readonly TKey _key;
        private IEnumerable<TReturn> _value;

        internal DeferredResult(TKey key)
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
        public struct DataLoaderResultAwaiter : ICriticalNotifyCompletion
        {
            private DeferredResult<TKey, TReturn> _deferred;

            internal DataLoaderResultAwaiter(DeferredResult<TKey, TReturn> deferred)
            {
                _deferred = deferred;
            }

            public bool IsCompleted => _deferred._value != null;

            public IEnumerable<TReturn> GetResult() => _deferred._value;

            [SecuritySafeCritical]
            public void OnCompleted(Action continuation) => _deferred._continuations.Enqueue(continuation);

            [SecurityCritical]
            public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);
        }
    }
}