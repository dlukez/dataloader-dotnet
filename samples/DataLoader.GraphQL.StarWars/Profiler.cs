using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DataLoader.GraphQL.StarWars.Schema;
using GraphQL;

namespace DataLoader.GraphQL.StarWars
{
    public static class Profiler
    {
        public static void RunTests()
        {
            var schema = new StarWarsSchema();
            var executer = new DocumentExecuter();
            var clock = Stopwatch.StartNew();
            var i = 0;
            for (; i < 100000; i++) RunTest(schema, executer).Wait();
            clock.Stop();
            Console.WriteLine($"Took {clock.ElapsedMilliseconds}ms to run {i} tests");
        }

        private static async Task RunTest(StarWarsSchema schema, IDocumentExecuter executer)
        {
            await executer.ExecuteAsync(_ =>
            {
                _.Query = TestQuery;
                _.Schema = schema;
                _.UserContext = new StarWarsContext();
            });
        }

        private const string TestQuery = @"
{
  droidId
  name
  primaryFunction
  friends {
    ... on Droid {
      ...friendFields
      appearsIn {
        id
        name
      }
    }
    name
    __typename
    ... on Human {
      ...friendFields
      appearsIn {
        id
        name
      }
      ... on Human {
        homePlanet
        appearsIn {
          id
          name
          characters {
            name
            friends {
              name
              __typename
              ... on Human {
                humanId
                name
                friends {
                  name
                }
              }
              ... on Droid {
                droidId
                name
                friends {
                  name
                }
              }
            }
          }
        }
      }
    }
    friends {
      ...friendFields
      friends {
        ...friendFields
        friends {
          ...friendFields
          friends {
            appearsIn {
              id
              name
              characters {
                appearsIn {
                  id
                  name
                  characters {
                    name
                  }
                }
              }
            }
            ...friendFields
          }
        }
      }
    }
  }
}
";
    }
}