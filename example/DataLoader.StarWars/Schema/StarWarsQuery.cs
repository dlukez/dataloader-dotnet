using System.Linq;
using DataLoader.StarWars.Data;
using GraphQL.Types;

namespace DataLoader.StarWars.Schema
{
    public class StarWarsQuery : ObjectGraphType
    {
        public StarWarsQuery()
        {
            Name = "Query";

            Field<ListGraphType<HumanType>>()
                .Name("humans")
                .Resolve(ctx =>
                {
                    using (var db = new StarWarsContext())
                        return db.Humans.ToList();
                });

            Field<ListGraphType<DroidType>>()
                .Name("droids")
                .Resolve(ctx =>
                {
                    using (var db = new StarWarsContext())
                        return db.Droids.ToList();
                });

            Field<ListGraphType<EpisodeType>>()
                .Name("episodes")
                .Resolve(ctx =>
                {
                    using (var db = new StarWarsContext())
                        return db.Episodes.ToList();
                });
        }
    }
}
