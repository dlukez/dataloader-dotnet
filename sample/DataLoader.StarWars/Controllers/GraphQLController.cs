using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataLoader.StarWars.Schema;
using GraphQL;
using Microsoft.AspNetCore.Mvc;

namespace DataLoader.StarWars.Controllers
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
            Console.WriteLine();
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Running query {queryNumber}...");
            var sw = Stopwatch.StartNew();

            var result = await DataLoaderContext.Run(loadCtx => _executer.ExecuteAsync(_ =>
            {
                _.Schema = _schema;
                _.Query = request.Query;
                _.UserContext = new GraphQLUserContext(loadCtx);
            }));

            sw.Stop();
            if (result.Errors != null) Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Error executing query {queryNumber}: {result.Errors.Aggregate("", (s, e) => s + Environment.NewLine + e.ToString())}");
            else Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Executed query {queryNumber} ({sw.ElapsedMilliseconds}ms)");

            return result;
        }
    }

    public class GraphQLRequest
    {
        public string Query { get; set; }
        public object Variables { get; set; }
    }
}