using System.Collections.Concurrent;
using System.Threading;

namespace DataLoader
{
    public class DataLoaderContextThread
    {
        private Thread _thread;
        private BlockingCollection<IDataLoader> _queue;

        public DataLoaderContextThread()
        {
            _thread = new Thread(ThreadLoop);
            _queue = new BlockingCollection<IDataLoader>();
        }

        public void QueueDataLoader(IDataLoader loader)
        {
            if (!_queue.IsAddingCompleted) _queue.Add(loader);
        }

        private void ThreadLoop()
        {
            foreach (var loader in _queue.GetConsumingEnumerable())
            {
                // do something with a loader...
                loader.ExecuteAsync().Wait();
            }
        }
    }
}