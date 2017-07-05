using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    public interface IDataLoader<T> : IDataLoader
    {
        Task<T> LoadAsync();
    }

    /// <summary>
    /// Wraps an arbitrary query and integrates it into the loading chain.
    /// </summary>
    public class DataLoaderRoot<T> : IDataLoader<T>
    {
        private readonly DataLoaderContext _boundContext;
        private readonly Func<Task<T>> _fetchDelegate;
        private readonly TaskCompletionSource<T> _completionSource = new TaskCompletionSource<T>(); 

        /// <summary>
        /// Creates a new <see cref="DataLoaderRoot{T}"/>.
        /// </summary>
        public DataLoaderRoot(Func<Task<T>> fetchDelegate) : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="DataLoaderRoot{T}"/> bound to the specified context.
        /// </summary>
        internal DataLoaderRoot(Func<Task<T>> fetchDelegate, DataLoaderContext boundContext)
        {
            _fetchDelegate = fetchDelegate;
            _boundContext = boundContext;
        }

        /// <summary>
        /// Gets the context visible to the loader which is either the loader is
        /// bound to if available, otherwise the current ambient context.
        /// </summary>
        /// <seealso cref="DataLoaderContext.Current"/>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Current;

        /// <summary>
        /// Loads data using the configured fetch delegate.
        /// </summary>
        public async Task<T> LoadAsync()
        {
            Context?.SetNext(ExecuteAsync);
            return await _completionSource.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Executes the fetch delegate and resolves the promise.
        /// </summary>
        public async Task<Task> ExecuteAsync()
        {
            var result = await _fetchDelegate().ConfigureAwait(false);
            return Task.Run(() => _completionSource.SetResult(result));
        }
    }
}
