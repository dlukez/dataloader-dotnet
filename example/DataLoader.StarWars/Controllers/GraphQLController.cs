using System;
using System.Diagnostics;
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
        [HttpPost]
        public Task<ExecutionResult> Post([FromBody] GraphQLRequest request)
        {
            return RunQuery(request.Query, false);
        }

        [HttpPost]
        [Route("custom")]
        public Task<ExecutionResult> PostCustom([FromBody] GraphQLRequest request)
        {
            return RunQuery(request.Query, true);
        }

        public static async Task<ExecutionResult> RunQuery(string query, bool usePrototypeLoader)
        {
            var type = usePrototypeLoader ? "prototype" : "task-based";
            var executer = new DocumentExecuter();
            var schema = new StarWarsSchema();

            var sw = Stopwatch.StartNew();
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} ({TaskScheduler.Current.GetType().Name}) ========={type}=========");
            var task = DataLoaderContext.Run(ctx => executer.ExecuteAsync(schema, ctx, query, null), usePrototypeLoader);
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} ({TaskScheduler.Current.GetType().Name}) - Task status = {Enum.GetName(typeof(TaskStatus), task.Status)}");
            var result = await task;
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} ({TaskScheduler.Current.GetType().Name}) - Total time: {sw.ElapsedMilliseconds}ms");

            return result;
        }
    }

    public class GraphQLRequest
    {
        public string Query { get; set; }
        public object Variables { get; set; }
    }
}