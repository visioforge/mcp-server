@echo off
REM Quick MCP Build Script (skips IndexBuilder if XML exists)
REM Use this for fast rebuilds when deployment guides haven't changed

setlocal enabledelayedexpansion

echo ========================================
echo VisioForge MCP Quick Build
echo ========================================
echo.

set MCP_SERVER=%~dp0VisioForge.MCP.Server
set MCP_TESTS=%~dp0VisioForge.MCP.Tests

echo [1/2] Building MCP Server (Release)...
echo ========================================
cd /d "%MCP_SERVER%"
dotnet build -c Release
if errorlevel 1 (
    echo ERROR: Failed to build MCP Server
    exit /b 1
)
echo.

echo [2/2] Running tests...
echo ========================================
cd /d "%MCP_TESTS%"
dotnet test -c Release
if errorlevel 1 (
    echo ERROR: Tests failed
    exit /b 1
)
echo.

echo ========================================
echo QUICK BUILD SUCCESSFUL!
echo ========================================
echo.
echo To run the MCP server:
echo   cd "%MCP_SERVER%"
echo   dotnet run
echo.

cd /d "%~dp0"
exit /b 0
