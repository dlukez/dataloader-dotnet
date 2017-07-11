using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    internal class DataLoaderTaskScheduler : TaskScheduler
    {
        private ConcurrentQueue<Task> _tasks = new ConcurrentQueue<Task>();
        private bool _isProcessing;

        public override int MaximumConcurrencyLevel => 1;

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _tasks.ToArray();
        }

        protected override void QueueTask(Task task)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Queueing task {task.Id}");
            _tasks.Enqueue(task);
            if (!_isProcessing)
            {
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Starting processing");
                StartProcessing();
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return TryExecuteTask(task);
        }

        private void StartProcessing()
        {
            _isProcessing = true;
            try
            {
                while (_tasks.TryDequeue(out var task))
                {
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Executing task {task.Id}");
                    TryExecuteTask(task);
                }
            }
            finally { _isProcessing = false; }
        }
    }
}