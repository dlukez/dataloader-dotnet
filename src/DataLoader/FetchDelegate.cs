using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataLoader
{
    public delegate Task<ILookup<TKey, TReturn>> FetchDelegate<TKey, TReturn>(IEnumerable<TKey> keys);
}