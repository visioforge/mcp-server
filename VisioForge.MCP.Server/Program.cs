using VisioForge.MCP.Server.Index;
using VisioForge.MCP.Server.Resources;

// The implementation version reported in the initialize handshake. The registry listing
// version is separate: it lives in server.json and changes only when the listing does.
const string ServerVersion = "1.2.0";

var builder = WebApplication.CreateBuilder(args);

// Load API index as a singleton
var indexService = new ApiIndexService();
var indexPath = builder.Configuration["ApiIndex:Path"]
    ?? Path.Combine(AppContext.BaseDirectory, "Data", "api-index.json");

if (File.Exists(indexPath))
{
    indexService.Load(indexPath);
}
else
{
    Console.Error.WriteLine($"WARNING: API index not found at {indexPath}. Run the IndexBuilder first.");
    Console.Error.WriteLine("The server will start but tools will return empty results.");
}

builder.Services.AddSingleton(indexService);

// Configure MCP server
builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "VisioForge SDK Assistant",
        Version = ServerVersion,
    };

    // Agents read this once, at initialize, and it is the only lever we have over
    // which tool they reach for first. tools/list order is NOT a lever: the SDK
    // keeps tools in a ConcurrentDictionary keyed on the tool name and enumerates
    // it unsorted, and .NET randomizes string hashing per process, so the order
    // changes on every restart. It landed on list_media_blocks first, which sent
    // every "record a webcam" evaluation into the pipeline layer instead of into
    // one of the three products.
    options.ServerInstructions =
        """
        VisioForge ships three .NET products, and an evaluation should start in one of them:

          - Video Capture SDK .Net (VisioForge.DotNet.VideoCapture) — webcam, USB and IP
            cameras, screen recording, capture cards, streaming out.
          - Media Player SDK .Net (VisioForge.DotNet.MediaPlayer) — file, RTSP and HLS playback.
          - Video Edit SDK .Net (VisioForge.DotNet.VideoEdit) — timeline editing, transitions,
            overlays, conversion.

        Media Blocks SDK (VisioForge.DotNet.MediaBlocks) is the composition layer underneath
        them: a block graph you wire by hand. Choose it only when the user genuinely needs a
        custom source -> process -> encode -> output pipeline. For "capture a webcam",
        "play a video", "trim a clip" — including on MAUI, Avalonia, WPF, WinForms and Uno —
        answer with the product SDK, not with Media Blocks.

        Where to start:
          - search_api or list_sdk_classes to find the type for a task.
          - get_code_example for a runnable snippet.
          - get_nuget_packages_snippet for the .csproj — sdkType is required, so name the SKU.
          - get_deployment_guide for shipping to a platform.

        list_media_blocks and get_media_block_info describe the block catalog and are only
        relevant once Media Blocks is the chosen SDK.
        """;
})
.WithHttpTransport(o =>
{
    // Pin behaviour: live server returns Mcp-Session-Id, so stateful must
    // stay default-true across SDK minor releases. IdleTimeout matches the
    // typical Claude Desktop session length; bump if very long sessions
    // start dropping mid-flow.
    o.Stateless = false;
    o.IdleTimeout = TimeSpan.FromMinutes(30);
    o.MaxIdleSessionCount = 1_000;
})
.WithToolsFromAssembly()
.WithPromptsFromAssembly()
.WithListResourcesHandler((ctx, ct) =>
{
    return ValueTask.FromResult(new ModelContextProtocol.Protocol.ListResourcesResult
    {
        Resources =
        [
            new() { Name = "Getting Started Guide", Uri = "visioforge://guides/getting-started", Description = "Introduction to VisioForge .NET SDK with quick start examples", MimeType = "text/plain" },
            new() { Name = "Architecture Overview", Uri = "visioforge://guides/architecture", Description = "SDK layered architecture, media blocks pipeline, and event model", MimeType = "text/plain" },
            new() { Name = "Platform Compatibility", Uri = "visioforge://guides/platform-matrix", Description = "Platform support matrix, .NET frameworks, and GStreamer dependencies", MimeType = "text/plain" },
            new() { Name = "Migration Guide", Uri = "visioforge://guides/migration", Description = "Migration guide from legacy API to modern X-suffix API", MimeType = "text/plain" },
        ]
    });
})
.WithReadResourceHandler((ctx, ct) =>
{
    var uri = ctx.Params?.Uri;
    var text = uri switch
    {
        "visioforge://guides/getting-started" => StaticResources.GettingStarted,
        "visioforge://guides/architecture" => StaticResources.Architecture,
        "visioforge://guides/platform-matrix" => StaticResources.PlatformMatrix,
        "visioforge://guides/migration" => StaticResources.MigrationGuide,
        _ => throw new NotSupportedException($"Unknown resource: {uri}")
    };

    return ValueTask.FromResult(new ModelContextProtocol.Protocol.ReadResourceResult
    {
        Contents = [new ModelContextProtocol.Protocol.TextResourceContents { Text = text, MimeType = "text/plain", Uri = uri! }]
    });
});

