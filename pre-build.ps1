# Install .NET CLI Tools
if ($env:BuildRunner) { & ./tools/dotnet-install.ps1 }

# Add path to the .NET CLI
Write-Output "##myget[setParameter name='PATH' value='$env:PATH']"

# Run GitVersion
Invoke-Expression "$env:GitVersion /output buildserver"
