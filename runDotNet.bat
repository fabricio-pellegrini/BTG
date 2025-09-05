@echo off
echo Checking DotNet installation...
dotnet --version
if %errorlevel% neq 0 (
    echo .NET SDK is not installed or not added to PATH.
    exit /b 1
)
cd dotnet
echo Running .NET application...
dotnet run
echo Cleaning up...
dotnet clean
cd ..