var app = builder.Build();

// DNS-rebinding protection (MCP spec 2025-11-25, required for Streamable HTTP).
// Empty Origin header is allowed because non-browser clients (Claude Desktop,
// claude-code, MCP Inspector, curl) typically don't send Origin at all. Browser
// clients always send it — the spec relies on this asymmetry.
var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "https://mcp.visioforge.com",
};
foreach (var o in (Environment.GetEnvironmentVariable("MCP_ALLOWED_ORIGINS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
{
    allowedOrigins.Add(o);
}

static bool IsLoopbackOrigin(string origin) =>
    Uri.TryCreate(origin, UriKind.Absolute, out var u)
    && (u.Host is "localhost" or "127.0.0.1" or "[::1]");

app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/mcp"))
    {
        var origin = ctx.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin)
            && !allowedOrigins.Contains(origin)
            && !IsLoopbackOrigin(origin))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsync("Forbidden: Origin not allowed.");
            return;
        }
    }
    await next();
});

// Map MCP endpoint
app.MapMcp("/mcp");

// How old the API index may get before /health starts calling it stale. The index
// carries the SDK version, the NuGet pins and the deployment guides, so a frozen one
// is not a cosmetic problem — it is the server answering with last quarter's API.
// It went unnoticed for six months once, because nothing reported the age.
const int IndexStaleAfterDays = 45;

// Health check endpoint
app.MapGet("/health", (ApiIndexService idx) =>
{
    int? ageDays = DateTimeOffset.TryParse(idx.GeneratedAt, out var generated)
        ? (int)(DateTimeOffset.UtcNow - generated).TotalDays
        : null;

    return Results.Ok(new
    {
        status = "healthy",
        sdkVersion = idx.SdkVersion,
        indexGeneratedAt = idx.GeneratedAt,
        indexAgeDays = ageDays,
        // Always a bool, and true when GeneratedAt does not parse. `ageDays > N` on a
        // null int? is false, which would report a corrupt or missing timestamp as fresh
        // — hiding the broken-index case a monitor watches this field for. Reporting it
        // as stale is the useful answer: indexAgeDays is null alongside, so "stale" and
        // "unreadable" are still distinguishable.
        indexStale = ageDays is null || ageDays > IndexStaleAfterDays,
        deploymentGuideCount = idx.Data.DeploymentGuides.Count,
        classCount = idx.Data.Classes.Count,
        enumCount = idx.Data.Enums.Count,
        mediaBlockCategories = idx.Data.MediaBlockCategories.Count,
    });
});

// Domain from config or env, defaults to mcp.visioforge.com
var publicDomain = builder.Configuration["PublicDomain"]
    ?? Environment.GetEnvironmentVariable("MCP_PUBLIC_DOMAIN")
    ?? "mcp.visioforge.com";

