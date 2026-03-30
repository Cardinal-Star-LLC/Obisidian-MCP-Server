/// <summary>
/// Central configuration for the MCP server and its HTTP client.
/// All tunable values and server identity strings live here.
/// </summary>
static class ServerConfig
{
    // ── Network ───────────────────────────────────────────────────────────────
    public const string BaseUrl = "https://127.0.0.1:27124";

    // Read operations (GET) — short timeout; failure is fast and obvious.
    public static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    // Write operations (PUT, PATCH) — larger bodies need more transfer time.
    public static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(60);

    // Wall-clock budget for an entire MCP request (wraps all HTTP calls inside).
    // Set just under Claude Desktop's 4-minute MCP client timeout so we can
    // return a clean error rather than letting the client give up silently.
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(210);

    // ── Environment ───────────────────────────────────────────────────────────
    public const string ApiKeyEnvVar = "OBSIDIAN_API_KEY";
    public const string AuthScheme = "Bearer";

    // ── Logging ───────────────────────────────────────────────────────────────
    public const string LogFileName = "obsidian-mcp.log";

    // ── MCP server identity ───────────────────────────────────────────────────
    public const string ProtocolVersion = "2024-11-05";
    public const string ServerName = "obsidian-local-rest-api";
    public const string ServerVersion = "1.0.0";
}
