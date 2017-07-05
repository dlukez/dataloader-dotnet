using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Types;
using Microsoft.EntityFrameworkCore;

namespace DataLoader.StarWars.Schema
{
    public class StarWarsQuery : ObjectGraphType
    {
        public StarWarsQuery()
        {
            Name = "Query";

            Field<ListGraphType<HumanType>>(
                name: "humans",
                resolve: ctx => ctx.GetDataLoader(async () => (await ctx.GetDataContext().Humans.ToArrayAsync()).AsEnumerable()).LoadAsync());

            Field<ListGraphType<DroidType>>(
                name: "droids",
                resolve: ctx => ctx.GetDataLoader(async () => (await ctx.GetDataContext().Droids.ToArrayAsync()).AsEnumerable()).LoadAsync());

            Field<ListGraphType<EpisodeType>>(
                name: "episodes",
                resolve: ctx => ctx.GetDataLoader(async () => (await ctx.GetDataContext().Episodes.ToArrayAsync()).AsEnumerable()).LoadAsync());
        }
    }
}
