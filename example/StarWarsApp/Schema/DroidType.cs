using DataLoader;
using GraphQL.Types;
using StarWarsApp.Data;
using System;
using System.Linq;

namespace StarWarsApp.Schema
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

            var friendsLoader = new BatchLoader<int, Human>(ids =>
            {
                Console.WriteLine("Fetching friends of droids " + string.Join(", ", ids));
                using (var db = new StarWarsContext())
                    return db.Friendships
                        .Where(f => ids.Contains(f.DroidId))
                        .Select(f => new {Key = f.DroidId, f.Human})
                        .ToLookup(x => x.Key, x => x.Human);
            });
            
            Field<ListGraphType<CharacterInterface>>()
                .Name("friends")
                .Resolve(ctx => friendsLoader.LoadAsync(ctx.Source.DroidId));
        }
    }
}
