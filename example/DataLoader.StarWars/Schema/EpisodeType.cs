using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataLoader.StarWars.Data;
using GraphQL.Types;

namespace DataLoader.StarWars.Schema
{
    public class EpisodeType : ObjectGraphType<Episode>
    {
        public EpisodeType()
        {
            Name = "Episode";

            Field("id", e => e.EpisodeId);
            Field("name", e => e.Name);

            Field<ListGraphType<CharacterInterface>>()
                .Name("characters")
                .Resolve(ctx =>
                {
                    return ((DataLoaderContext) ctx.RootValue).GetCachedLoader<int, ICharacter>(
                        ctx.FieldDefinition,
                        async ids =>
                        {
                            await Task.Delay(10);
                            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} ({TaskScheduler.Current.GetType().Name}) - {new String(' ', 2  * DataLoaderContext.Current.Level)}Fetching characters of episodes " + string.Join(",", ids));
                            using (var db = new StarWarsContext())
                            {
                                var humans = db.HumanAppearances
                                    .Where(ha => ids.Contains(ha.EpisodeId))
                                    .Select(ha => new {Key = ha.EpisodeId, ha.Human})
                                    .ToList()
                                    .Select(x => new KeyValuePair<int, ICharacter>(x.Key, x.Human));

                                var droids = db.DroidAppearances
                                    .Where(da => ids.Contains(da.EpisodeId))
                                    .Select(da => new {Key = da.EpisodeId, da.Droid})
                                    .ToList()
                                    .Select(x => new KeyValuePair<int, ICharacter>(x.Key, x.Droid));

                                var characters = humans.Concat(droids);
                                return characters.ToLookup(kvp => kvp.Key, kvp => kvp.Value);
                            }
                        }).LoadAsync(ctx.Source.EpisodeId);
                });
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