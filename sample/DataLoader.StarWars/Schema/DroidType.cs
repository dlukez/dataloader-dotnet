using System;
using System.Linq;
using System.Threading;
using GraphQL.Types;
using Microsoft.EntityFrameworkCore;

namespace DataLoader.StarWars.Schema
{
    public class DroidType : ObjectGraphType<Droid>
    {
        public DroidType()
        {
            Name = "Droid";
            Field(d => d.Name);
            Field(d => d.DroidId);
            Field(d => d.PrimaryFunction);
            Interface<CharacterInterface>();

            FieldAsync<ListGraphType<CharacterInterface>>(
                name: "friends",
                resolve: async ctx => await ctx.GetDataLoader(async ids =>
                    {
                        var db = ctx.GetDataContext();
                        return (await db.Friendships
                                .Where(f => ids.Contains(f.DroidId))
                                .Select(f => new {Key = f.DroidId, f.Human})
                                .ToListAsync())
                            .ToLookup(x => x.Key, x => x.Human);
                    }).LoadAsync(ctx.Source.DroidId));

            FieldAsync<ListGraphType<EpisodeType>>(
                name: "appearsIn",
                resolve: async ctx => await ctx.GetDataLoader(async ids =>
                    {
                        var db = ctx.GetDataContext();
                        return (await db.DroidAppearances
                                .Where(da => ids.Contains(da.DroidId))
                                .Select(da => new {Key = da.DroidId, da.Episode})
                                .ToListAsync())
                            .ToLookup(x => x.Key, x => x.Episode);
                    }).LoadAsync(ctx.Source.DroidId));
        }
    }
}
