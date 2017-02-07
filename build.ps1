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

function Confirm-ExitCode {
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

dotnet clean
Confirm-ExitCode

dotnet restore
Confirm-ExitCode

dotnet build ./src/DataLoader/DataLoader.csproj --configuration $Configuration --no-incremental
Confirm-ExitCode

dotnet test ./test/DataLoader.Tests/DataLoader.Tests.csproj --configuration $Configuration
Confirm-ExitCode

dotnet pack ./src/DataLoader/DataLoader.csproj --configuration $Configuration --no-build --include-symbols --version-suffix $PrereleaseTag
Confirm-ExitCode

exit 0
