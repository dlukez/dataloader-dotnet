using System;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace DataLoader
{
    internal class PromiseChain
    {
        private readonly AsyncAutoResetEvent _next = new AsyncAutoResetEvent();
        private readonly AsyncCountdownEvent _countdown = new AsyncCountdownEvent(0);
        private readonly TaskCompletionSource<object> _completionSource = new TaskCompletionSource<object>();
        private int _pendingCount;

        /// <summary>
        /// Creates a new <see cref="PromiseChain"/>.
        /// </summary>
        public PromiseChain()
        {
        }

        /// <summary>
        /// Completes when execution has reached the end of the chain.
        /// </summary>
        public Task Completion => _completionSource.Task;

        /// <summary>
        /// Indicates whether this chain is currently being executed.
        /// </summary>
        public bool IsExecuting => _pendingCount > 0;

        /// <summary>
        /// Begins executing the tasks in the chain.
        /// </summary>
        public async void Trigger()
        {
            Console.WriteLine($"Triggering chain (items: {_countdown.CurrentCount})");
            _next.Set();
            Console.WriteLine($"Chain triggered (remaining: {_countdown.CurrentCount})");

            await _next.WaitAsync().ConfigureAwait(false);
            _completionSource.SetResult(null);
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
        public async void Append(Func<Task> func)
        {
            Console.WriteLine($"Adding link to chain (total: {_countdown.CurrentCount+1})");
            _countdown.AddCount();
            
            await _next.WaitAsync().ConfigureAwait(false);
            Console.WriteLine($"Running delegate...");
            await func().ConfigureAwait(false);

            Console.WriteLine($"Triggering next item...");
            _next.Set();

            Console.WriteLine($"Signaling the countdown (total: {_countdown.CurrentCount-1})");
            _countdown.Signal();
        }
    }
}