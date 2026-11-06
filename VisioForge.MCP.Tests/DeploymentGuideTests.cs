using System.Text.RegularExpressions;
using VisioForge.MCP.IndexBuilder;
using VisioForge.MCP.Server;
using Xunit;

namespace VisioForge.MCP.Tests;

/// <summary>
/// Guards the deployment guides served by <c>get_deployment_guide</c>.
/// </summary>
/// <remarks>
/// The guides are hand-maintained markdown data, served to agents verbatim, and no release
/// step touches them: they drifted two framework versions and three package waves behind the
/// samples before anyone noticed (#3592). These tests compare them against the one file that
/// does move every release, <c>_SOURCE/VisioForge.PackageVersions.props</c>.
/// </remarks>
public class DeploymentGuideTests
{
    /// <summary>The framework every guide must target; bump it with the release.</summary>
    private const int CurrentFrameworkMajor = 10;

    // A TFM, a .NET container base image, or a dotnet-install channel — compared against
    // CurrentFrameworkMajor rather than blocklisted, so the guard does not expire on the
    // next major the way a hardcoded "net5.0-net9.0" would. All three alternatives share
    // one group name, so the caller reads a single value whichever one matched.
    private static readonly Regex FrameworkMention = new(
        @"\bnet(?<major>[0-9]+)\.0|dotnet/(?:aspnet|runtime):(?<major>[0-9]+)\.0|--channel (?<major>[0-9]+)\.0",
        RegexOptions.Compiled);

    private static readonly Regex PackageReferenceElement = new(
        @"<PackageReference\s+(?<attrs>[^>]*?)/>",
        RegexOptions.Compiled);

    private static readonly Regex VfPackageElement = new(
        @"<VfPackage\s+(?<attrs>[^>]*?)/>",
        RegexOptions.Compiled);

    private static readonly Regex XmlAttribute = new(
        @"(?<name>\w+)=""(?<value>[^""]*)""",
        RegexOptions.Compiled);

    [Fact]
    public void Guides_Pin_Published_Package_Versions()
    {
        var published = LoadPublishedVersions();
        var failures = new List<string>();

        foreach (var file in GuideFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in PackageReferenceElement.Matches(text))
            {
                var attributes = ParseAttributes(match.Groups["attrs"].Value);
                if (!attributes.TryGetValue("Include", out var package)
                    || !package.StartsWith("VisioForge.", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!attributes.TryGetValue("Version", out var version))
                {
                    failures.Add($"{Name(file)}: {package} has no Version");
                    continue;
                }

                if (!published.TryGetValue(package, out var expected))
                {
                    failures.Add($"{Name(file)}: {package} is not in VisioForge.PackageVersions.props");
                }
                else if (version != expected)
                {
                    failures.Add($"{Name(file)}: {package} pinned at {version}, published version is {expected}");
                }
            }
        }

        Assert.True(failures.Count == 0,
            "Deployment guides pin package versions that a copy-pasting customer cannot restore, "
            + "or that are behind the current release:\n  " + string.Join("\n  ", failures));
    }

    [Fact]
    public void Guides_Target_Current_Framework()
    {
        var failures = new List<string>();

        foreach (var file in GuideFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in FrameworkMention.Matches(text))
            {
                if (int.Parse(match.Groups["major"].Value) != CurrentFrameworkMajor)
                {
                    failures.Add($"{Name(file)}: {LineOf(text, match.Index).Trim()}");
                }
            }
        }

