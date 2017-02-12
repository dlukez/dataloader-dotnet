DataLoader for .NET
===================

A port of Facebook's [DataLoader](https://github.com/facebook/dataloader) for .NET.

[![NuGet](https://img.shields.io/nuget/v/DataLoader.svg)](https://nuget.org/packages/DataLoader)
[![MyGet Pre Release](https://img.shields.io/myget/dlukez/vpre/DataLoader.svg)](https://www.myget.org/feed/dlukez/package/nuget/DataLoader)
[![MyGet Build Status](https://www.myget.org/BuildSource/Badge/dlukez?identifier=265cd302-0184-43af-abc8-6041143cfc91)](https://www.myget.org/feed/dlukez/package/nuget/DataLoader)

Originally began as [a solution](https://github.com/dlukez/graphql-dotnet-dataloader) to the [select N+1 problem](https://github.com/graphql-dotnet/graphql-dotnet/issues/21) for [GraphQL in .NET](https://github.com/graphql-dotnet/graphql-dotnet) but found that most of the (small amount of) code was independent and could be generalized for use in other scenarios.

If anyone finds use for this in other areas, please let me know... I'd love to know whether the solution could be expanded to cater for other uses.

Check out the [sample](https://github.com/dlukez/dataloader-dotnet/tree/master/samples/DataLoader.GraphQL.StarWars) to see it used in a GraphQL implementation.


Caveats
-------

Facebook's implementation runs in Javascript and takes advantage of the [event loop](https://developer.mozilla.org/en-US/docs/Web/API/window/requestAnimationFrame) to fire any pending requests for ID's collected during the previous frame. Unfortunately, not all .NET applications run in an event loop.

As such, we have defined a special frame or context to contain our load operations. Whenever we want to use a loader, we should be inside one of these contexts. A simple way to do this is by calling the static `DataLoaderContext.Run` method. This method takes a user-supplied delegate and runs it in within a new context, before actually executing any loaders that were called within it.


Usage
-----

### Example 1: Implicit/unbound.

```csharp
var personLoader = new DataLoader<int, Person>(ids =>
{
    using (var db = new StarWarsContext())
    {
        return db.Person.Where(p => ids.Contains(p.Id)).ToListAsync();
    }
});

var results = await DataLoaderContext.Run(() =>
{
    // We have an implicit context here
    Debug.Assert(DataLoaderContext.Current != null);

    // Queue up some person loads.
    var task1 = personLoader.LoadAsync(1);
    var task2 = personLoader.LoadAsync(2);
    var task3 = personLoader.LoadAsync(3);
    
    // Await the results... Control returns to Run and the loader is fired.
    var results = await Task.WhenAll(task1, task2, task3);

    // We have the results, but let's load some more! Run ensures that asynchronous
    // continuations behave like the initial call - ID's should be collected and fetched
    // as a batch after continuations have run.
    var task4 = personLoader.LoadAsync(4);
    var task5 = personLoader.LoadAsync(5);

    // Return all our results.
    return (await Task.WhenAll(task4, task5)).Concat(results);
});
```

### Example 2: Explicit/bound.

```csharp
var results = await DataLoaderContext.Run(loadCtx =>
{
    var droidLoader = loadCtx.GetDataLoader<int, Droid>(ids =>
    {
        using (var db = new StarWarsContext())
        {
            return db.Droid.Where(d => ids.Contains(d.Id)).ToListAsync();
        }
    });

    //...

    var task1 = droidLoader.LoadAsync(1);
    var task2 = droidLoader.LoadAsync(2);
    var task3 = droidLoader.LoadAsync(3);
    
    return Task.WhenAll(task1, task2, task3);
));
```



To do
-----
- [x] Basic support
- [x] Support async fetching
- [ ] Cancellation
- [ ] Benchmarks
- [ ] Multithreaded performance

Ideas
-----
- [ ] Single worker thread to service loaders
- [ ] Sync context to handle async/await in load continuations
