using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace VisioForge.MCP.Server.Prompts;

[McpServerPromptType]
public static class InteractivePrompts
{
    // Sanitize user input to prevent prompt injection
    private static string SanitizeInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "[not provided]";

        // Remove potential prompt injection patterns
        return input
            .Replace("```", "'''")           // Prevent breaking out of code blocks
            .Replace("---", "- - -")         // Prevent delimiter confusion
            .Replace("\"\"\"", "'''")        // Prevent breaking out of triple-quote blocks
            .Trim();
    }

    [McpServerPrompt, Description(
        "Interactive prompt to help build a VisioForge media block pipeline. " +
        "Asks about your input source, processing needs, and output destination, " +
        "then generates a complete pipeline configuration.")]
    public static string BuildPipeline(
        [Description("What is your input source? (e.g., 'webcam', 'RTSP camera', 'video file', 'screen capture', 'NDI', 'virtual test source')")]
        string inputSource,
        [Description("What processing do you need? (e.g., 'none', 'resize to 720p', 'add text overlay', 'apply effects', 'mix multiple sources')")]
        string processing,
        [Description("What is your output destination? (e.g., 'MP4 file', 'RTMP stream', 'HLS stream', 'screen display', 'NDI output', 'WebM file')")]
        string outputDestination,
        [Description("Do you need audio? (e.g., 'yes with microphone', 'yes from file', 'no audio', 'yes from RTSP stream')")]
        string audioNeeds = "yes")
    {
        // Sanitize all user inputs to prevent prompt injection
        var safeInputSource = SanitizeInput(inputSource);
        var safeProcessing = SanitizeInput(processing);
        var safeOutputDestination = SanitizeInput(outputDestination);
        var safeAudioNeeds = SanitizeInput(audioNeeds);

        return $@"You are a VisioForge SDK expert. Build a complete media block pipeline based on these requirements:

--- USER INPUT START ---
Input Source: {safeInputSource}
Processing: {safeProcessing}
Output: {safeOutputDestination}
Audio: {safeAudioNeeds}
--- USER INPUT END ---

Please:
1. List all required media blocks with their settings classes
2. Show the pipeline connection graph (which pads connect to which)
3. Provide complete, working C# code
4. Note any platform-specific considerations
5. Suggest encoder settings appropriate for the use case

Use the VisioForge MCP tools (list_media_blocks, get_class_info, get_media_block_info) to look up exact class names, properties, and settings. Ensure the code is accurate and uses the correct API.";
    }

    [McpServerPrompt, Description(
        "Diagnostic prompt to help troubleshoot VisioForge SDK issues. " +
        "Describe your problem and get suggestions for fixes.")]
    public static string Troubleshoot(
        [Description("What error or symptom are you experiencing? Include error messages if available.")]
        string problem,
        [Description("What SDK class or feature are you using? (e.g., 'VideoCaptureCoreX', 'H264EncoderBlock', 'RTSP playback')")]
        string sdkFeature,
        [Description("What platform are you on? (e.g., 'Windows 11', 'Ubuntu 22.04', 'macOS Sonoma')")]
        string platform)
    {
        // Sanitize all user inputs to prevent prompt injection
        var safeProblem = SanitizeInput(problem);
        var safeSdkFeature = SanitizeInput(sdkFeature);
        var safePlatform = SanitizeInput(platform);

        return $@"You are a VisioForge SDK troubleshooting expert. Help diagnose and fix this issue:

--- USER INPUT START ---
Problem: {safeProblem}
SDK Feature: {safeSdkFeature}
Platform: {safePlatform}
--- USER INPUT END ---

Please:
1. Identify likely root causes
2. Suggest specific fixes with code changes
3. Check if the feature is supported on the platform (use the platform compatibility info)
4. Recommend relevant properties or settings to check
5. Suggest diagnostic steps (logging, checking device availability, etc.)

Use the VisioForge MCP tools to look up correct API usage, properties, and methods.";
    }

    [McpServerPrompt, Description(
        "Help choose the right video/audio encoder for your use case. " +
        "Considers quality, speed, file size, and platform compatibility.")]
    public static string ChooseEncoder(
        [Description("What is the primary use case? (e.g., 'live streaming', 'archival recording', 'web playback', 'editing intermediate', 'broadcast')")]
        string useCase,
        [Description("What quality/speed tradeoff? (e.g., 'best quality', 'balanced', 'fastest encoding', 'smallest file size')")]
        string qualityPreference,
        [Description("Target platform for playback (e.g., 'web browsers', 'mobile devices', 'professional broadcast', 'any')")]
        string targetPlatform,
        [Description("Is hardware encoding available? (e.g., 'NVIDIA GPU', 'Intel QuickSync', 'Apple Silicon', 'CPU only')")]
        string hardwareCapability = "CPU only")
    {
        // Sanitize all user inputs to prevent prompt injection
        var safeUseCase = SanitizeInput(useCase);
        var safeQualityPreference = SanitizeInput(qualityPreference);
        var safeTargetPlatform = SanitizeInput(targetPlatform);
        var safeHardwareCapability = SanitizeInput(hardwareCapability);

        return $@"You are a VisioForge SDK encoder expert. Recommend the best encoder configuration:

--- USER INPUT START ---
Use Case: {safeUseCase}
Quality Preference: {safeQualityPreference}
Target Platform: {safeTargetPlatform}
Hardware: {safeHardwareCapability}
--- USER INPUT END ---

Please:
1. Recommend specific video and audio encoder blocks
2. Suggest encoder settings (bitrate, profile, preset, etc.)
3. Recommend the appropriate container/sink block
4. Explain the tradeoffs of your recommendation
5. Provide a code snippet with the recommended encoder settings

Use the VisioForge MCP tools to look up available encoders and their settings classes.";
    }
}
