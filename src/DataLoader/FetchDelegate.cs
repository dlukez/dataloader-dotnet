using System.Collections.Generic;
using System.Linq;

namespace DataLoader
{
    public delegate ILookup<TKey, TValue> FetchDelegate<TKey, TValue>(IEnumerable<TKey> keys);
}