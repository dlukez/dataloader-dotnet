using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Types;
using Microsoft.EntityFrameworkCore;

namespace DataLoader.StarWars.Schema
{
    public class EpisodeType : ObjectGraphType<Episode>
    {
        public EpisodeType()
        {
            Name = "Episode";

            Field("id", e => e.EpisodeId);
            Field("name", e => e.Name);

            Field<ListGraphType<CharacterInterface>>(
                name: "characters",
                resolve: ctx => ctx.GetDataLoader(async ids =>
                    {
                        var db = ctx.GetDataContext();

                        var humansTask = db.HumanAppearances
                            .Where(ha => ids.Contains(ha.EpisodeId))
                            .Select(ha => (ICharacterAppearance) new HumanAppearance { EpisodeId = ha.EpisodeId, Human = ha.Human })
                            .ToListAsync();

                        var droidsTask = db.DroidAppearances
                            .Where(da => ids.Contains(da.EpisodeId))
                            .Select(da => (ICharacterAppearance) new DroidAppearance { EpisodeId = da.EpisodeId, Droid = da.Droid })
                            .ToListAsync();

                        await Task.WhenAll(humansTask, droidsTask);

                        return humansTask.Result
                            .Concat(droidsTask.Result)
                            .ToLookup(a => a.EpisodeId, a => a.Character);
                    }).LoadAsync(ctx.Source.EpisodeId));
        }
    }

    public class EpisodeEnumType : EnumerationGraphType
    {
        public EpisodeEnumType()
        {
            Name = "EpisodeEnum";
            Description = "One of the films in the Star Wars Trilogy.";
            AddValue("NEWHOPE", "Released in 1977.", 4);
            AddValue("EMPIRE", "Released in 1980.", 5);
            AddValue("JEDI", "Released in 1983.", 6);
        }
    }

    public enum Episodes
    {
        NEWHOPE  = 4,
        EMPIRE  = 5,
        JEDI  = 6
    }
}