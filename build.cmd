dotnet restore
dotnet build src/DataLoader tests/DataLoader.Tests -c Release
dotnet test tests/DataLoader.Tests
dotnet pack src/DataLoader


