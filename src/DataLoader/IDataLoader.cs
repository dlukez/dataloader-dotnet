using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataLoader
{
    public interface IDataLoader<TKey, TValue> : IDataLoader
    {
        Task<IEnumerable<TValue>> LoadAsync(TKey key);
    }

    public interface IDataLoader
    {
        DataLoaderStatus Status { get; }
        Task ExecuteAsync();
        void SetContext(DataLoaderContext context);
    }
}