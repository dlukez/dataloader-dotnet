# Params
param (
    [string]$Configuration = "Release",
    [switch]$SkipInstall = $false,
    [string]$PrereleaseTag
)

if (-not $PrereleaseTag) {
    $PrereleaseTag = "dev"; 
}

# Script behavior
$ErrorActionPreference = "Stop";

# Install the SDK
if (-not $SkipInstall) {
    & .\tools\dotnet-install.ps1 -Architecture x64;
}

# Build script
dotnet msbuild test/DataLoader.Tests/DataLoader.Tests.csproj /t:Restore,VSTest /p:Configuration=$Configuration 
dotnet msbuild src/DataLoader/DataLoader.csproj /t:Clean,Restore,Build,Pack /p:Configuration=$Configuration
# dotnet clean
# dotnet test ./test/DataLoader.Tests/DataLoader.Tests.csproj --configuration $Configuration
# dotnet pack ./src/DataLoader/DataLoader.csproj --configuration $Configuration