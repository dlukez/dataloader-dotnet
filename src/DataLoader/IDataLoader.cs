using System;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Represents a loader that can be triggered explicitly.
    /// </summary>
    public interface IDataLoader : IDisposable
    {
        void Trigger();
    }

    /// <summary>
    /// Represents a loader that has no parameters.
    /// </summary>
    public interface IDataLoader<T>
    {
        Task<T> LoadAsync();
    }

    /// <summary>
    /// Represents a loader that retrieves a result for the given key.
    /// </summary>
    public interface IDataLoader<TKey, TReturn>
    {
        Task<TReturn> LoadAsync(TKey key);
    }
}