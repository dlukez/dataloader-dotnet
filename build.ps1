# Params
param (
    [string]$Configuration = $env:Configuration,
    [string]$PrereleaseTag = $env:PrereleaseTag
)

if (-not $Configuration) {
    $Configuration = "Release"
}

if (-not $PrereleaseTag) {
    $PrereleaseTag = "dev"
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

dotnet build --configuration $Configuration
Test-ExitCode

dotnet test ./test/DataLoader.Tests/DataLoader.Tests.csproj --configuration $Configuration
Test-ExitCode

dotnet pack ./src/DataLoader/DataLoader.csproj --configuration $Configuration --no-build --include-symbols --version-suffix $PrereleaseTag
Test-ExitCode
