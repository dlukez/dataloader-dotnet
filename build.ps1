# Params
param (
    [string]$Configuration = $env:Configuration,
    [string]$Version = $env:SemVer
)

if (-not $Version) {
    $Version = '0.0.0'
}

if (-not $Configuration) {
    $Configuration = "Release"
}

# Download the SDK
& ./tools/dotnet-install.ps1 -Version 1.0.0-rc4-004771 -Architecture x64

# Setup
$ErrorActionPreference = "Stop"

# Helpers
function Test-ExitCode {
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE 
    }
}

# Build script
dotnet clean
Test-ExitCode

dotnet restore
Test-ExitCode

dotnet msbuild src/DataLoader/DataLoader.csproj /t:Rebuild,Pack /p:Configuration=$Configuration /p:Version=$Version /p:IncludeSymbols=true
Test-ExitCode

dotnet test ./test/DataLoader.Tests/DataLoader.Tests.csproj --configuration $Configuration
Test-ExitCode

exit
