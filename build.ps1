param (
  [string]$Configuration = "Release",
  [switch]$SkipInstall = $false,
  [string]$PrereleaseTag
)

if (-not $PrereleaseTag) {
  $PrereleaseTag = "dev"
}

if (-not $SkipInstall) {
  & .\tools\dotnet-install.ps1 -Architecture x64
}

dotnet msbuild /p:Configuration=$Configuration;IncludeSymbols=$true;VersionSuffix=$PrereleaseTag /t:restore;build;test;pack

exit $LASTEXITCODE
