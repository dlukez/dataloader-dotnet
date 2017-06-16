using System.Threading.Tasks;

namespace DataLoader
{
    public interface IDataLoader
    {
        DataLoaderStatus Status { get; }
        Task ExecuteAsync();
    }
}