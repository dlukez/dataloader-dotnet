using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataLoader
{
    internal class AsyncAutoResetEvent
    {
        private Queue<TaskCompletionSource<bool>> _waits = new Queue<TaskCompletionSource<bool>>();
        private bool _isSignaled;

        public AsyncAutoResetEvent(bool isSignaled = true)
        {
            _isSignaled = isSignaled;
        }

        public Task WaitAsync()
        {
            lock (_waits)
            {
                if (_isSignaled)
                {
                    _isSignaled = false;
                    return Task.CompletedTask;
                } 
                else 
                { 
                    var tcs = new TaskCompletionSource<bool>();
                    _waits.Enqueue(tcs);
                    return tcs.Task;
                } 
            } 
        }

        public bool Set() 
        { 
            TaskCompletionSource<bool> toRelease = null;
            
            lock (_waits) 
            { 
                if (_waits.Count > 0) toRelease = _waits.Dequeue();
                else if (!_isSignaled) _isSignaled = true;
            }

            if (toRelease != null)
            {
                toRelease.SetResult(true);
                return true;
            }
            
            return false;
        }
    }
}