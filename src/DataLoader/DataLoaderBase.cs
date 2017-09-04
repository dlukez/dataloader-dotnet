using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Provides the base functionality for implementing a data loader.
    /// </summary>
    public abstract class DataLoaderBase<TFetchResult> : IDataLoader
    {
        /// <summary>
        /// To be implemented in derived classes to fire the query and begin loading.
        /// </summary>
        public abstract Task<TFetchResult> Fetch();

        /// <summary>
        /// Gets the loader's context. If the loader is bound to a context, this will
        /// property will contain that context. Loaders bound to a context are those obtained
        /// from methods or properties on a context, namely <see cref="DataLoaderContext.Factory"/>.
        /// If there is no bound context, this property will return the current ambient context.
        /// </summary>
        /// <seealso cref="DataLoaderContext.Current"/>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Current;
        private DataLoaderContext _boundContext;

        /// <summary>
        /// Represents the result of the fetch operation.
        /// </summary>
        protected Task<TFetchResult> Completion => _completionSource.Value.Task;
        private Lazy<TaskCompletionSource<TFetchResult>> _completionSource;

        /// <summary>
        /// Creates a new loader instance.
        /// </summary>
        public DataLoaderBase() : this(null) {}

        /// <summary>
        /// Creates a new loader instance bound to a specific context.
        /// </summary>
        /// <param name="boundContext"></param>
        protected internal DataLoaderBase(DataLoaderContext boundContext)
        {
            _completionSource = CreateNewLazyCompletionSource();
            _boundContext = boundContext;
        }

        public string FetchResultTypeName => GetType().Name.Split(new [] { "DataLoader" }, StringSplitOptions.None).First().ToLower() + " of " + typeof(TFetchResult).GenericTypeArguments.Last().Name;

        /// <summary>
        /// Executes the <see cref="Fetch"/> method implemented by the derived type,
        /// before setting the result on the <see cref="Completion"/> task.
        /// </summary>
        /// <remarks>
        /// Currently only one loader is permitted to be fetching at a time (at least, per loader context).
        /// Parallel requests to the database are usually counter-productive and create unnecessary contention.
        /// As soon as a fetch completes, the next waiting loader will be signaled. This allows us to keep loading
        /// data simultaneously in the background while we process the current items.
        /// </remarks>
        public void Trigger()
        {
            Fetch().ContinueWith((task, state) =>
                {
                    // Signal the next pending loader to fetch more data before we complete our promise task
                    // since then we have to wait for all the continuations to finish executing synchronously.
                    // We may as well pull more data over the network and keep the DB working in the meantime.
                    Context.SignalNext();

                    // Complete the promise task - continuations should run synchronously.
                    ((TaskCompletionSource<TFetchResult>)state).SetResult(task.Result);

                    // Then flush any loaders that were called from the continuations.
                    Context.FlushLoadersOnThread();
                }
                , Interlocked.Exchange(ref _completionSource, CreateNewLazyCompletionSource()).Value
                , CancellationToken.None
                , TaskContinuationOptions.ExecuteSynchronously
                , TaskScheduler.Default);
        }

        /// <summary>
        /// Prepares the loader to execute by queueing it to the context.
        /// </summary>
        private void Prepare()
        {
            Context.QueueLoader(this);
        }

        /// <summary>
        /// Creates a new <see cref="Lazy{TaskCompletionSource{TFetchResult}}"/>.
        /// When the value is instantiated the loader will also schedule itself with the 
        /// <see cref="Context"/> to be executed when appropriate.
        /// </summary>
        private Lazy<TaskCompletionSource<TFetchResult>> CreateNewLazyCompletionSource()
        {
            return new Lazy<TaskCompletionSource<TFetchResult>>(() =>
            {
                Prepare();
                return new TaskCompletionSource<TFetchResult>();
            });
        }
    }
}