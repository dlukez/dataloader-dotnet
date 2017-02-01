param (
  [string]$Configuration = "Release",
  [switch]$SkipInstall = $false
)

if (-not $PrereleaseTag) {
  $PrereleaseTag = "dev"
}

if (-not $SkipInstall) {
  & .\tools\dotnet-install.ps1 -Architecture x64
}

dotnet restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet msbuild /p:Configuration=$Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet pack src\DataLoader\DataLoader.csproj --configuration $Configuration --include-symbols
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

exit 0
