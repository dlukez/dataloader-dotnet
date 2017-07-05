using System.Threading.Tasks;

namespace DataLoader
{
    internal static class AsyncHelpers
    {
        public static void IgnoreAwait(this Task task) {}
    }
}