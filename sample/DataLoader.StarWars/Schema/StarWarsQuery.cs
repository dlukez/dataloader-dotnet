using System;
using System.Collections.Generic;
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
                resolve: ctx => ctx.GetDataLoader((Func<Task<IEnumerable<Human>>>)(
                    async () => await ctx.GetDataContext().Humans.ToListAsync())).LoadAsync());

            Field<ListGraphType<DroidType>>(
                name: "droids",
                resolve: ctx => ctx.GetDataLoader((Func<Task<IEnumerable<Droid>>>)(
                    async () => await ctx.GetDataContext().Droids.ToListAsync())).LoadAsync());

            Field<ListGraphType<EpisodeType>>(
                name: "episodes",
                resolve: ctx => ctx.GetDataLoader((Func<Task<IEnumerable<Episode>>>)(
                    async () => await ctx.GetDataContext().Episodes.ToListAsync())).LoadAsync());
        }
    }
}
