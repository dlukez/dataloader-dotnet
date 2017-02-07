using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataLoader
{
    public interface IDataLoader<in TKey, TReturn> : IDataLoader
    {
        Task<IEnumerable<TReturn>> LoadAsync(TKey key);
    }

    public interface IDataLoader
    {
        DataLoaderStatus Status { get; }
        Task ExecuteAsync();
    }
}