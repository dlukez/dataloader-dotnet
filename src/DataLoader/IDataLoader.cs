namespace DataLoader
{
    /// <summary>
    /// Represents a data loader that should be triggered explicitly.
    /// </summary>
    public interface IDataLoader
    {
        /// <summary>
        /// Executes the load operation.
        /// </summary>
        Task ExecuteAsync();
    }

    /// <summary>
    /// Represents a basic loader with no parameters.
    /// </summary>
    public interface IDataLoader<T>
    {
        Task<T> LoadAsync();
    }

    /// <summary>
    /// Represents a loader that takes a single key parameter.
    /// </summary>
    public interface IDataLoader<TKey, TReturn> : IDataLoader
    {
        Task<IEnumerable<TReturn>> LoadAsync(TKey key);
    }
}