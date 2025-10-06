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

echo Step 3: Publishing MCP Server (single-file, self-contained)...
dotnet publish pbi-local-mcp/pbi-local-mcp.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish/single-file

echo Step 4: Publishing Discovery CLI (single-file, self-contained)...
dotnet publish pbi-local-mcp.DiscoverCli/pbi-local-mcp.DiscoverCli.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish/single-file

echo Step 5: Creating release package...
powershell -ExecutionPolicy Bypass -File scripts/package-releases.ps1

echo.
echo === Publishing Complete ===
echo.
echo Output directory: releases\
dir /b releases\

echo.
echo Release information: releases\RELEASE-INFO.md
echo.
echo Publishing completed successfully!
pause