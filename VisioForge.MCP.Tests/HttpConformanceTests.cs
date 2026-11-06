using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace VisioForge.MCP.Tests;

/// <summary>
/// In-process tests against the live ASP.NET Core pipeline that verify MCP
/// Streamable HTTP transport conformance per spec 2025-11-25.
///
/// Covers things the existing ToolTests can't: HTTP status codes, headers,
/// the Origin allowlist middleware, and the auto-generated tool name list
/// that real clients see via tools/list.
/// </summary>
public class HttpConformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HttpConformanceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient NewClient() => _factory.CreateClient();

    private static StringContent InitBody() => new(
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"conformance-test","version":"0"}}}""",
        Encoding.UTF8,
        "application/json");

    private static void AddMcpAccept(HttpRequestMessage req)
    {
        // Streamable HTTP clients negotiate JSON or SSE; sending both is the
        // common Claude Desktop / Inspector pattern.
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    }

    [Fact]
    public async Task BadOrigin_Returns403()
    {
        var client = NewClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = InitBody() };
        req.Headers.Add("Origin", "http://evil.attacker.example");
        AddMcpAccept(req);

        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task GoodOrigin_AndEmptyOrigin_BothPass()
    {
        // Allowed list contains https://mcp.visioforge.com; empty Origin
        // header is allowed because non-browser MCP clients typically omit it.
        foreach (var origin in new string?[] { "https://mcp.visioforge.com", null })
        {
            var client = NewClient();
            var req = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = InitBody() };
            if (origin != null)
            {
                req.Headers.Add("Origin", origin);
            }
            AddMcpAccept(req);

            var resp = await client.SendAsync(req);

            Assert.True(
                resp.IsSuccessStatusCode,
                $"Origin '{origin ?? "<empty>"}' got {(int)resp.StatusCode} {resp.StatusCode}");
            Assert.True(
                resp.Headers.Contains("Mcp-Session-Id"),
                "initialize must return Mcp-Session-Id (stateful transport mode)");
        }
    }

    [Fact]
    public async Task LocalhostOrigin_IsAllowed()
    {
        // Local dev / local agents commonly send Origin: http://localhost:port.
        var client = NewClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = InitBody() };
        req.Headers.Add("Origin", "http://localhost:5173");
        AddMcpAccept(req);

        var resp = await client.SendAsync(req);

        Assert.True(resp.IsSuccessStatusCode, $"localhost Origin got {(int)resp.StatusCode}");
    }

    [Fact]
    public async Task Initialize_ReturnsProtocolVersion_2025_11_25()
    {
        var client = NewClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = InitBody() };
        AddMcpAccept(req);

        var resp = await client.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        Assert.True(resp.IsSuccessStatusCode, $"initialize got {(int)resp.StatusCode}: {body}");
        Assert.Contains("\"protocolVersion\":\"2025-11-25\"", body);
        Assert.Contains("\"name\":\"VisioForge SDK Assistant\"", body);
        Assert.Contains("\"version\":\"1.2.0\"", body);
    }

    [Fact]
    public async Task ToolsList_ContainsAll14Tools_WithCorrectNames()
    {
        var client = NewClient();

        // 1) initialize, capture Mcp-Session-Id.
        var initReq = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = InitBody() };
        AddMcpAccept(initReq);
        var initResp = await client.SendAsync(initReq);
        Assert.True(
            initResp.IsSuccessStatusCode,
            $"initialize got {(int)initResp.StatusCode}");
        var sessionId = initResp.Headers.GetValues("Mcp-Session-Id").Single();

        // 2) notifications/initialized — required by spec before further requests.
        var notif = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
                Encoding.UTF8,
                "application/json"),
        };
        notif.Headers.Add("Mcp-Session-Id", sessionId);
        AddMcpAccept(notif);
        await client.SendAsync(notif);

        // 3) tools/list with the same session.
        var listReq = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""",
                Encoding.UTF8,
                "application/json"),
        };
        listReq.Headers.Add("Mcp-Session-Id", sessionId);
        AddMcpAccept(listReq);
        var listResp = await client.SendAsync(listReq);
        var body = await listResp.Content.ReadAsStringAsync();

        Assert.True(
            listResp.IsSuccessStatusCode,
            $"tools/list got {(int)listResp.StatusCode}: {body}");

        var expected = new[]
        {
            "list_media_blocks",
            "get_media_block_info",
            "list_sdk_classes",
            "get_class_info",
            "get_method_signature",
            "search_api",
            "get_enum_values",
            "get_code_example",
            "get_pipeline_template",
            "list_namespaces",
            "list_deployment_guides",
            "get_deployment_guide",
            "get_nuget_packages_snippet", // bug-fix coverage
            "get_platform_specific_config",
        };

        foreach (var name in expected)
        {
            Assert.Contains($"\"name\":\"{name}\"", body);
        }

        // Negative assertion: the broken auto-generated name must NOT appear.
        Assert.DoesNotContain("get_nu_get_packages_snippet", body);

        // Every tool is a lookup in the static index: agents and registry scanners read these hints.
        Assert.Equal(expected.Length, CountOf(body, "\"readOnlyHint\":true"));
        Assert.Equal(expected.Length, CountOf(body, "\"idempotentHint\":true"));
        Assert.Equal(expected.Length, CountOf(body, "\"openWorldHint\":false"));
    }

    private static int CountOf(string text, string needle) =>
        (text.Length - text.Replace(needle, "").Length) / needle.Length;

    [Fact]
    public async Task BadOrigin_OnNonMcpPath_IsNotBlocked()
    {
        // The Origin allowlist middleware in Program.cs is gated on
        // `Request.Path.StartsWithSegments("/mcp")`. If the gate is ever
        // removed, the middleware would start blocking /health and /
        // for any non-allowlisted Origin — breaking monitoring + browser
        // probes. This test pins the gate.
        var client = NewClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.Add("Origin", "http://evil.attacker.example");

        var resp = await client.SendAsync(req);

        Assert.True(
            resp.IsSuccessStatusCode,
            $"Origin allowlist must NOT apply to non-/mcp paths. /health got {(int)resp.StatusCode}");
    }

    [Fact]
    public async Task WellKnownMcpJson_IsServed_WithDollarSchemaKey()
    {
        // This endpoint used to 404: nginx proxies `location /` to Kestrel and Kestrel
        // had no route for it, so an agent probing the well-known path got an ASP.NET
        // 404 rather than a manifest.
        // The domain is pinned so the remote URL rewrite is checked, not the machine's
        // MCP_PUBLIC_DOMAIN.
        var client = _factory.WithWebHostBuilder(b => b.UseSetting("PublicDomain", "mcp.test.example")).CreateClient();
        var resp = await client.GetAsync("/.well-known/mcp.json");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();

        // Served == server.json, the registry listing, with only the remote URL
        // moved to this deployment's domain.
        var expected = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "server.json")))!;
        expected["remotes"]![0]!["url"] = "https://mcp.test.example/mcp";
        Assert.Equal(expected.ToJsonString(), System.Text.Json.Nodes.JsonNode.Parse(body)!.ToJsonString());

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal("https://mcp.test.example/mcp", root.GetProperty("remotes")[0].GetProperty("url").GetString());

        // "$schema", not "schema": a validating client must see the meta-key under
        // its real name.
        Assert.True(root.TryGetProperty("$schema", out _), "manifest must carry the $schema meta-key");
        Assert.False(root.TryGetProperty("schema", out _), "manifest must not carry a bare 'schema' key");

        Assert.Equal("com.visioforge/sdk-docs", root.GetProperty("name").GetString());

        // remotes[].type is "streamable-http" per the MCP server.json schema — "http"
        // is not a transport name the spec knows.
        var remote = root.GetProperty("remotes")[0];
        Assert.Equal("streamable-http", remote.GetProperty("type").GetString());
        Assert.EndsWith("/mcp", remote.GetProperty("url").GetString());

        // Tools are deliberately absent: clients enumerate them over tools/list.
        Assert.False(root.TryGetProperty("tools", out _), "the manifest must not list tools");

        // The served manifest is server.json, the document published to the official
        // MCP Registry, whose schema rejects a description over 100 characters.
        Assert.True(root.GetProperty("description").GetString()!.Length <= 100, "description exceeds the registry's 100-character limit");
    }

    [Fact]
    public async Task Health_ReportsIndexAgeAndGuideCount()
    {
        // The api-index.json shipped with the server sat frozen for six months, and
        // nothing surfaced it: the package pins, class summaries and deployment guides
        // are all served from that file. /health now carries the two numbers that make
        // a freeze visible, so a monitor can alert on them.
        var resp = await NewClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("indexAgeDays", out _), "/health must report indexAgeDays");
        Assert.True(root.TryGetProperty("indexStale", out var stale), "/health must report indexStale");
        // A boolean either way. Asserting `true` would pass only while the committed
        // index happens to be old, and would go red the moment it is rebuilt — which
        // is the outcome this whole branch is working towards.
        Assert.True(
            stale.ValueKind is JsonValueKind.True or JsonValueKind.False,
            $"indexStale must be a boolean, got {stale.ValueKind}");
        Assert.True(
            root.TryGetProperty("deploymentGuideCount", out _),
            "/health must report deploymentGuideCount — an index with zero guides is why "
            + "list_deployment_guides answered 'not found' for everything");
    }
}
