using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

/// <summary>
/// Concrete implementation of IObsidianClient backed by HttpClient.
/// All HTTP concerns are contained here; no protocol or tool logic leaks in.
///
/// Timeout strategy:
///   HttpClient.Timeout is set to the max of ReadTimeout / WriteTimeout so it
///   never fires before our own per-operation linked tokens do. Each method
///   creates a linked CancellationTokenSource that combines the caller's token
///   (the global RequestTimeout) with an operation-specific timeout, ensuring:
///     - reads cancel after ReadTimeout
///     - writes cancel after WriteTimeout
///     - everything cancels if the global RequestTimeout fires first
/// </summary>
class ObsidianClient : IObsidianClient
{
    // ── Endpoint paths ────────────────────────────────────────────────────────
    const string StatusEndpoint  = "/";
    const string VaultRoot       = "/vault/";
    const string ActiveEndpoint  = "/active/";
    const string SearchEndpoint  = "/search/simple/";
    const string OpenEndpoint    = "/open/";

    // ── Content types ─────────────────────────────────────────────────────────
    const string JsonContentType     = "application/json";
    const string MarkdownContentType = "text/markdown";

    // ── API response keys ─────────────────────────────────────────────────────
    const string FilesKey = "files";

    // ── Shared HTTP primitives ────────────────────────────────────────────────
    static readonly MediaTypeWithQualityHeaderValue _markdownType = new(MarkdownContentType);

    readonly HttpClient             _http;
    readonly ILogger                _logger;

    internal ObsidianClient(HttpClient http, ILogger logger)
    {
        _http   = http;
        _logger = logger;
    }

    // ── IObsidianClient ───────────────────────────────────────────────────────

    public async Task<string> GetStatus(CancellationToken ct = default)
    {
        using var lcts = ReadLinked(ct);
        return await _http.GetStringAsync(StatusEndpoint, lcts.Token);
    }

    public async Task<string> ListFiles(JsonObject args, CancellationToken ct = default)
    {
        var path      = args[ArgKeys.Path]?.GetValue<string>() ?? "";
        var recursive = args[ArgKeys.Recursive]?.GetValue<bool>() ?? false;

        if (!recursive)
        {
            using var lcts = ReadLinked(ct);
            return await _http.GetStringAsync(VaultDirUrl(path), lcts.Token);
        }

        var allFiles = new List<string>();
        await WalkDirectory("", allFiles, ct);
        var arr = new JsonArray();
        foreach (var f in allFiles) arr.Add(JsonValue.Create(f));
        return new JsonObject { [FilesKey] = arr }.ToJsonString();
    }

    public Task<string> ReadNote(JsonObject args, CancellationToken ct = default)
        => GetMarkdownAsync(VaultUrl(RequiredString(args, ArgKeys.Path)), ct);

    public Task<string> GetActiveFile(CancellationToken ct = default)
        => GetMarkdownAsync(ActiveEndpoint, ct);

    public async Task<string> WriteNote(JsonObject args, CancellationToken ct = default)
    {
        using var lcts = WriteLinked(ct);
        return StatusLine(await _http.PutAsync(
            VaultUrl(RequiredString(args, ArgKeys.Path)),
            MarkdownContent(RequiredString(args, ArgKeys.Content)),
            lcts.Token));
    }

