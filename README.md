# DataLoader for .NET

A port of Facebook's [DataLoader](https://github.com/facebook/dataloader) for .NET.

[![NuGet](https://img.shields.io/nuget/v/DataLoader.svg)](https://nuget.org/packages/DataLoader)
[![MyGet Pre Release](https://img.shields.io/myget/dlukez/vpre/DataLoader.svg?label=myget)](https://www.myget.org/feed/dlukez/package/nuget/DataLoader)
[![MyGet Build Status](https://www.myget.org/BuildSource/Badge/dlukez?identifier=265cd302-0184-43af-abc8-6041143cfc91)](https://www.myget.org/feed/dlukez/package/nuget/DataLoader)

This project began as a solution to the [select N+1 problem](https://github.com/graphql-dotnet/graphql-dotnet/issues/21) for [GraphQL .NET](https://github.com/graphql-dotnet/graphql-dotnet) but was implemented as a standalone package that is completely decoupled from any framework.

It leverages .NET's async/await feature to enable query batching (a la Facebook's [Dataloader](https://github.com/facebook/dataloader)) that should work out of the box, without requiring significant changes to an existing codebase.

If anyone finds this useful outside of GraphQL, feel free to drop me a message - I'm interested to know of other potential applications that could be catered to.

Check out the [sample](https://github.com/dlukez/dataloader-dotnet/tree/master/samples/DataLoader.GraphQL.StarWars) to see it used in a GraphQL implementation.

## Caveats

Facebook's implementation runs in Javascript and takes advantage of the [event loop](https://developer.mozilla.org/en-US/docs/Web/API/window/requestAnimationFrame) to fire any pending requests for ID's collected during the previous frame. Unfortunately, not all .NET applications run in an event loop.

As such, we have defined a special frame or context to contain our load operations. Whenever we want to use a loader, we should be inside one of these contexts. A simple way to do this is by calling the static `DataLoaderContext.Run` method. This method takes a user-supplied delegate and runs it in within a new context, before actually executing any loaders that were called within it.

## Usage

There are two ways loaders can be used.

### Method 1: Bound/explicit context (recommended)

With this approach, loader instances are obtained for a particular context using the context's `GetDataLoader` methods. Along with the user-supplied fetch callback, these methods also take a key for caching and reusing instances.

```csharp
var results = await DataLoaderContext.Run(async loadCtx =>
{
    // Here we obtain a loader using the context's factory method.
    var droidLoader = loadCtx.Factory.GetDataLoader<int, Droid>("droids", ids =>
    {
        using (var db = new StarWarsContext())
        {
            return db.Droid.Where(d => ids.Contains(d.Id)).ToListAsync();
        }
    });

    // Queue up some loads.
    var task1 = droidLoader.LoadAsync(1);
    var task2 = droidLoader.LoadAsync(2);
    var task3 = droidLoader.LoadAsync(3);

    // Await the results... Control is yielded to the framework and the loader is fired.
    var results = Task.WhenAll(task1, task2, task3);

    // We have the results, but let's load some more! Run ensures that asynchronous
    // continuations behave like the initial call - ID's should be collected and
    // fetched as a batch after continuations have run.
    var task4 = droidLoader.LoadAsync(4);
    var task5 = droidLoader.LoadAsync(5);

    // Return all our results.
    return (await Task.WhenAll(task4, task5)).Concat(results);
));
```

### Example 2: Unbound/implicit context

```csharp
// Create a floating/unbound loader that will attach itself to the context
// that's currently active at the time a load method is called.
var personLoader = new DataLoader<int, Person>(ids =>
{
    using (var db = new StarWarsContext())
    {
        return db.Person.Where(p => ids.Contains(p.Id)).ToListAsync();
    }
});

var results = await DataLoaderContext.Run(async () =>
{
    // We have an implicit context here.
    Debug.Assert(DataLoaderContext.Current != null);

    // Queue up some person loads.
    var task1 = personLoader.LoadAsync(1);
    var task2 = personLoader.LoadAsync(2);
    var task3 = personLoader.LoadAsync(3);

    // Await the results... Control is yielded to the framework and the loader is fired.
    var results = await Task.WhenAll(task1, task2, task3);

    // We have the results, but let's load some more! Run ensures that asynchronous
    // continuations behave like the initial call - ID's should be collected and
    // fetched as a batch after continuations have run.
    var task4 = personLoader.LoadAsync(4);
    var task5 = personLoader.LoadAsync(5);

    // Return all our results.
    return (await Task.WhenAll(task4, task5)).Concat(results);
});
```

## To Do

- [x] Basic support
- [x] Support async fetching
- [ ] Cancellation
- [ ] Benchmarks
- [ ] Multithreaded performance

## Ideas

- [ ] Single worker thread to service loaders
- [ ] Sync context to handle async/await in load continuations
