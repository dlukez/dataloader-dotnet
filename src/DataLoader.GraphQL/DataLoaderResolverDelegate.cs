using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Types;

namespace GraphQL.DataLoader
{
    public delegate Task<ILookup<TKey, TValue>> DataLoaderResolverDelegate<TKey, TValue>(IEnumerable<TKey> ids, ResolveFieldContext fieldContext);
}