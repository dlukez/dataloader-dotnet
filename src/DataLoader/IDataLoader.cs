using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Represents a data loader that should be triggered explicitly.
    /// </summary>
    public interface IDataLoader
    {
        void Trigger();
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
    public interface IDataLoader<TKey, TReturn>
    {
        Task<TReturn> LoadAsync(TKey key);
    }
}