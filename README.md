DataLoader for .NET
===================

A port of Facebook's [DataLoader](https://github.com/facebook/dataloader) for .NET.

Originally began as a solution to the [select N+1 problem](https://github.com/graphql-dotnet/graphql-dotnet/issues/21) for [GraphQL in .NET](https://github.com/graphql-dotnet/graphql-dotnet.


Running the sample app
----------------------

```
cd example/StarWarsApp/
dotnet ef migrations add InitialSetup
dotnet ef database update
dotnet run
```


API
---

```csharp
var friendsLoader = new DataLoader<int, Droid>(ids =>
{
    using (var db = new StarWarsContext())
        return db.Friendships
            .Where(f => ids.Contains(f.HumanId))
            .Select(f => new {Key = f.HumanId, f.Droid})
            .ToLookup(f => f.Key, f => f.Droid);
});

Field<ListGraphType<CharacterInterface>>()
    .Name("friends2")
    .Resolve(ctx => friendsLoader.LoadAsync(ctx.Source.HumanId));
```
