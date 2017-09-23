$ErrorActionPreference = "Stop"

# Setup
if (-not $env:Configuration) { $env:Configuration = "Release" }
function Invoke-BuildStep { param([scriptblock]$cmd) & $cmd; if ($LASTEXITCODE -ne 0) { exit 1 } }

# Run GitVersion
if (-not $env:BuildRunner) { $env:PackageVersion = (gitversion | ConvertFrom-Json).NuGetVersionV2 }
else { Invoke-Expression "$env:GitVersion /output buildserver" }

# Run build
Invoke-BuildStep { dotnet restore src/DataLoader/DataLoader.csproj }
Invoke-BuildStep { dotnet restore test/DataLoader.Tests/DataLoader.Tests.csproj }
Invoke-BuildStep { dotnet build src/DataLoader/DataLoader.csproj }
Invoke-BuildStep { dotnet build test/DataLoader.Tests/DataLoader.Tests.csproj --no-dependencies }
Invoke-BuildStep { dotnet test test/DataLoader.Tests/DataLoader.Tests.csproj --no-build }
Invoke-BuildStep { dotnet pack src/DataLoader/DataLoader.csproj --include-symbols --no-build }

# End
exit

