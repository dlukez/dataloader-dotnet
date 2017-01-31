param (
  [Parameter(Position = 1)]
  [string]$PrereleaseTag = $env:PrereleaseTag,
  [string]$Configuration = "Release"
)

if (-not $PrereleaseTag) {
  $PrereleaseTag = "dev"
}

dotnet restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build .\src\DataLoader\ --configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

cd .\test\DataLoader.Tests\
dotnet test --configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
cd ..\..\

cd .\src\DataLoader\
dotnet pack --configuration $Configuration --include-symbols --version-suffix $PrereleaseTag
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
cd ..\..\

exit 0
