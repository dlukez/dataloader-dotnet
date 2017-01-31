param (
  [Parameter(Position = 1)]
  [string]$PrereleaseTag = $env:PrereleaseTag,
  [string]$Configuration = "Release"
)

dotnet restore DataLoader.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build DataLoader.sln --configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test test/DataLoader.Tests/DataLoader.Tests.csproj --configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet pack src/DataLoader/DataLoader.csproj --configuration $Configuration --include-symbols --version-suffix $PrereleaseTag
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

exit 0
