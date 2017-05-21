using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;

namespace DataLoader.GraphQL.StarWars
{
    public class Program
    {
        public static void Main()
        {
            InitTestData();

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .UseIISIntegration()
                .Build();

            host.Run();
        }

        private static void InitTestData()
        {
            using (var db = new StarWarsContext())
            {
                db.Database.EnsureCreated();

                if (db.Humans.Any()) return;

                const int numberOfHumans = 1000;
                const int numberOfDroids = 1000;
                const int numberOfFriendships = 1500;
                const int numberOfAppearancesPerType = 1250;

                var random = new Random();

                db.Humans.RemoveRange(db.Humans);
                db.Droids.RemoveRange(db.Droids);
                db.Episodes.RemoveRange(db.Episodes);
                db.Friendships.RemoveRange(db.Friendships);
                db.DroidAppearances.RemoveRange(db.DroidAppearances);
                db.HumanAppearances.RemoveRange(db.HumanAppearances);

                // Humans
                db.Humans.AddRange(Enumerable.Range(1, numberOfHumans).Select(id => new Human
                {
                    HumanId = id,
                    Name = Faker.Name.First(),
                    HomePlanet = Faker.Company.BS()
                }));

                // Droids
                db.Droids.AddRange(Enumerable.Range(1, numberOfDroids).Select(id => new Droid
                {
                    DroidId = id,
                    Name = $"{(char)Faker.RandomNumber.Next('A', 'Z')}"
                         + $"{Faker.RandomNumber.Next(1, 9)}"
                         + $"{(char)Faker.RandomNumber.Next('A', 'Z')}"
                         + $"{Faker.RandomNumber.Next(1, 9)}"
                }));

                // Episodes
                db.Episodes.AddRange(new[]
                {
                    new Episode { EpisodeId = 4, Name = "A New Hope", Year = "1978" },
                    new Episode { EpisodeId = 5, Name = "Rise of the Empire", Year = "1980" },
                    new Episode { EpisodeId = 6, Name = "Return of the Jedi", Year = "1983" }
                });

                // Friendships
                db.Friendships.AddRange(Enumerable.Range(1, numberOfFriendships).Select(_ => new Friendship
                {
                    DroidId = random.Next(1, numberOfDroids),
                    HumanId = random.Next(1, numberOfHumans)
                }));

                // Appearances (Droid)
                db.DroidAppearances.AddRange(Enumerable.Range(1, numberOfAppearancesPerType).Select(_ => new DroidAppearance
                {
                    EpisodeId = random.Next(4, 6),
                    DroidId = random.Next(1, numberOfDroids)
                }));

                // Appearances (Human)
                db.HumanAppearances.AddRange(Enumerable.Range(1, numberOfAppearancesPerType).Select(_ => new HumanAppearance
                {
                    EpisodeId = random.Next(4, 6),
                    HumanId = random.Next(1, numberOfHumans)
                }));

                // Save
                var count = db.SaveChanges();
                Console.WriteLine("{0} records saved to database", count);
            }
        }
    }
}