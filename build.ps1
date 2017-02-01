param (
  [Parameter(Position = 1)]
  [string]$PrereleaseTag = $env:PrereleaseTag,
  [string]$Configuration = "Release",
  [switch]$SkipInstall
)

if (-not $PrereleaseTag) {
  $PrereleaseTag = "dev"
}

if (-not $SkipInstall) {
  & .\dotnet-install.ps1 -Architecture x64
}

dotnet restore DataLoader.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build DataLoader.sln -configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test test\DataLoader.Tests\DataLoader.Tests.csproj --configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet pack src\DataLoader\DataLoader.csproj --configuration $Configuration --version-suffix $PrereleaseTag --include-symbols
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

exit 0
