using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DataLoader
{
    internal sealed class DataLoaderTaskScheduler : TaskScheduler
    {
        private readonly BufferBlock<Task> _taskBuffer;
        private DataLoaderContext _context;

        internal DataLoaderTaskScheduler(DataLoaderContext context)
        {
            _context = context;
            _taskBuffer = new BufferBlock<Task>();
            ProcessUntilComplete();
        }

        public async void ProcessUntilComplete()
        {
            while (await _taskBuffer.OutputAvailableAsync().ConfigureAwait(false))
            {
                // OutputAvailableAsync() should ensure there's always a task readily available to receive
                TryExecuteTask(_taskBuffer.Receive());
            }
        }

        public void Complete()
        {
            _taskBuffer.Complete();
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            if (_taskBuffer.TryReceiveAll(out var tasks))
            {
                foreach (var task in tasks)
                    _taskBuffer.SendAsync(task);

                return tasks;
            }

            return Enumerable.Empty<Task>();
        }

        protected override void QueueTask(Task task)
        {
            // Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Queueing task {task.Id}");
            _taskBuffer.SendAsync(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Inlining task {task.Id}");
            return TryExecuteTask(task);
        }

        public void Dispose()
        {
            if (!_taskBuffer.Completion.IsCompleted)
                _taskBuffer.Complete();
        }
    }
}
