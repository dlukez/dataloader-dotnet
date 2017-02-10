# Let's use GitVersion (Y)
& "$env:GitVersion" /output buildserver

# Download the SDK
& ./tools/dotnet-install.ps1 -Version 1.0.0-rc4-004771 -InstallDir ./tools/dotnet
