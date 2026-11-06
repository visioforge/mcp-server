@echo off
REM VisioForge MCP Server Build Script
REM This script builds VisioForge.Core, runs IndexBuilder, and builds the MCP Server

rem Delayed expansion stays off until the paths are known: it would strip a '!' from them.
setlocal disabledelayedexpansion
set DOTNET_NOLOGO=1

echo ========================================
echo VisioForge MCP Server Build Script
echo ========================================
echo.

REM Set paths
rem The VisioForge SDK checkout the index is built from: VF_SDK_ROOT, or a sibling directory.
set "ROOT_DIR=%VF_SDK_ROOT%"
if not defined ROOT_DIR set "ROOT_DIR=%~dp0..\MediaFrameworkDotnet"
rem Absolute: IndexBuilder and the tests run from other directories.
for %%I in ("%ROOT_DIR%") do set "ROOT_DIR=%%~fI"
if not exist "%ROOT_DIR%\_SOURCE\" echo ERROR: no SDK checkout at "%ROOT_DIR%" - set VF_SDK_ROOT to the MediaFrameworkDotnet root.& exit /b 1
set "VF_SDK_ROOT=%ROOT_DIR%"
set "PATH_CHECK=%ROOT_DIR%%~dp0"
if not "%PATH_CHECK%"=="%PATH_CHECK:!=%" echo ERROR: a '!' in "%ROOT_DIR%" or "%~dp0" is not supported by this script.& exit /b 1
setlocal enabledelayedexpansion
set CORE_DIR=%ROOT_DIR%\_SOURCE\VisioForge.Core
set CORE_PROJECT=%CORE_DIR%\VisioForge.Core.csproj
set PACKAGE_CATALOG=%ROOT_DIR%\_SOURCE\VisioForge.PackageVersions.props
rem Release, not Debug: _SOURCE\Directory.Build.props turns GenerateDocumentationFile
rem OFF for Debug unless /p:GenerateXmlDocs=true, so the old bin\Debug\net9.0 path could
rem never exist — and every "rebuild the index" message in this repo points here.
set CORE_XML=
set CORE_SOURCE=%CORE_DIR%
set INDEX_BUILDER=%~dp0VisioForge.MCP.IndexBuilder
set MCP_SERVER=%~dp0VisioForge.MCP.Server
set MCP_TESTS=%~dp0VisioForge.MCP.Tests
set OUTPUT_JSON=%MCP_SERVER%\Data\api-index.json
set DEPLOYMENT_GUIDES=%MCP_SERVER%\Data\deployment-guides
rem Evaluate the shared catalog; the project's Version is an MSBuild expression.
set SDK_VERSION=
for /f "delims=" %%v in ('dotnet msbuild "%PACKAGE_CATALOG%" -nologo -getProperty:VfSdkBuildVersion') do (
    if not defined SDK_VERSION set SDK_VERSION=%%v
)
rem Non-empty is not enough — it has to look like a version.
echo %SDK_VERSION%| findstr /r "^[0-9][0-9.]*$" >nul
if errorlevel 1 (
    echo ERROR: could not read VfSdkBuildVersion from %PACKAGE_CATALOG% ^(got "%SDK_VERSION%"^)
    exit /b 1
)
echo SDK version from catalog: %SDK_VERSION%

rem VisioForge.Core.csproj imports deps.props, which is gitignored and written by the
rem switch-to-*.ps1 scripts. Without it the build dies with MSB4019 and an empty
rem TargetFramework, which reads like an SDK problem and is not one.
if not exist "%CORE_DIR%\deps.props" (
    echo ERROR: %CORE_DIR%\deps.props is missing.
    echo VisioForge.Core cannot be built until the source tree is switched to a build
    echo profile. For the index use the docs profile - its seven net10.0-x targets
    echo include the Windows one this script needs:
    echo     pwsh %ROOT_DIR%\_SOURCE\switch-to-docs.ps1
    echo Then re-run this script.
    exit /b 1
)

echo [1/5] Building VisioForge.Core (Release, with XML docs)...
echo ========================================
dotnet build "%CORE_PROJECT%" -c Release -p:GenerateXmlDocs=true
if errorlevel 1 (
    echo ERROR: Failed to build VisioForge.Core
    exit /b 1
)
echo.

echo [2/5] Locating XML documentation...
echo ========================================
rem Same selection as build-mcp.sh. The XML doc is per target framework and documents
rem only the members compiled for that one, and Directory.Build.Docs.props declares
rem seven, so taking whichever the filesystem returned first would make the index
rem depend on directory order.
rem
rem The Windows one, deliberately: it is the only XML carrying the legacy DirectShow
rem cores (VideoCaptureCore, MediaPlayerCore, VideoEditCore), which are three of the ten
rem names in SourceAnalyzer's core-class allowlist, and it documents ~59,500 members
rem against ~39,900 for plain net10.0.
for /f "delims=" %%x in ('dir /b /s "%CORE_DIR%\bin\Release\net*-windows*\VisioForge.Core.xml" 2^>nul') do (
    if not defined CORE_XML set CORE_XML=%%x
)
if not defined CORE_XML (
    rem No Windows target built: fall back to any, minus the legacy cores.
    for /f "delims=" %%x in ('dir /b /s "%CORE_DIR%\bin\Release\VisioForge.Core.xml" 2^>nul') do (
        if not defined CORE_XML set CORE_XML=%%x
    )
    if defined CORE_XML echo WARNING: no Windows target framework was built, so the index will omit the legacy DirectShow cores.
)
if not defined CORE_XML (
    echo ERROR: XML documentation not found under %CORE_DIR%\bin\Release
    echo The build above should have produced it; check GenerateDocumentationFile
    echo in _SOURCE\Directory.Build.props.
    exit /b 1
)
echo XML documentation found: %CORE_XML%
echo.

echo [3/5] Running IndexBuilder to generate api-index.json...
echo ========================================
cd /d "%INDEX_BUILDER%"
dotnet run --configuration Release -- "%CORE_XML%" "%CORE_SOURCE%" "%OUTPUT_JSON%" "%SDK_VERSION%" "%DEPLOYMENT_GUIDES%"
if errorlevel 1 (
    echo ERROR: IndexBuilder failed
    exit /b 1
)
echo.

echo [4/5] Building MCP Server (Release)...
echo ========================================
cd /d "%MCP_SERVER%"
dotnet build -c Release
if errorlevel 1 (
    echo ERROR: Failed to build MCP Server
    exit /b 1
)
echo.

echo [5/5] Running tests...
echo ========================================
rem No --no-build: step [4/5] builds only the server project, so on a clean clone the
rem test project is neither restored nor built and --no-build fails with NETSDK1004.
cd /d "%~dp0"
dotnet test VisioForge.MCP.sln -c Release
if errorlevel 1 (
    echo ERROR: Tests failed
    exit /b 1
)
echo.

echo ========================================
echo BUILD SUCCESSFUL!
echo ========================================
echo.
echo MCP Server built successfully:
echo   Server: %MCP_SERVER%\bin\Release\net10.0\VisioForge.MCP.Server.dll
echo   API Index: %OUTPUT_JSON%
echo.
echo If you touched Tools\ExampleTools.cs, verify the snippets actually compile:
echo   python %~dp0tools\verify-examples.py
echo   ^(nothing else compiles them - they are string literals^)
echo.
echo To run the MCP server:
echo   cd "%MCP_SERVER%"
echo   dotnet run
echo.

cd /d "%~dp0"
exit /b 0
