using System.Text.RegularExpressions;
using VisioForge.MCP.Shared;
using YamlDotNet.Serialization;

namespace VisioForge.MCP.IndexBuilder;

/// <summary>
/// Parses deployment guide markdown files into DeploymentGuide objects
/// </summary>
public class DeploymentGuideParser
{
    private readonly string _guidesRootPath;

    public DeploymentGuideParser(string guidesRootPath)
    {
        _guidesRootPath = guidesRootPath;
    }

    public List<DeploymentGuide> ParseAll()
    {
        var guides = new List<DeploymentGuide>();

        // A missing directory here is a packaging or wiring mistake, never a legitimate
        // runtime state — the guides live beside this builder in the repo. Returning an
        // empty list on a warning is how production ended up serving an index with zero
        // deployment guides while every caller of list_deployment_guides was told,
        // politely, that nothing matched their filters.
        if (!Directory.Exists(_guidesRootPath))
        {
            throw new DirectoryNotFoundException(
                $"Deployment guides directory not found: {_guidesRootPath}. "
                + "Pass the correct path as the 5th argument to the index builder.");
        }

        var markdownFiles = Directory.GetFiles(_guidesRootPath, "*.md", SearchOption.AllDirectories);
        Console.WriteLine($"Found {markdownFiles.Length} deployment guide files");

        foreach (var file in markdownFiles)
        {
            try
            {
                var guide = ParseGuide(file);
                if (guide != null)
                {
                    guides.Add(guide);
                    Console.WriteLine($"  Parsed: {guide.Id} ({guide.Title})");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error parsing {file}: {ex.Message}");
            }
        }

        return guides;
    }

    private DeploymentGuide? ParseGuide(string filePath)
    {
        var content = File.ReadAllText(filePath);

        // Extract YAML front matter
        var frontMatterMatch = Regex.Match(content, @"^---\s*\n(.*?)\n---\s*\n(.*)$",
            RegexOptions.Singleline);

        if (!frontMatterMatch.Success)
        {
            Console.WriteLine($"Warning: No YAML front matter in {filePath}");
            return null;
        }

        var yamlContent = frontMatterMatch.Groups[1].Value;
        var markdownContent = frontMatterMatch.Groups[2].Value;

        // Parse YAML front matter
        var deserializer = new DeserializerBuilder().Build();
        var frontMatter = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        // Handle empty or null YAML front matter
        if (frontMatter == null)
        {
            Console.WriteLine($"Warning: Empty YAML front matter in {filePath}");
            frontMatter = new Dictionary<string, object>();
        }

        var guide = new DeploymentGuide
        {
            Id = GetValue<string>(frontMatter, "id") ?? Path.GetFileNameWithoutExtension(filePath),
            Title = GetValue<string>(frontMatter, "title") ?? "",
            Category = GetValue<string>(frontMatter, "category") ?? DetermineCategory(filePath),
            Summary = GetValue<string>(frontMatter, "summary") ?? "",
            Difficulty = GetValue<string>(frontMatter, "difficulty") ?? "intermediate",
            Platforms = GetListValue(frontMatter, "platforms"),
            ProjectTypes = GetListValue(frontMatter, "projectTypes"),
            SdkTypes = GetListValue(frontMatter, "sdkTypes"),
            Tags = GetListValue(frontMatter, "tags"),
            Content = markdownContent.Trim(),
        };

        // Parse code snippets from markdown
        guide.CodeSnippets = ExtractCodeSnippets(markdownContent);

        // Parse NuGet package references
        guide.NuGetPackages = ExtractNuGetPackages(markdownContent);

        return guide;
    }

    private string DetermineCategory(string filePath)
    {
        if (filePath.Contains("\\platforms\\") || filePath.Contains("/platforms/")) return "platform";
        if (filePath.Contains("\\project-types\\") || filePath.Contains("/project-types/")) return "project-type";
        if (filePath.Contains("\\scenarios\\") || filePath.Contains("/scenarios/")) return "scenario";
        if (filePath.Contains("\\packages\\") || filePath.Contains("/packages/")) return "package";
        return "general";
    }

    private List<CodeSnippet> ExtractCodeSnippets(string markdown)
    {
        var snippets = new List<CodeSnippet>();
        var codeBlockRegex = new Regex(@"```(\w+)\s*\n(.*?)\n```",
            RegexOptions.Singleline);

        var matches = codeBlockRegex.Matches(markdown);
        int snippetNumber = 1;
        foreach (Match match in matches)
        {
            snippets.Add(new CodeSnippet
            {
                Title = $"Code Snippet {snippetNumber++}",
                Language = match.Groups[1].Value,
                Code = match.Groups[2].Value.Trim(),
            });
        }

        return snippets;
    }

    private List<NuGetPackageReference> ExtractNuGetPackages(string markdown)
    {
        var packages = new List<NuGetPackageReference>();
        var packageRegex = new Regex(
            @"<PackageReference\s+Include=""([^""]+)""\s+Version=""([^""]+)""");

        var matches = packageRegex.Matches(markdown);
        foreach (Match match in matches)
        {
            var matchPosition = match.Index;
            var optionalCommentPos = markdown.LastIndexOf("<!-- Optional", matchPosition);
            var requiredCommentPos = markdown.LastIndexOf("<!-- Required", matchPosition);

            // Determine if package is required or optional based on most recent comment
            bool isRequired = requiredCommentPos > optionalCommentPos;

            packages.Add(new NuGetPackageReference
            {
                PackageId = match.Groups[1].Value,
                Version = match.Groups[2].Value,
                Required = isRequired,
            });
        }

        return packages;
    }

    private T? GetValue<T>(Dictionary<string, object>? dict, string key)
    {
        if (dict != null && dict.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
                return typedValue;

            // Try to convert string to T
            if (typeof(T) == typeof(string) && value != null)
                return (T)(object)value.ToString()!;
        }
        return default;
    }

    private List<string> GetListValue(Dictionary<string, object>? dict, string key)
    {
        if (dict != null && dict.TryGetValue(key, out var value))
        {
            if (value is List<object> list)
                return list.Select(o => o?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();

            if (value is string str)
                return new List<string> { str };
        }
        return new List<string>();
    }
}
