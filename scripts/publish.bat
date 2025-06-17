@echo off
echo === Tabular MCP Server Publisher ===
echo.

:: Check if we're in the correct directory
if not exist "pbi-local-mcp\pbi-local-mcp.csproj" (
    echo Error: Please run this script from the project root directory.
    echo Expected to find: pbi-local-mcp\pbi-local-mcp.csproj
    pause
    exit /b 1
)

echo Step 1: Clean previous builds...
if exist "publish" rmdir /s /q "publish"
if exist "releases" rmdir /s /q "releases"

echo Step 2: Building project...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo Step 3: Publishing Self-Contained (win-x64)...
dotnet publish pbi-local-mcp/pbi-local-mcp.csproj -c Release -r win-x64 --self-contained -o ./publish/win-x64

echo Step 4: Publishing DiscoverCli (win-x64)...
dotnet publish pbi-local-mcp.DiscoverCli/pbi-local-mcp.DiscoverCli.csproj -c Release -r win-x64 --self-contained -o ./publish/win-x64

echo Step 5: Publishing Framework-Dependent (win-x64)...
dotnet publish pbi-local-mcp/pbi-local-mcp.csproj -c Release -r win-x64 --no-self-contained -o ./publish/win-x64-framework-dependent

echo Step 6: Publishing DiscoverCli Framework-Dependent (win-x64)...
dotnet publish pbi-local-mcp.DiscoverCli/pbi-local-mcp.DiscoverCli.csproj -c Release -r win-x64 --no-self-contained -o ./publish/win-x64-framework-dependent

echo Step 7: Publishing Single-File (win-x64)...
dotnet publish pbi-local-mcp/pbi-local-mcp.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish/win-x64-single-file

echo Step 8: Creating release packages...
powershell -ExecutionPolicy Bypass -File scripts/package-releases.ps1

echo.
echo === Publishing Complete ===
echo.
echo Available packages:
dir /b releases\*.zip

echo.
echo Release information: releases\RELEASE-INFO.md
echo.
echo Publishing completed successfully!
pause