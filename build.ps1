# Setup
$ErrorActionPreference = "Stop"

if (-not $env:Configuration) {
    $env:Configuration = "Release"
}

if (-not $env:PackageVersion) {
    $env:PackageVersion = (gitversion | ConvertFrom-Json).NuGetVersionV2
}

if ($env:BuildRunner) {
    & ./tools/dotnet-install.ps1 -Version 1.0.0-rc4-004771 -Architecture x86
}

# Build
dotnet msbuild src/DataLoader/DataLoader.csproj /t:Restore,Rebuild,Pack /p:IncludeSymbols=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Test
dotnet msbuild test/DataLoader.Tests/DataLoader.Tests.csproj /t:Restore,VSTest
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# End
exit
