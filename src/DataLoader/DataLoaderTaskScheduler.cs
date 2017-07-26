using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    internal sealed class DataLoaderTaskScheduler : TaskScheduler
    {
        public override int MaximumConcurrencyLevel => 1;

        private readonly DataLoaderContext _context;
        private readonly ConcurrentQueue<Task> _taskQueue;
        private bool _isProcessing;

        internal DataLoaderTaskScheduler(DataLoaderContext context)
        {
            _context = context;
            _taskQueue = new ConcurrentQueue<Task>();
        }

        internal void StartProcessing()
        {
            _isProcessing = true;

            while (_taskQueue.TryDequeue(out var task))
                TryExecuteTask(task);

            _isProcessing = false;
        }

        protected override void QueueTask(Task task)
        {
            _taskQueue.Enqueue(task);
            if (!_isProcessing)
                StartProcessing();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return TryExecuteTask(task);
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _taskQueue.ToArray();
        }
    }
}