DataLoader for .NET
===================

A port of Facebook's [DataLoader](https://github.com/facebook/dataloader) for .NET.

[![dataloader-dotnet MyGet Build Status](https://www.myget.org/BuildSource/Badge/dataloader-dotnet?identifier=146afa03-c463-4c59-bc89-559ab4107f85)](https://www.myget.org/feed/dataloader-dotnet/package/nuget/DataLoader)

Originally began as [a solution](https://github.com/dlukez/graphql-dotnet-dataloader) to the [select N+1 problem](https://github.com/graphql-dotnet/graphql-dotnet/issues/21)
for [GraphQL in .NET](https://github.com/graphql-dotnet/graphql-dotnet) but found that most of the (small amount of) code
was independent and could be generalized for use in other scenarios.

If anyone finds use for this in other areas, please let me know...
I'd love to know whether the solution could be expanded to cater for other uses.

See [this repository](https://github.com/dlukez/graphql-dotnet-dataloader) to see it used in a GraphQL implementation.


Caveats
-------

Facebook's implementation runs in Javascript and takes advantage of the
[event loop](https://developer.mozilla.org/en-US/docs/Web/API/window/requestAnimationFrame)
to fire any pending requests for ID's collected during the previous frame.
Unfortunately not all .NET applications run in an event loop.

For this reason, we have our own `DataLoaderContext` to house `DataLoader` instances.
Any instances should be called within a particular `DataLoaderContext` - which essentially
represents a frame in Javascript - using the static `DataLoaderContext.Run` method.
This method will run the user-supplied delegate before calling `Start` on the created context,
which then fires any pending fetches and processes the results.

Loaders may be called again as the results are processed, which would cause them to be requeued.
This effectively turns the context into a kind of asynchronous loader pump.


Usage
-----

```csharp
// Create the loader.
var personLoader = new DataLoader<int, Person>(ids =>
{
    using (var db = new StarWarsContext())
        return db.Person
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();
});

// Call Run with a delegate that will be doing some loading and await the result.
var results = await DataLoaderContext.Run(() =>
{
    // We have an implicit context here
    Debug.Assert(DataLoaderContext.Current != null);

    // Queue up some person loads.
    var task1 = personLoader.LoadAsync(1);
    var task2 = personLoader.LoadAsync(2);
    var task3 = personLoader.LoadAsync(3);
    
    // Await the results... Control returns to Run and the loader is fired.
    var results = await Task.WhenAll(task1, task2, task3).ConfigureAwait(false);

    // We have the results, but let's load some more! Run ensures that asynchronous
    // continuations behave like the initial call - ID's should be collected and fetched
    // as a batch at the end of each continuation.
    var task4 = personLoader.LoadAsync(4);
    var task5 = personLoader.LoadAsync(5);

    // Return all our results.
    return (await Task.WhenAll(task4, task5)).Concat(results);
});

// Do something with the results.
Console.WriteLine(results);
```


To do
-----
- [x] Basic support
- [x] Support async fetching
- [ ] Cancellation
- [ ] Benchmarks
- [ ] Multithreaded performance
