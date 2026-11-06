using System.Text.RegularExpressions;

namespace VisioForge.MCP.IndexBuilder;

/// <summary>
/// Analyzes C# source files to extract metadata that XML docs alone don't capture,
/// such as class inheritance, interface implementations, media block pad definitions,
/// and media block categorization by directory structure.
/// </summary>
public class SourceAnalyzer
{
    private readonly string _sourceRoot;

    // Maps class full name -> (baseClass, interfaces)
    private readonly Dictionary<string, (string? BaseClass, List<string> Interfaces)> _inheritance = new();

    // Maps class name -> media block category (derived from directory)
    private readonly Dictionary<string, string> _mediaBlockCategories = new();

    // Maps class name -> pad definitions found in source
    private readonly Dictionary<string, List<PadInfo>> _pads = new();

    // Known Core SDK classes
    private static readonly HashSet<string> CoreSdkClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // MediaInfoReaderX, not ...CoreX: the "Core" spelling matches no type in
        // VisioForge.Core, so the class was silently absent from every index while
        // list_sdk_classes went on advertising it in its own tool description.
        "VideoCaptureCoreX", "VideoEditCoreX", "MediaPlayerCoreX",
        "MediaInfoReaderX", "SimplePlayerCoreX",
        // EquirectangularProcessorX is not here: the whole type is commented out in
        // _SOURCE/VisioForge.Core/FastImageProcessingX/EquirectangularProcessorX.cs,
        // so listing it only advertised a class no index could ever contain.
        "ColorspaceConverterX",
        // Legacy
        "VideoCaptureCore", "VideoEditCore", "MediaPlayerCore",
    };

    // SDK module mapping
    private static readonly Dictionary<string, string> SdkModuleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VideoCaptureCoreX"] = "Video Capture SDK X",
        ["VideoEditCoreX"] = "Video Edit SDK X",
        ["MediaPlayerCoreX"] = "Media Player SDK X",
        ["MediaInfoReaderX"] = "Media Info Reader SDK X",
        ["SimplePlayerCoreX"] = "Simple Player SDK X",
        ["ColorspaceConverterX"] = "Fast Image Processing SDK X",
        ["VideoCaptureCore"] = "Video Capture SDK (Legacy)",
        ["VideoEditCore"] = "Video Edit SDK (Legacy)",
        ["MediaPlayerCore"] = "Media Player SDK (Legacy)",
    };

    public SourceAnalyzer(string sourceRoot)
    {
        _sourceRoot = sourceRoot;
    }

    public void Analyze()
    {
        Console.WriteLine("Analyzing source files...");
        AnalyzeMediaBlockDirectories();
        AnalyzeSourceFiles();
        Console.WriteLine($"Found {_mediaBlockCategories.Count} media block categorizations.");
        Console.WriteLine($"Found {_inheritance.Count} class inheritance records.");
        Console.WriteLine($"Found {_pads.Count} pad definitions.");
    }

    private void AnalyzeMediaBlockDirectories()
    {
        var mediaBlocksDir = Path.Combine(_sourceRoot, "MediaBlocks");
        if (!Directory.Exists(mediaBlocksDir)) return;

        foreach (var dir in Directory.GetDirectories(mediaBlocksDir))
        {
            var categoryName = Path.GetFileName(dir);
            var csFiles = Directory.GetFiles(dir, "*.cs", SearchOption.TopDirectoryOnly);

            foreach (var file in csFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                // Most block files end with "Block"
                if (fileName.EndsWith("Block") || fileName.Contains("Block"))
                {
                    _mediaBlockCategories[fileName] = categoryName;
                }
            }
        }
    }

    private void AnalyzeSourceFiles()
    {
        var csFiles = Directory.GetFiles(_sourceRoot, "*.cs", SearchOption.AllDirectories);
        Console.WriteLine($"Scanning {csFiles.Length} C# source files...");

        foreach (var file in csFiles)
        {
            try
            {
                AnalyzeFile(file);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error analyzing {file}: {ex.Message}");
            }
        }
    }

    private void AnalyzeFile(string filePath)
    {
        var content = File.ReadAllText(filePath);

        // Extract namespace - handle both traditional and file-scoped namespaces
        var nsMatches = Regex.Matches(content, @"namespace\s+([\w.]+)");
        string ns;

        if (nsMatches.Count == 0)
        {
            ns = ""; // No namespace (rare, but possible in old or generated code)
        }
        else if (nsMatches.Count == 1)
        {
            ns = nsMatches[0].Groups[1].Value;
        }
        else
        {
            // Multiple namespaces in one file - use the first one and log a warning
            ns = nsMatches[0].Groups[1].Value;
            // Note: This is a limitation - ideally we'd track which classes belong to which namespace
            // but that requires more complex parsing. In practice, most modern C# uses file-scoped namespaces.
        }

        // Find class/struct/interface declarations with optional inheritance
        var classPattern = @"public\s+(?:abstract\s+|sealed\s+|static\s+|partial\s+)*(?:class|struct|interface)\s+(\w+)(?:<[^>]+>)?\s*(?::\s*([^\{]+?))?\s*\{";
        var classMatches = Regex.Matches(content, classPattern);

        foreach (Match match in classMatches)
        {
            var className = match.Groups[1].Value;
            var fullName = string.IsNullOrEmpty(ns) ? className : $"{ns}.{className}";

            if (match.Groups[2].Success)
            {
                var inheritanceStr = match.Groups[2].Value.Trim();

                // Strip 'where' clauses from inheritance string (e.g., "IBar where T : class" → "IBar")
                var whereIndex = inheritanceStr.IndexOf(" where ", StringComparison.Ordinal);
                if (whereIndex >= 0)
                    inheritanceStr = inheritanceStr[..whereIndex].Trim();

                var parts = SplitInheritanceList(inheritanceStr);

                string? baseClass = null;
                var interfaces = new List<string>();

                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    // Convention: interfaces start with I and have a second uppercase letter
                    if (trimmed.StartsWith("I") && trimmed.Length > 1 && char.IsUpper(trimmed[1]))
                    {
                        interfaces.Add(trimmed);
                    }
                    else if (baseClass == null)
                    {
                        baseClass = trimmed;
                    }
                    else
                    {
                        interfaces.Add(trimmed);
                    }
                }

                _inheritance[fullName] = (baseClass, interfaces);
            }

            // Extract class body and look for pad definitions within it
            var classBody = ExtractClassBody(content, match.Index + match.Length);
            if (classBody != null)
            {
                AnalyzePadsInClass(classBody, className, fullName);
            }
        }
    }

    /// <summary>
    /// Extracts the body of a class starting from the position right after the opening '{'.
    /// Uses brace counting to find the matching closing '}', skipping strings and comments.
    /// </summary>
    private static string? ExtractClassBody(string content, int bodyStart)
    {
        int braceCount = 1;
        int i = bodyStart;

        while (i < content.Length)
        {
            char c = content[i];

            // Skip single-line comments
            if (c == '/' && i + 1 < content.Length && content[i + 1] == '/')
            {
                i += 2;
                while (i < content.Length && content[i] != '\n') i++;
                i++;
                continue;
            }

            // Skip multi-line comments
            if (c == '/' && i + 1 < content.Length && content[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < content.Length && !(content[i] == '*' && content[i + 1] == '/')) i++;
                i += 2;
                continue;
            }

            // Skip string literals
            if (c == '"' || c == '\'')
            {
                char quote = c;
                i++;
                while (i < content.Length)
                {
                    if (content[i] == '\\' && i + 1 < content.Length)
                    {
                        i += 2; // Skip escaped character
                    }
                    else if (content[i] == quote)
                    {
                        i++;
                        break;
                    }
                    else
                    {
                        i++;
                    }
                }
                continue;
            }

            // Skip verbatim string literals (@"...")
            if (c == '@' && i + 1 < content.Length && content[i + 1] == '"')
            {
                i += 2;
                while (i < content.Length)
                {
                    if (content[i] == '"')
                    {
                        if (i + 1 < content.Length && content[i + 1] == '"')
                        {
                            i += 2; // Skip doubled quote (escape sequence in verbatim strings)
                        }
                        else
                        {
                            i++;
                            break;
                        }
                    }
                    else
                    {
                        i++;
                    }
                }
                continue;
            }

            // Count braces
            if (c == '{') braceCount++;
            else if (c == '}') braceCount--;

            if (braceCount == 0)
            {
                return content[bodyStart..i];
            }

            i++;
        }

        return null;
    }

    private void AnalyzePadsInClass(string classBody, string className, string fullName)
    {
        // Match patterns like: new MediaBlockPad(this, MediaBlockPadDirection.Out, MediaBlockPadMediaType.Video)
        var padPattern = @"new\s+MediaBlockPad\s*\(\s*this\s*,\s*MediaBlockPadDirection\.(\w+)\s*,\s*MediaBlockPadMediaType\.(\w+)\s*\)";
        var padMatches = Regex.Matches(classBody, padPattern);

        if (padMatches.Count == 0) return;

        var pads = new List<PadInfo>();
        int videoInCount = 0, audioInCount = 0, videoOutCount = 0, audioOutCount = 0;

        foreach (Match pm in padMatches)
        {
            var direction = pm.Groups[1].Value;
            var mediaType = pm.Groups[2].Value;

            // Try to find the field name this pad is assigned to
            var padName = direction switch
            {
                "Out" when mediaType == "Video" => $"VideoOutput{(++videoOutCount > 1 ? videoOutCount.ToString() : "")}",
                "Out" when mediaType == "Audio" => $"AudioOutput{(++audioOutCount > 1 ? audioOutCount.ToString() : "")}",
                "In" when mediaType == "Video" => $"VideoInput{(++videoInCount > 1 ? videoInCount.ToString() : "")}",
                "In" when mediaType == "Audio" => $"AudioInput{(++audioInCount > 1 ? audioInCount.ToString() : "")}",
                _ => $"{mediaType}{direction}"
            };

            pads.Add(new PadInfo
            {
                Name = padName,
                Direction = direction,
                MediaType = mediaType
            });
        }

        if (pads.Count > 0)
        {
            _pads[fullName] = pads;
        }
    }

    private static List<string> SplitInheritanceList(string str)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(str[start..i].Trim());
                start = i + 1;
            }
        }

        if (start < str.Length)
            result.Add(str[start..].Trim());

        return result;
    }

    public (string? BaseClass, List<string> Interfaces) GetInheritance(string fullName)
    {
        if (_inheritance.TryGetValue(fullName, out var info))
            return info;
        return (null, new List<string>());
    }

    public string? GetMediaBlockCategory(string className)
    {
        if (_mediaBlockCategories.TryGetValue(className, out var cat))
            return cat;

        // Try with "Block" suffix
        if (_mediaBlockCategories.TryGetValue(className + "Block", out cat))
            return cat;

        return null;
    }

    public List<PadInfo>? GetPads(string fullName)
    {
        _pads.TryGetValue(fullName, out var pads);
        return pads;
    }

    public bool IsCoreSDK(string className) => CoreSdkClassNames.Contains(className);

    public string? GetSdkModule(string className)
    {
        SdkModuleMap.TryGetValue(className, out var module);
        return module;
    }

    public Dictionary<string, string> GetMediaBlockCategories() => new(_mediaBlockCategories);

    /// <summary>
    /// Get category descriptions for well-known media block categories.
    /// </summary>
    public static Dictionary<string, string> GetCategoryDescriptions() => new()
    {
        ["Sources"] = "Input source blocks that provide media data from files, cameras, network streams, and virtual sources",
        ["Sinks"] = "Output sink blocks that write media data to files, network streams, or hardware outputs",
        ["VideoEncoders"] = "Video encoder blocks that compress raw video into formats like H.264, HEVC, VP9, AV1",
        ["AudioEncoders"] = "Audio encoder blocks that compress raw audio into formats like AAC, MP3, Opus, FLAC",
        ["VideoDecoders"] = "Video decoder blocks that decompress encoded video streams",
        ["VideoProcessing"] = "Video processing blocks for effects, resizing, cropping, color conversion, and overlays",
        ["VideoRendering"] = "Video rendering blocks for display output on screen",
        ["AudioProcessing"] = "Audio processing blocks for effects, mixing, resampling, and equalization",
        ["AudioRendering"] = "Audio rendering blocks for audio playback through speakers/headphones",
        ["AudioVisualizers"] = "Audio visualization blocks that generate visual representations of audio",
        ["Demuxers"] = "Demuxer blocks that separate multiplexed streams into individual tracks",
        ["Parsers"] = "Parser blocks that analyze and prepare encoded streams for decoding",
        ["OpenGL"] = "GPU-accelerated rendering blocks using OpenGL",
        ["OpenCV"] = "Computer vision processing blocks using OpenCV",
        ["Nvidia"] = "NVIDIA GPU-accelerated processing blocks (NVENC, NVDEC, Maxine)",
        ["Decklink"] = "Blackmagic DeckLink professional capture/output blocks",
        ["AWS"] = "Amazon Web Services integration blocks",
        ["RTSPServer"] = "RTSP server blocks for streaming media over RTSP protocol",
        ["Bridge"] = "Stream bridging blocks for connecting separate pipelines",
        ["Special"] = "Specialized processing blocks for unique use cases",
        ["Outputs"] = "Composite output blocks that combine encoding and muxing",
        ["Helpers"] = "Helper and utility blocks",
    };
}
