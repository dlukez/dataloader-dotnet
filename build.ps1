param (
  [Parameter(Mandatory=$true)]
  [string]$PrereleaseTag,
  [Parameter()]
  [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'

if (-not $Configuration) {
  if ($2 -eq "") {
    $Configuration = "Release"
  } else {
    $Configuration = "$2"
  }
}

if (-not $PrereleaseTag) {
  $PrereleaseTag = "$1"
}

dotnet restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build src/DataLoader/ --configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test test/DataLoader.Tests/ --configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet pack src/DataLoader/ --configuration $Configuration --version-suffix $PrereleaseTag
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