// Root endpoint with info
app.MapGet("/", () => Results.Ok(new
{
    name = "VisioForge SDK MCP Server",
    description = "Model Context Protocol server for VisioForge .NET Media SDK",
    mcpEndpoint = "/mcp",
    healthEndpoint = "/health",
    documentation = "https://www.visioforge.com/docs",
    connect = new
    {
        claudeCode = $"claude mcp add --transport http visioforge-sdk https://{publicDomain}/mcp",
        vsCode = $"Add to .vscode/mcp.json: {{ \"servers\": {{ \"visioforge-sdk\": {{ \"type\": \"http\", \"url\": \"https://{publicDomain}/mcp\" }} }} }}",
    },
}));

// Discovery document. This 404'd until now: nginx proxies `location /` straight
// to Kestrel, and Kestrel had no route for it, so agents probing the well-known
// path got an ASP.NET 404 rather than a manifest. It serves server.json — the
// document published to the official MCP Registry — with every remote URL moved to this
// deployment's domain, so the listing has one source. Parsed once at startup; a missing
// or malformed file is a startup WARNING and a 404 on this route alone, never on /mcp.
// Tools are deliberately absent: clients enumerate them over JSON-RPC tools/list.
// visioforge.com/.well-known/mcp.json (the website repo) still serves an older
// home-rolled 0.1-draft manifest; this one is the registry's shape.
var manifestPath = Path.Combine(AppContext.BaseDirectory, "server.json");
string? manifestJson = null;
try
{
    var manifest = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
    foreach (var remote in manifest["remotes"]!.AsArray())
    {
        // Only the authority (host and port) becomes this deployment's; the path is kept
        // verbatim, so a {variable} in it survives. A non-https or non-string url is
        // served as published.
        if (remote?["url"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var url)
            && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var slash = url.IndexOf('/', "https://".Length);
            remote["url"] = $"https://{publicDomain}{(slash < 0 ? "" : url[slash..])}";
        }
    }
    manifestJson = manifest.ToJsonString();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"WARNING: {manifestPath} could not be served ({ex.Message}); /.well-known/mcp.json will 404.");
}
app.MapGet("/.well-known/mcp.json", () => manifestJson is null
    ? Results.NotFound()
    : Results.Text(manifestJson, "application/json"));

Console.WriteLine("=== VisioForge SDK MCP Server ===");
Console.WriteLine($"SDK Version: {indexService.SdkVersion}");
Console.WriteLine($"Classes: {indexService.Data.Classes.Count}");
Console.WriteLine($"Enums: {indexService.Data.Enums.Count}");
Console.WriteLine($"Deployment guides: {indexService.Data.DeploymentGuides.Count}");
Console.WriteLine($"Index generated: {indexService.GeneratedAt}");
Console.WriteLine($"MCP endpoint: /mcp");

if (DateTimeOffset.TryParse(indexService.GeneratedAt, out var indexGenerated)
    && (DateTimeOffset.UtcNow - indexGenerated).TotalDays > IndexStaleAfterDays)
{
    Console.Error.WriteLine(
        $"WARNING: the API index is {(int)(DateTimeOffset.UtcNow - indexGenerated).TotalDays} days old. "
        + "Package pins, class summaries and deployment guides are all served from it. "
        + "Rebuild it with build-mcp.sh on a machine that has VisioForge.Core.xml.");
}

if (indexService.Data.DeploymentGuides.Count == 0)
{
    Console.Error.WriteLine(
        "WARNING: the API index contains no deployment guides, so list_deployment_guides "
        + "and get_deployment_guide will answer 'not found' for everything. The index was "
        + "most likely built without its deployment-guides path argument.");
}

app.Run();

// Exposed for WebApplicationFactory<Program> in VisioForge.MCP.Tests.
// Top-level statements compile to an internal Program class; WebApplicationFactory
// needs a public type token to bootstrap the test host.
public partial class Program { }
