using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

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
        private readonly bool _isAutoResetEnabled;
        private ILookup<TKey, TValue> _result;

        // TODO - add cancellation token support
        public Fetch(FetchDelegate<TKey, TValue> fetchDelegate, bool resetAutomatically = true)
        {
            _fetchDelegate = fetchDelegate;
            _isAutoResetEnabled = resetAutomatically;
        }

        public void Execute(IEnumerable<TKey> keys)
        {
            _result = _fetchDelegate(keys);

            // Allow continuations to be attached for the next run
            var remaining = _continuations.Count;
            while (remaining > 0)
            {
                _continuations.Dequeue().Invoke();
                remaining--;
            }

            // Allows this object to be reused
            if (_isAutoResetEnabled)
                _result = null;
        }

        public Fetch<TKey, TValue> GetAwaiter()
        {
            return this;
        }

        // If auto-reset is turned on, always return false so that any `await`s
        // that occur in a continuation are queued for the next fetch
        // rather than immediately returning the result from the current one.
        public bool IsCompleted => !_isAutoResetEnabled && _result != null;

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
