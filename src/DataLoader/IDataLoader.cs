using System.Threading.Tasks;

namespace DataLoader
{
    public interface IDataLoader
    {
        Task<Task> ExecuteAsync();
    }
}