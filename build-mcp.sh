#!/bin/bash
# VisioForge MCP Server Build Script
# This script builds VisioForge.Core, runs IndexBuilder, and builds the MCP Server

set -e  # Exit on error

echo "========================================"
echo "VisioForge MCP Server Build Script"
echo "========================================"
echo ""

# Determine script directory and root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# The VisioForge SDK checkout the index is built from: VF_SDK_ROOT, or a sibling directory.
SDK_ROOT_INPUT="${VF_SDK_ROOT:-$SCRIPT_DIR/../MediaFrameworkDotnet}"
if [ ! -d "$SDK_ROOT_INPUT/_SOURCE" ]; then
    echo "ERROR: no SDK checkout at $SDK_ROOT_INPUT (set VF_SDK_ROOT to the MediaFrameworkDotnet root)."
    exit 1
fi
ROOT_DIR="$(cd "$SDK_ROOT_INPUT" && pwd)"
# Absolute, for the test step: it runs from the test output directory.
export VF_SDK_ROOT="$ROOT_DIR"

# Set paths
CORE_DIR="$ROOT_DIR/_SOURCE/VisioForge.Core"
CORE_PROJECT="$CORE_DIR/VisioForge.Core.csproj"
PACKAGE_CATALOG="$ROOT_DIR/_SOURCE/VisioForge.PackageVersions.props"
# Release, and found by search rather than a hardcoded TFM. _SOURCE/Directory.Build.props
# turns GenerateDocumentationFile OFF for Debug unless /p:GenerateXmlDocs=true, so the old
# bin/Debug/net9.0 path could never exist — and every "rebuild the index" message in this
# repo points the operator at this script.
CORE_XML=""
CORE_SOURCE="$CORE_DIR"
INDEX_BUILDER="$SCRIPT_DIR/VisioForge.MCP.IndexBuilder"
MCP_SERVER="$SCRIPT_DIR/VisioForge.MCP.Server"
MCP_TESTS="$SCRIPT_DIR/VisioForge.MCP.Tests"
OUTPUT_JSON="$MCP_SERVER/Data/api-index.json"
DEPLOYMENT_GUIDES="$MCP_SERVER/Data/deployment-guides"
# Evaluate the shared catalog; the project's Version is an MSBuild expression.
SDK_VERSION=$(DOTNET_NOLOGO=1 dotnet msbuild "$PACKAGE_CATALOG" -nologo -getProperty:VfSdkBuildVersion)
if [[ ! "$SDK_VERSION" =~ ^[0-9]+(\.[0-9]+){2,3}$ ]]; then
    echo "ERROR: could not read VfSdkBuildVersion from $PACKAGE_CATALOG"
    exit 1
fi
echo "SDK version from catalog: $SDK_VERSION"

# VisioForge.Core.csproj imports deps.props, which is gitignored and written by the
# switch-to-*.ps1 scripts. Without it the build dies with MSB4019 and an empty
# TargetFramework, which reads like an SDK problem and is not one.
if [ ! -f "$CORE_DIR/deps.props" ]; then
    echo "ERROR: $CORE_DIR/deps.props is missing."
    echo "VisioForge.Core cannot be built until the source tree is switched to a build"
    echo "profile. For the index use the docs profile — its seven net10.0-x targets"
    echo "include the Windows one this script needs:"
    echo "    pwsh $ROOT_DIR/_SOURCE/switch-to-docs.ps1"
    echo "Then re-run this script."
    exit 1
fi

echo "[1/5] Building VisioForge.Core (Release, with XML docs)..."
echo "========================================"
dotnet build "$CORE_PROJECT" -c Release -p:GenerateXmlDocs=true
echo ""

echo "[2/5] Locating XML documentation..."
echo "========================================"
# The XML doc is per target framework and documents only the members compiled for that
# one, and the docs profile builds seven of them. Choosing whichever the filesystem
# returned first made the index depend on directory order — and `find` does not sort
# while the .cmd's `dir /b /s` does, so the two scripts could index different things.
#
# The Windows TFM, deliberately: it is the only one that carries the legacy DirectShow
# cores (VideoCaptureCore, MediaPlayerCore, VideoEditCore), and those are three of the
# ten names in SourceAnalyzer's core-class allowlist. Measured on the docs profile it
# also has the most members by a wide margin — 59,519 against 39,888 for plain net10.0 —
# so nothing cross-platform is lost by preferring it.
CORE_XML=$(find "$CORE_DIR/bin/Release" -type d -name "net*-windows*" 2>/dev/null \
    -exec test -f "{}/VisioForge.Core.xml" \; -print 2>/dev/null | sort | head -1)
if [ -n "$CORE_XML" ]; then
    CORE_XML="$CORE_XML/VisioForge.Core.xml"
else
    # No Windows target built: fall back to the richest of whatever is there, so the
    # script still works on a Linux or macOS box, minus the legacy cores.
    CORE_XML=$(find "$CORE_DIR/bin/Release" -name "VisioForge.Core.xml" -type f 2>/dev/null \
        | xargs -I{} sh -c 'printf "%s %s\n" "$(wc -c < "{}")" "{}"' 2>/dev/null | sort -rn | head -1 | cut -d" " -f2-)
    if [ -n "$CORE_XML" ]; then
        echo "WARNING: no Windows target framework was built, so the index will omit the"
        echo "WARNING: legacy DirectShow cores. Using $CORE_XML"
    fi
fi
if [ -z "$CORE_XML" ] || [ ! -f "$CORE_XML" ]; then
    echo "ERROR: XML documentation not found under $CORE_DIR/bin/Release"
    echo "The build above should have produced it; check that GenerateDocumentationFile"
    echo "is not disabled for this configuration in _SOURCE/Directory.Build.props."
    exit 1
fi
echo "Using XML docs from: $CORE_XML"
echo "XML documentation found: $CORE_XML"
echo ""

echo "[3/5] Running IndexBuilder to generate api-index.json..."
echo "========================================"
cd "$INDEX_BUILDER"
dotnet run --configuration Release -- "$CORE_XML" "$CORE_SOURCE" "$OUTPUT_JSON" "$SDK_VERSION" "$DEPLOYMENT_GUIDES"
echo ""

echo "[4/5] Building MCP Server (Release)..."
echo "========================================"
cd "$MCP_SERVER"
dotnet build -c Release
echo ""

echo "[5/5] Running tests..."
echo "========================================"
# No --no-build: step [4/5] builds only the server project, so on a clean clone the
# test project is neither restored nor built and --no-build fails with NETSDK1004.
cd "$SCRIPT_DIR"
dotnet test VisioForge.MCP.sln -c Release
echo ""

echo "========================================"
echo "BUILD SUCCESSFUL!"
echo "========================================"
echo ""
echo "MCP Server built successfully:"
echo "  Server: $MCP_SERVER/bin/Release/net10.0/VisioForge.MCP.Server.dll"
echo "  API Index: $OUTPUT_JSON"
echo ""
echo "If you touched Tools/ExampleTools.cs, verify the snippets actually compile:"
echo "  python3 $SCRIPT_DIR/tools/verify-examples.py"
echo "  (nothing else compiles them — they are string literals)"
echo ""
echo "To run the MCP server:"
echo "  cd \"$MCP_SERVER\""
echo "  dotnet run"
echo ""

cd "$SCRIPT_DIR"
exit 0
