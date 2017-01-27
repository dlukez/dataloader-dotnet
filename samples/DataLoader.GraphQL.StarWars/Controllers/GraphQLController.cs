using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DataLoader.GraphQL.StarWars.Schema;
using GraphQL;
using Microsoft.AspNetCore.Mvc;

namespace DataLoader.GraphQL.StarWars.Controllers
{
    [Route("api/graphql")]
    public class GraphQLController : Controller
    {
        private readonly IDocumentExecuter _executer = new DocumentExecuter();
        private readonly StarWarsSchema _schema = new StarWarsSchema();

        [HttpPost]
        public async Task<ExecutionResult> Post([FromBody] GraphQLRequest request)
        {
            var sw = Stopwatch.StartNew();

            var result = await _executer.ExecuteAsync(_ =>
            {
                var userContext = new GraphQLUserContext();
                _.Schema = _schema;
                _.Query = request.Query;
                _.UserContext = userContext;
                _.Listeners.Add(new DataLoaderListener(userContext.LoadContext));
            });

            Debug.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - Finished query ({sw.ElapsedMilliseconds}ms)");
            sw.Stop();

            return result;
        }
    }

    public class GraphQLRequest
    {
        public string Query { get; set; }
        public object Variables { get; set; }
    }
}