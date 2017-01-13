@echo off

dotnet restore
if not "%errorlevel%"=="0" goto failure

dotnet test test/DataLoader.Tests
if not "%errorlevel%"=="0" goto failure

dotnet pack src/DataLoader -c Release
if not "%errorlevel%"=="0" goto failure

:success
exit 0

:failure
exit -1