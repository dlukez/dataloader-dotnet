using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataLoader.StarWars.Data;
using GraphQL.Types;

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

            /*
             *  Example 1: One loader instance for each HumanType instance.
             *
             *    If the schema is created once on application startup and reused
             *    for every request, then the same loader will be used by
             *    multiple requests/threads.
             */
//            var friendsLoader = new BatchDataLoader<int, Human>(ids =>
//            {
//                Console.WriteLine("Fetching friends of droids " + string.Join(", ", ids));
//                using (var db = new StarWarsContext())
//                    return db.Friendships
//                        .Where(f => ids.Contains(f.DroidId))
//                        .Select(f => new {Key = f.DroidId, f.Human})
//                        .ToLookup(x => x.Key, x => x.Human);
//            });
//            Field<ListGraphType<CharacterInterface>>()
//                .Name("friends")
//                .Resolve(ctx => friendsLoader.LoadAsync(ctx.Source.DroidId));
            Field<ListGraphType<CharacterInterface>>()
                .Name("friends")
                .Resolve(ctx => ((DataLoaderContext) ctx.RootValue).GetCachedLoader<int, Human>(
                    ctx.FieldDefinition,
                    async ids =>
                    {
                        await Task.Delay(10);
                        Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} ({TaskScheduler.Current.GetType().Name}) - {new String(' ', 2  * DataLoaderContext.Current.Level)}Fetching friends of droids " + string.Join(",", ids));
                        using (var db = new StarWarsContext())
                            return db.Friendships
                                .Where(f => ids.Contains(f.DroidId))
                                .Select(f => new {Key = f.DroidId, f.Human})
                                .ToLookup(x => x.Key, x => x.Human);
                    }).LoadAsync(ctx.Source.DroidId));

            /*
             *  Example 2: Each request has its own context passed through the RootValue.
             *
             *    This helps keep queries/batches separate and is more likely to prevent
             *    issues arising from other queries that are executing concurrently.
             */
            Field<ListGraphType<EpisodeType>>()
                .Name("appearsIn")
                .Resolve(ctx => ((DataLoaderContext) ctx.RootValue).GetCachedLoader<int, Episode>(
                    ctx.FieldDefinition,
                    async ids =>
                    {
                        await Task.Delay(10);
                        Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} ({TaskScheduler.Current.GetType().Name}) - {new String(' ', 2  * DataLoaderContext.Current.Level)}Fetching episodes appeared in by droids " + string.Join(",", ids));
                        using (var db = new StarWarsContext())
                            return db.HumanAppearances
                                .Where(ha => ids.Contains(ha.HumanId))
                                .Select(ha => new {Key = ha.HumanId, ha.Episode})
                                .ToLookup(x => x.Key, x => x.Episode);
                    }).LoadAsync(ctx.Source.DroidId));
        }
    }
}
