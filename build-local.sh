PrereleaseTag=$1
Configuration=Release

dotnet restore
dotnet build src/DataLoader/ --configuration $Configuration
dotnet test test/DataLoader.Tests/ --configuration $Configuration
dotnet pack src/DataLoader/ --configuration $Configuration --version-suffix $PrereleaseTag --output ~/.nuget/feed/
