Configuration="Release"
PrereleaseTag="local"

mkdir pkg

dotnet restore
dotnet build src/DataLoader/ --configuration $Configuration
dotnet test test/DataLoader.Tests/ --configuration $Configuration
dotnet pack src/DataLoader/ --configuration $Configuration --version-suffix $PrereleaseTag --output pkg/

cp pkg/*.nupkg ~/.nuget/feed/
rm -rf pkg/

