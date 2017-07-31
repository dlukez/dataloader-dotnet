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
                resolve: async ctx => await ctx.GetBatchLoader(async ids =>
                    {
                        var db = ctx.GetDataContext();
                        var result = db.Friendships
                                .Where(f => ids.Contains(f.DroidId))
                                .GroupBy(f => f.DroidId, f => f.Human)
                                .ToDictionaryAsync(g => g.Key, g => g.AsEnumerable());
                        return await result;
                    }).LoadAsync(ctx.Source.DroidId));

            FieldAsync<ListGraphType<EpisodeType>>(
                name: "appearsIn",
                resolve: async ctx => await ctx.GetBatchLoader(async ids =>
                    {
                        var db = ctx.GetDataContext();
                        var result = db.DroidAppearances
                                .Where(da => ids.Contains(da.DroidId))
                                .GroupBy(da => da.DroidId, da => da.Episode)
                                .ToDictionaryAsync(g => g.Key, g => g.AsEnumerable());
                        return await result;
                    }).LoadAsync(ctx.Source.DroidId));
        }
    }
}
