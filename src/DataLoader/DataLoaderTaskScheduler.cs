using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// A custom task scheduler that executes tasks in the correct synchronization context.
    /// </summary>
    internal class DataLoaderTaskScheduler : TaskScheduler
    {
        private DataLoaderContext _context;

        /// <summary>
        /// Creates a new <see cref="DataLoaderTaskScheduler"/>.
        /// </summary>
        public DataLoaderTaskScheduler(DataLoaderContext context)
        {
            _context = context;
        }

        protected override IEnumerable<Task> GetScheduledTasks() => throw new NotImplementedException();

        protected override void QueueTask(Task task)
        {
            _context.SyncContext.Post(_ => TryExecuteTask(task), null);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => TryExecuteTask(task);
    }
}