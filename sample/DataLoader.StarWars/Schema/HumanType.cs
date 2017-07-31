using System;
using System.Linq;
using System.Threading;
using GraphQL.Types;
using Microsoft.EntityFrameworkCore;

namespace DataLoader.StarWars.Schema
{
    public class HumanType : ObjectGraphType<Human>
    {
        public HumanType()
        {
            Name = "Human";
            Field(h => h.Name);
            Field(h => h.HumanId);
            Field(h => h.HomePlanet);
            Interface<CharacterInterface>();

            FieldAsync<ListGraphType<CharacterInterface>>(
                name: "friends",
                resolve: async ctx => await ctx.GetBatchLoader(async ids =>
                    {
                        var db = ctx.GetDataContext();
                        var result = db.Friendships
                            .Where(f => ids.Contains(f.HumanId))
                            .GroupBy(f => f.HumanId, f => f.Droid)
                            .ToDictionaryAsync(g => g.Key, g => g.AsEnumerable());
                        return await result;
                    }).LoadAsync(ctx.Source.HumanId));

            FieldAsync<ListGraphType<EpisodeType>>(
                name: "appearsIn",
                resolve: async ctx => await ctx.GetBatchLoader(async ids =>
                {
                    var db = ctx.GetDataContext();
                    var result = db.HumanAppearances
                        .Where(ha => ids.Contains(ha.HumanId))
                        .GroupBy(ha => ha.HumanId, ha => ha.Episode)
                        .ToDictionaryAsync(g => g.Key, g => g.AsEnumerable());
                    return await result;
                }).LoadAsync(ctx.Source.HumanId));
        }
    }
}
