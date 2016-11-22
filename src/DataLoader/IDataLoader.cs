using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataLoader
{
    public interface IDataLoader
    {
        Task<IEnumerable> LoadAsync(object key);
    }

    public interface IDataLoader<TValue> : IDataLoader<object, TValue>
    {
    }

    public interface IDataLoader<TKey, TValue>
    {
        Task<IEnumerable<TValue>> LoadAsync(TKey key);
    }
}