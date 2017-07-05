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

        /// <summary>
        /// Executes the <see cref="Fetch"/> method implemented by the derived type,
        /// before completing the <see cref="Completion"/> promise task.
        /// </summary>
        /// <remarks>
        /// Once the fetch portion of the operation returns, the <see cref="Context"/>
        /// is signaled, informing it that it may execute the next loader. In general,
        /// it is a good idea to minimize database contention by not running multiple
        /// queries in parallel.
        /// </remarks>
        public Task ExecuteAsync()
        {
            return Fetch().ContinueWith(
                (task, state) =>
                {
                    // Fetch again as soon as the call has returned. This should help 
                    // minimize contention on the DB while continuously providing data
                    // to keep the CPU busy and minimize response time.
                    Context.SignalNext();
                    Logger.WriteLine($"Completing {typeof(TFetchResult).GenericTypeArguments.Last().Name}");
                    ((TaskCompletionSource<TFetchResult>)state).SetResult(task.Result);
                    Context.FlushLoadersOnThread();
                }, Interlocked.Exchange(ref _completionSource, CreateNewLazyCompletionSource()).Value);
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