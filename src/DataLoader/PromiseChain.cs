using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace DataLoader
{
    internal class PromiseChain
    {
        private readonly TaskCompletionSource<object> _completion = new TaskCompletionSource<object>();
        private readonly AsyncAutoResetEvent _signal = new AsyncAutoResetEvent();
        private int _pendingCount;
        private Task _tail;

        /// <summary>
        /// Creates a new <see cref="PromiseChain"/>.
        /// </summary>
        public PromiseChain()
        {
            _tail = _signal.WaitAsync();
        }

        /// <summary>
        /// Completes when execution has reached the end of the chain.
        /// </summary>
        public Task Completion => _completion.Task;

        /// <summary>
        /// Indicates whether this chain is currently being executed.
        /// </summary>
        public bool IsExecuting => _pendingCount > 0;

        /// <summary>
        /// Begins executing the tasks in the chain.
        /// </summary>
        public void Trigger()
        {
            _tail = _tail.ContinueWith(async delegate
            {
                Console.WriteLine($"Pending count = {_pendingCount}");
                if (_pendingCount == 0) _completion.SetResult(null);
                else await _signal.WaitAsync().ConfigureAwait(false);
            }).Unwrap();

            _signal.Set();
        }

        /// <summary>
        /// Appends a callback to run after the last promise in the chain has been fulfilled.
        /// </summary>
        public void Append(Action action)
        {
            Append(() => { action(); return Task.CompletedTask; });
        }

        /// <summary>
        /// Appends a callback to run after the last promise in the chain has been fulfilled.
        /// </summary>
        public void Append(Func<Task> func)
        {
            _tail = ContinueWith(func);
        }

        /// <summary>
        /// Returns a continuation that runs after the last task in the chain.
        /// </summary>
        private async Task ContinueWith(Func<Task> func)
        {
            Interlocked.Increment(ref _pendingCount);
            await _tail.ConfigureAwait(false);
            await func().ConfigureAwait(false);
            Interlocked.Decrement(ref _pendingCount);
        }
    }
}