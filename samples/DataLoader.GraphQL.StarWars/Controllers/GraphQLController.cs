using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataLoader.GraphQL.StarWars.Schema;
using GraphQL;
using GraphQL.Instrumentation;
using Microsoft.AspNetCore.Mvc;

namespace DataLoader.GraphQL.StarWars.Controllers
{
    [Route("api/graphql")]
    public class GraphQLController : Controller
    {
        private static int _queryNumber;
        private readonly StarWarsSchema _schema;
        private readonly IDocumentExecuter _executer;

        public GraphQLController(StarWarsSchema schema, IDocumentExecuter executer)
        {
            _schema = schema;
            _executer = executer;
        }

        [HttpPost]
        public async Task<ExecutionResult> Post([FromBody] GraphQLRequest request)
        {
            var queryNumber = Interlocked.Increment(ref _queryNumber);
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} - Running query {queryNumber}...");
            var sw = Stopwatch.StartNew();

            var result = await await DataLoaderContext.Run(ctx => _executer.ExecuteAsync(_ =>
            {
                _.Schema = _schema;
                _.Query = request.Query;
                _.UserContext = new GraphQLUserContext(ctx);
            }));

            sw.Stop();
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} - Executed query {queryNumber} ({sw.ElapsedMilliseconds}ms)");
            return result;
        }
    }

    public class GraphQLRequest
    {
        public string Query { get; set; }
        public object Variables { get; set; }
    }
}