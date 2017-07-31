using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    internal sealed class DataLoaderTaskScheduler : TaskScheduler
    {
        private BlockingCollection<Task> _tasks = new BlockingCollection<Task>();
        private ConcurrentStack<Task> _queue = new ConcurrentStack<Task>();
        private DataLoaderContext _context;
        private int _runningTasks;

        internal DataLoaderTaskScheduler(DataLoaderContext context)
        {
            _context = context;
        }

        public void ProcessUntilComplete()
        {
            foreach (var task in _tasks.GetConsumingEnumerable())
            {
                TrackAndTryExecuteTask(task);
            }
        }

        public void Complete()
        {
            _tasks.CompleteAdding();
        }

        internal void ProcessOnCurrentThread()
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Processing tasks ({_queue.Count} in queue)");
            while (_queue.TryPop(out var task))
            {
                // Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Dequeued task {task.Id}");
                TrackAndTryExecuteTask(task);
            }
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Done processing tasks");
        }

        internal int Count => _queue.Count;

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _queue.ToArray();
        }

        protected override void QueueTask(Task task)
        {
            // Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Queueing task {task.Id} ({_runningTasks} running)");
            _queue.Push(task);

            // if (_runningTasks == 0)
            //     ProcessOnCurrentThread();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Inlining task {task.Id}");
            return TrackAndTryExecuteTask(task);
        }

        private bool TrackAndTryExecuteTask(Task task)
        {
            Interlocked.Increment(ref _runningTasks);
            try
            {
                // Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Executing task {task.Id} ({_runningTasks} in progress)");
                return TryExecuteTask(task);
            }
            finally { Interlocked.Decrement(ref _runningTasks); }
        }
    }
}