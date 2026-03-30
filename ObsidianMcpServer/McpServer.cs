using ObsidianMcpServer;
using System.Text.Json.Nodes;

/// <summary>
/// MCP protocol handler. Depends on ITool and ILogger abstractions only.
/// Adding a new tool requires zero changes here — register it in Program.cs.
/// </summary>
class McpServer
{
    // ── JSON-RPC wire format ──────────────────────────────────────────────────
    const string JsonRpcVersion = "2.0";
    const string KeyJsonRpc = "jsonrpc";
    const string KeyResult = "result";
    const string KeyError = "error";
    const string KeyCode = "code";
    const string KeyMessage = "message";

    // internal so Program can use them for transport-level error responses
    internal const string KeyId = "id";
    internal const string KeyMethod = "method";
    internal const int MethodNotFound = -32601;
    internal const int InternalError = -32603;

    // ── MCP request keys ──────────────────────────────────────────────────────
    const string KeyParams = "params";
    const string KeyName = "name";
    const string KeyArguments = "arguments";

    // ── MCP method names ──────────────────────────────────────────────────────
    const string MethodInitialize = "initialize";
    const string MethodNotificationsInitialized = "notifications/initialized";
    const string MethodInitialized = "initialized";
    const string MethodToolsList = "tools/list";
    const string MethodToolsCall = "tools/call";

    // ── MCP capability / tool-list keys ──────────────────────────────────────
    const string KeyProtocolVersion = "protocolVersion";
    const string KeyCapabilities = "capabilities";
    const string KeyTools = "tools";
    const string KeyListChanged = "listChanged";
    const string KeyServerInfo = "serverInfo";
    const string KeyVersion = "version";
    const string KeyDescription = "description";
    const string KeyInputSchema = "inputSchema";

    // ── Tool result keys ──────────────────────────────────────────────────────
    const string KeyContent = "content";
    const string KeyType = "type";
    const string KeyText = "text";
    const string TypeText = "text";

    readonly IMcpLogger _logger;
    readonly IReadOnlyList<ITool> _tools;
    readonly IReadOnlyDictionary<string, ITool> _toolMap;

    internal McpServer(IEnumerable<ITool> tools, IMcpLogger logger)
    {
        _tools = tools.ToList();
        _toolMap = _tools.ToDictionary(t => t.Name);
        _logger = logger;
    }

    // ── Request dispatcher ────────────────────────────────────────────────────

    internal async Task<JsonObject?> HandleRequest(JsonObject req, CancellationToken ct = default)
    {
        var id = req[KeyId];
        var method = req[KeyMethod]?.GetValue<string>() ?? "";
        bool isNotification = id is null;

        _logger.Log($"Handling: '{method}' (notification={isNotification})");

        try
        {
            return method switch
            {
                MethodInitialize => Respond(id, BuildInitializeResult()),
                MethodNotificationsInitialized or MethodInitialized => null,
                MethodToolsList => Respond(id, BuildToolList()),
                MethodToolsCall => Respond(id, await CallTool(req, ct)),
                _ when isNotification => null,
                _ => Error(id, MethodNotFound, $"Method not found: {method}")
            };
        }
        catch (Exception ex)
        {
            _logger.Log($"Error in HandleRequest ({method}): {ex}");
            return isNotification ? null : Error(id, InternalError, ex.Message);
        }
    }

    // ── Tool dispatcher ───────────────────────────────────────────────────────

    async Task<JsonObject> CallTool(JsonObject req, CancellationToken ct)
    {
        var p = req[KeyParams];
        var name = p?[KeyName]?.GetValue<string>() ?? "";
        var args = p?[KeyArguments]?.AsObject() ?? new JsonObject();

        _logger.Log($"CallTool: '{name}' args={args.ToJsonString()}");

        string result;
        try
        {
            if (!_toolMap.TryGetValue(name, out var tool))
                throw new InvalidOperationException($"Unknown tool: {name}");

            result = await tool.ExecuteAsync(args, ct);
        }
        catch (OperationCanceledException)
        {
            var msg = $"Tool '{name}' timed out (read={ServerConfig.ReadTimeout.TotalSeconds}s, " +
                      $"write={ServerConfig.WriteTimeout.TotalSeconds}s, " +
                      $"request={ServerConfig.RequestTimeout.TotalSeconds}s). " +
                       "Is Obsidian running with the Local REST API plugin enabled?";
            _logger.Log(msg);
            result = $"ERROR: {msg}";
        }
        catch (HttpRequestException ex)
        {
            var msg = $"HTTP error calling tool '{name}': {ex.Message}";
            _logger.Log(msg);
            result = $"ERROR: {msg}";
        }
        catch (Exception ex)
        {
            _logger.Log($"Tool '{name}' error: {ex}");
            result = $"ERROR: {ex.Message}";
        }

        _logger.Log($"CallTool '{name}' result (first 200 chars): {result[..Math.Min(200, result.Length)]}");

        return new JsonObject
        {
            [KeyContent] = new JsonArray
            {
                new JsonObject { [KeyType] = TypeText, [KeyText] = result }
            }
        };
    }

    // ── Capability / tool-list builders ──────────────────────────────────────

    static JsonObject BuildInitializeResult() => new()
    {
        [KeyProtocolVersion] = ServerConfig.ProtocolVersion,
        [KeyCapabilities] = new JsonObject { [KeyTools] = new JsonObject { [KeyListChanged] = false } },
        [KeyServerInfo] = new JsonObject { [KeyName] = ServerConfig.ServerName, [KeyVersion] = ServerConfig.ServerVersion }
    };

    // Automatically reflects whatever tools were registered — no hardcoded list.
    JsonObject BuildToolList() => new()
    {
        [KeyTools] = new JsonArray(_tools
            .Select(t => (JsonNode)new JsonObject
            {
                [KeyName] = t.Name,
                [KeyDescription] = t.Description,
                [KeyInputSchema] = t.InputSchema
            })
            .ToArray())
    };

    // ── JSON-RPC helpers — internal so Program can use them for transport-level errors

    internal static JsonObject Respond(JsonNode? id, JsonObject result) => new()
    {
        [KeyJsonRpc] = JsonRpcVersion,
        [KeyId] = id?.DeepClone(),
        [KeyResult] = result
    };

    internal static JsonObject Error(JsonNode? id, int code, string message) => new()
    {
        [KeyJsonRpc] = JsonRpcVersion,
        [KeyId] = id?.DeepClone(),
        [KeyError] = new JsonObject { [KeyCode] = code, [KeyMessage] = message }
    };
}
