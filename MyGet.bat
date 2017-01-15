@echo off

dotnet restore ^
&& dotnet build --configuration Release src/DataLoader ^
&& dotnet test  --configuration Release test/DataLoader.Tests ^
&& dotnet pack  --configuration Release --no-build --version-suffix "%PrereleaseTag%" src/DataLoader

exit /b
