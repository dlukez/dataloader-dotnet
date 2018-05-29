using System;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Wraps a user-supplied delegate and integrates it into the loader
    /// pipeline so that it may be scheduled and completed appropriately,
    /// with respect to other loaders in the pipeline.
    /// </summary>
    internal sealed class RootDataLoader<T> : DataLoaderBase<T>, IDataLoader<T>
    {
        private Func<Task<T>> _fetchDelegate;

        /// <summary>
        /// Creates a new <see cref="RootDataLoader{T}"/>.
        /// </summary>
        public RootDataLoader(Func<Task<T>> fetchDelegate)
            : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="RootDataLoader{T}"/> bound to a specific context.
        /// </summary>
        internal RootDataLoader(Func<Task<T>> fetchDelegate, DataLoaderContext context)
            : base(context)
        {
            _fetchDelegate = fetchDelegate;
        }

        /// <summary>
        /// Schedules the loader to fire.
        /// </summary>
        /// <returns>A <see cref="Task{T}"/> representing the future result.</returns>
        public Task<T> LoadAsync() => Completion;

        /// <summary>
        /// Invokes the user-specified fetch delegate configured in the constructor.
        /// </summary>
        /// <returns>The result of the fetch delegate.</returns>
        public override Task<T> Fetch() => _fetchDelegate();
    }
}