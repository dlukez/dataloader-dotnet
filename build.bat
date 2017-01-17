dotnet restore || exit /b
dotnet build src/DataLoader/ --configuration %Configuration% || exit /b
dotnet test test/DataLoader.Tests/ --configuration %Configuration% || exit /b
dotnet pack src/DataLoader/ --configuration %Configuration% --version-suffix %PrereleaseTag% || exit /b
