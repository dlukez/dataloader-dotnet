using System;
using System.Linq;
using DataLoader;
using GraphQL.Types;
using StarWarsApp.Data;

namespace StarWarsApp.Schema
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

            var friendsLoader = new BatchLoader<int, Droid>(ids =>
            {
                Console.WriteLine("Fetching friends of humans " + string.Join(", ", ids));
                using (var db = new StarWarsContext())
                    return db.Friendships
                        .Where(f => ids.Contains(f.HumanId))
                        .Select(f => new {Key = f.HumanId, f.Droid})
                        .ToLookup(f => f.Key, f => f.Droid);
            });
            
            Field<ListGraphType<CharacterInterface>>()
                .Name("friends")
                .Resolve(ctx => friendsLoader.LoadAsync(ctx.Source.HumanId));
        }
    }
}
