param (
  [Parameter(Position = 1)]
  [string]$PrereleaseTag = $env:PrereleaseTag,
  [string]$Configuration = "Release"
)

dotnet restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build src/DataLoader/ --configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test test/DataLoader.Tests/ --configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet pack src/DataLoader/ --configuration $Configuration --version-suffix $PrereleaseTag
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

exit 0