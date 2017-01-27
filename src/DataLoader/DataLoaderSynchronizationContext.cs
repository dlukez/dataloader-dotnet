using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    internal class DataLoaderSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object state)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Post callback executing");
            d(state);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Send callback executing");
            d(state);
        }
    }
}