@echo off
echo === Tabular MCP Server Installation ===
echo.
echo This script will help you set up the Tabular MCP Server.
echo.
echo Step 1: Ensure Power BI Desktop is running with a PBIX file open
pause
echo.
echo Step 2: Running Power BI discovery...
cd /d "%~dp0app"
pbi-local-mcp.exe discover-pbi
echo.
if %ERRORLEVEL% EQU 0 (
    echo Setup completed successfully!
    echo.
    echo Next steps:
    echo 1. Copy the path: %~dp0app\pbi-local-mcp.exe
    echo 2. Add MCP configuration to VS Code
    echo 3. See docs\DEPLOYMENT.md for detailed instructions
) else (
    echo Setup failed. Please check:
    echo - Power BI Desktop is running
    echo - A PBIX file is open
    echo - No firewall is blocking the connection
)
echo.
pause
