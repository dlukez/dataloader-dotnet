using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Represents a future awaitable asynchronous load operation.
    /// </summary>
    /// <remarks>
    /// This type allows continuations to be attached for a fetch
    /// that has not yet begun. The fetch can be triggered using
    /// the <see cref="Execute"/> method, which will then expose
    /// the result and run all continuations synchronously/inline.
    /// </remarks>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class Fetch<TKey, TValue> : ICriticalNotifyCompletion
    {
        private readonly FetchDelegate<TKey, TValue> _fetchDelegate;
        private readonly Queue<Action> _continuations = new Queue<Action>();
        private ILookup<TKey, TValue> _result;

        // TODO - add cancellation token support
        public Fetch(FetchDelegate<TKey, TValue> fetchDelegate)
        {
            _fetchDelegate = fetchDelegate;
        }

        public async Task ExecuteAsync(IEnumerable<TKey> keys)
        {
            _result = await _fetchDelegate(keys);

            // Allow continuations to be attached for the next run
            int remaining;
            lock (_continuations) remaining = _continuations.Count;
            while (remaining > 0)
            {
                Action continuation;
                lock (_continuations) continuation = _continuations.Dequeue();
                continuation.Invoke();
                remaining--;
            }
        }

        public Fetch<TKey, TValue> GetAwaiter()
        {
            return this;
        }

        public bool IsCompleted => false;

        public void OnCompleted(Action continuation)
        {
            UnsafeOnCompleted(continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            _continuations.Enqueue(continuation);
        }

        public ILookup<TKey, TValue> GetResult()
        {
            return _result;
        }

        public void Reset()
        {
            if (_continuations.Count > 0)
                throw new InvalidOperationException("Continuations exist");

            _result = null;
        }
    }
}