        // One line can carry several mentions (net10.0-windows10.0.19041.0); report it once.
        var unique = failures.Distinct().ToList();
        Assert.True(unique.Count == 0,
            $"Deployment guides target a framework other than net{CurrentFrameworkMajor}.0:\n  "
            + string.Join("\n  ", unique));
    }

    [Fact]
    public void NugetPins_Match_Published_Versions()
    {
        // get_nuget_packages hands these out to agents; a stale constant is the same defect
        // as a stale guide, one tool call away.
        var published = LoadPublishedVersions();
        var pins = new (string Constant, string Package, string Value)[]
        {
            ("Windows", "VisioForge.CrossPlatform.Core.Windows.x64", NugetPins.Windows),
            ("Linux", "VisioForge.CrossPlatform.Core.Linux.x64", NugetPins.Linux),
            ("Android", "VisioForge.CrossPlatform.Core.Android", NugetPins.Android),
            ("IOS", "VisioForge.CrossPlatform.Core.iOS", NugetPins.IOS),
            ("Apple", "VisioForge.CrossPlatform.Core.macOS", NugetPins.Apple),
            ("OpenCV", "VisioForge.CrossPlatform.OpenCV.Windows.x64", NugetPins.OpenCV),
        };

        var failures = new List<string>();
        foreach (var (constant, package, value) in pins)
        {
            if (!published.TryGetValue(package, out var expected))
            {
                failures.Add($"{package} is not in VisioForge.PackageVersions.props");
            }
            else if (value != expected)
            {
                failures.Add($"NugetPins.{constant} = {value}, {package} published at {expected}");
            }
        }

        Assert.True(failures.Count == 0,
            "NugetPins constants are behind VisioForge.PackageVersions.props:\n  "
            + string.Join("\n  ", failures));
    }

    [Fact]
    public void Every_Guide_Parses()
    {
        // DeploymentGuideParser.ParseAll swallows a per-file exception onto stderr, and the
        // deploy script only refuses an index with NO guides — one guide lost that way ships
        // as "not found" to every caller.
        var files = GuideFiles();
        var guides = new DeploymentGuideParser(GuidesDirectory()).ParseAll();

        Assert.Equal(files.Length, guides.Count);
        Assert.All(guides, g =>
        {
            Assert.False(string.IsNullOrWhiteSpace(g.Id));
            Assert.False(string.IsNullOrWhiteSpace(g.Title));
            Assert.NotEmpty(g.Platforms);
        });
    }

    private static string[] GuideFiles() =>
        Directory.GetFiles(GuidesDirectory(), "*.md", SearchOption.AllDirectories);

    private static string Name(string path) => Path.GetFileName(path);

    private static string LineOf(string text, int index)
    {
        var start = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var end = text.IndexOf('\n', index);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static string GuidesDirectory() =>
        Path.Combine(McpRoot(), "VisioForge.MCP.Server", "Data", "deployment-guides");

    private static Dictionary<string, string> LoadPublishedVersions()
    {
        var propsPath = Path.Combine(SdkRoot(), "_SOURCE", "VisioForge.PackageVersions.props");
        var props = File.ReadAllText(propsPath);

        var properties = new Dictionary<string, string>();
        foreach (Match match in Regex.Matches(props, @"<(?<name>VfSdk\w+Version)>(?<value>[^<]+)</\k<name>>"))
        {
            properties[match.Groups["name"].Value] = match.Groups["value"].Value.Trim();
        }

        var versions = new Dictionary<string, string>();
        foreach (Match match in VfPackageElement.Matches(props))
        {
            var attributes = ParseAttributes(match.Groups["attrs"].Value);
            if (!attributes.TryGetValue("Include", out var id)
                || !attributes.TryGetValue("PublishedVersion", out var published))
            {
                continue;
            }

            var propertyMatch = Regex.Match(published, @"^\$\((?<name>\w+)\)$");
            if (propertyMatch.Success)
            {
                Assert.True(properties.TryGetValue(propertyMatch.Groups["name"].Value, out var resolved),
                    $"{propsPath} does not define {propertyMatch.Groups["name"].Value}");
                published = resolved!;
            }

            versions[id] = published;
        }

        return versions;
    }

    private static Dictionary<string, string> ParseAttributes(string attributes)
    {
        var parsed = new Dictionary<string, string>();
        foreach (Match attribute in XmlAttribute.Matches(attributes))
        {
            parsed[attribute.Groups["name"].Value] = attribute.Groups["value"].Value;
        }

        return parsed;
    }

    private static string McpRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VisioForge.MCP.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"VisioForge.MCP.sln not found above {AppContext.BaseDirectory}.");
    }

    // The package catalog lives in the SDK checkout: VF_SDK_ROOT, or a sibling MediaFrameworkDotnet.
    private static string SdkRoot()
    {
        // A relative VF_SDK_ROOT is taken from the repository root, whichever directory the tests run in.
        var root = Environment.GetEnvironmentVariable("VF_SDK_ROOT");
        root = Path.GetFullPath(string.IsNullOrEmpty(root) ? Path.Combine("..", "MediaFrameworkDotnet") : root, McpRoot());

        if (!Directory.Exists(Path.Combine(root, "_SOURCE")))
        {
            throw new DirectoryNotFoundException(
                $"No SDK checkout at {Path.GetFullPath(root)}; set VF_SDK_ROOT to the MediaFrameworkDotnet root.");
        }

        return root;
    }
}
