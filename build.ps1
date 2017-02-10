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
dotnet msbuild test/DataLoader.Tests/DataLoader.Tests.csproj /t:Restore,VSTest /v:normal /p:Configuration=$Configuration
Test-ExitCode

dotnet msbuild src/DataLoader/DataLoader.csproj /t:Clean,Restore,Build,Pack /v:normal /p:Configuration=$Configuration
Test-ExitCode

# Invoke-BuildStep { dotnet clean }
# Invoke-BuildStep { dotnet test ./test/DataLoader.Tests/DataLoader.Tests.csproj --configuration $Configuration }
# Invoke-BuildStep { dotnet pack ./src/DataLoader/DataLoader.csproj --configuration $Configuration }