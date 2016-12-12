using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataLoader
{
    public delegate Task<ILookup<TKey, TValue>> FetchDelegate<TKey, TValue>(IEnumerable<TKey> keys);
}