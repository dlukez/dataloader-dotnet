using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    public abstract class FetchBase<T> : INotifyCompletion
    {
        private Queue<Action> _continuations = new Queue<Action>();

        private T _result;

        public bool IsCompleted => _result != null;

        public FetchBase<T> GetAwaiter() => this;

        public void OnCompleted(Action continuation) => _continuations.Enqueue(continuation);

        public T GetResult()
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - GetResult {typeof(T).Name}");
            return _result;
        }



        protected Task Complete(T result)
        {
            _result = result;
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Complete {_continuations.Count} continuations");
            return Task.Run(() =>
            {
                while (_continuations.Count > 0)
                    _continuations.Dequeue().Invoke();
            });
        }
    }

    public class Fetch<T> : FetchBase<T>
    {
        private Func<T> _fetchDelegate;
        
        public Fetch(Func<T> fetchDelegate)
        {
            _fetchDelegate = fetchDelegate ?? throw new ArgumentNullException();
        }

        public Task Execute() => Complete(_fetchDelegate());
    }
    
    public class AsyncFetch<T> : FetchBase<T>
    {
        private Func<Task<T>> _fetchDelegate;

        public AsyncFetch(Func<Task<T>> fetchDelegate)
        {
            _fetchDelegate = fetchDelegate ?? throw new ArgumentNullException();
        }

        public async Task<Task> ExecuteAsync() => Complete(await _fetchDelegate().ConfigureAwait(false));
    }
}