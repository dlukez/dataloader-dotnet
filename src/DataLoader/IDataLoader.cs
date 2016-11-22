using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataLoader
{
    public interface IDataLoader
    {
        Task ExecuteAsync();
    }

    public interface IDataLoader<TKey, TValue> : IDataLoader
    {
        Task<IEnumerable<TValue>> LoadAsync(TKey key);
    }
}