    public async Task<string> AppendNote(JsonObject args, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, VaultUrl(RequiredString(args, ArgKeys.Path)))
        {
            Content = MarkdownContent(RequiredString(args, ArgKeys.Content))
        };
        using var lcts = WriteLinked(ct);
        return StatusLine(await _http.SendAsync(req, lcts.Token));
    }

    public async Task<string> DeleteNote(JsonObject args, CancellationToken ct = default)
    {
        using var lcts = ReadLinked(ct);
        return StatusLine(await _http.DeleteAsync(VaultUrl(RequiredString(args, ArgKeys.Path)), lcts.Token));
    }

    public async Task<string> SearchNotes(JsonObject args, CancellationToken ct = default)
    {
        var query  = RequiredString(args, ArgKeys.Query);
        var ctxLen = args[ArgKeys.ContextLength]?.GetValue<int>() ?? 100;
        using var lcts = ReadLinked(ct);
        var res = await _http.PostAsync(
            $"{SearchEndpoint}?query={Uri.EscapeDataString(query)}&contextLength={ctxLen}",
            null, lcts.Token);
        return await res.Content.ReadAsStringAsync(lcts.Token);
    }

    public async Task<string> OpenFile(JsonObject args, CancellationToken ct = default)
    {
        var newLeaf = args[ArgKeys.NewLeaf]?.GetValue<bool>() ?? false;
        var body    = new StringContent(
            new JsonObject { [ArgKeys.NewLeaf] = newLeaf }.ToJsonString(),
            Encoding.UTF8, JsonContentType);
        using var lcts = ReadLinked(ct);
        return StatusLine(await _http.PostAsync(
            $"{OpenEndpoint}{Uri.EscapeDataString(RequiredString(args, ArgKeys.Path))}",
            body, lcts.Token));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    async Task<string> GetMarkdownAsync(string url, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Add(_markdownType);
        using var lcts = ReadLinked(ct);
        var res = await _http.SendAsync(req, lcts.Token);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync(lcts.Token);
    }

    // dirPath is always slash-free — trailing '/' is stripped on entry so
    // we never need TrimEnd at URL-building time (see loop below).
    async Task WalkDirectory(string dirPath, List<string> results, CancellationToken ct)
    {
        var url = VaultDirUrl(dirPath);
        _logger.Log(LogLevel.Information, $"WalkDirectory: GET {url}");

        HttpResponseMessage res;
        try
        {
            using var lcts = ReadLinked(ct);
            res = await _http.GetAsync(url, lcts.Token);
        }
        catch (Exception ex) { _logger.Log(LogLevel.Information, $"WalkDirectory error at '{url}': {ex.Message}"); return; }

        JsonObject? obj;
        try   { obj = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct))?.AsObject(); }
        catch { _logger.Log(LogLevel.Information, $"WalkDirectory: failed to parse response for '{url}'"); return; }

        var files = obj?[FilesKey]?.AsArray();
        if (files is null) return;

        foreach (var entry in files)
        {
            ct.ThrowIfCancellationRequested();
            var name = entry?.GetValue<string>();
            if (string.IsNullOrEmpty(name)) continue;

            var trimmed  = name.TrimEnd('/');
            var isDir    = trimmed.Length < name.Length;
            var fullPath = string.IsNullOrEmpty(dirPath) ? trimmed : $"{dirPath}/{trimmed}";

            if (isDir)
                await WalkDirectory(fullPath, results, ct);
            else
                results.Add(fullPath);
        }
    }

    // ── Linked token helpers ──────────────────────────────────────────────────
    // Each returns a CancellationTokenSource linked to the caller's token so
    // either the operation-specific timeout or the global RequestTimeout fires,
    // whichever comes first.

    static CancellationTokenSource ReadLinked(CancellationToken ct)
    {
        var lcts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lcts.CancelAfter(ServerConfig.ReadTimeout);
        return lcts;
    }

    static CancellationTokenSource WriteLinked(CancellationToken ct)
    {
        var lcts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lcts.CancelAfter(ServerConfig.WriteTimeout);
        return lcts;
    }

    static string VaultUrl(string path)
        => VaultRoot + string.Join("/", path.Split('/').Select(Uri.EscapeDataString));

    // Trailing slash signals to the Obsidian API that we want a directory listing.
    static string VaultDirUrl(string path)
        => string.IsNullOrEmpty(path)
            ? VaultRoot
            : VaultRoot + string.Join("/", path.TrimEnd('/').Split('/').Select(Uri.EscapeDataString)) + "/";
    static string StatusLine(HttpResponseMessage r) => $"HTTP {(int)r.StatusCode} {r.ReasonPhrase}";
    static StringContent MarkdownContent(string s)  => new(s, Encoding.UTF8, MarkdownContentType);

    static string RequiredString(JsonObject args, string key)
    {
        var val = args[key]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(val))
            throw new ArgumentException($"Required argument '{key}' is missing or empty.");
        return val;
    }
}
