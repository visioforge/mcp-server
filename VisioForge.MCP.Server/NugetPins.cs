namespace VisioForge.MCP.Server;

/// <summary>
/// Versions of the native redistributable packages emitted by
/// <see cref="Tools.DeploymentTools.GetNuGetPackagesSnippet"/>.
/// </summary>
/// <remarks>
/// These are deliberately the versions PUBLISHED ON NUGET.ORG, not the ones the
/// internal demo tree pins. The demos build against packages that have not been
/// pushed yet (Windows natives sit at 2026.8.29 there), and a developer who
/// copy-pastes such a version gets NU1102 on the very first restore. That is not
/// hypothetical: this file exists because the snippet tool served
/// VisioForge.CrossPlatform.Core.Linux.x64 2025.11.0, a version that was never
/// published at all, so every Linux evaluation died on `dotnet restore`.
///
/// The managed wrapper version is NOT here — it comes from ApiIndexService.SdkVersion,
/// which the index builder derives from VisioForge.Core.csproj.
///
/// Last verified against api.nuget.org: 2026-11-06. DeploymentGuideTests keeps these
/// constants equal to _SOURCE/VisioForge.PackageVersions.props, so a release that moves
/// the catalog fails the build here instead of shipping stale versions.
/// </remarks>
internal static class NugetPins
{
    /// <summary>
    /// Every Windows-native redistributable that ships on one cadence:
    /// CrossPlatform.Core.Windows.*, Libav.Windows.*, and the optional
    /// AWS.Windows.x64, Basler.Windows.x64 and Spinnaker.Windows.x64.
    /// They are all on this version today; if one ever diverges it needs its own
    /// constant rather than a silent ride on this one, which would emit a version
    /// that does not exist and fail restore with NU1102.
    /// </summary>
    public const string Windows = "2026.11.3";

    /// <summary>VisioForge.CrossPlatform.Core.Linux.x64.</summary>
    public const string Linux = "2026.10.26";

    /// <summary>VisioForge.CrossPlatform.Core.Android.</summary>
    public const string Android = "2026.11.5";

    /// <summary>VisioForge.CrossPlatform.Core.iOS.</summary>
    public const string IOS = "2026.11.5";

    /// <summary>VisioForge.CrossPlatform.Core.macOS and Core.macCatalyst.</summary>
    public const string Apple = "2026.11.5";

    /// <summary>VisioForge.CrossPlatform.OpenCV.Windows.x64.</summary>
    public const string OpenCV = "2026.11.3";

    /// <summary>
    /// The Android Java bindings project the demos reference. X10 is what the demo
    /// tree uses (83 references against 4 for the retired X9); there is no X8.
    /// </summary>
    public const string AndroidBindingsProject = "VisioForge.Core.Android.X10.csproj";

    /// <summary>
    /// Trims a 4-part assembly version to the 3-part form NuGet publishes.
    /// The index carries "2026.8.16.0"; no such NuGet version exists.
    /// </summary>
    public static string ToNuGetVersion(string assemblyVersion)
    {
        var parts = assemblyVersion.Split('.');
        return parts.Length > 3 ? string.Join('.', parts[..3]) : assemblyVersion;
    }
}
