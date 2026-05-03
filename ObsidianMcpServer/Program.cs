using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace ObsidianMcpServer;

/// <summary>
/// Composition root and stdio transport.
/// The only file that references concrete types — everything else depends on abstractions.
/// To add a new tool: create a class implementing ITool, register it here. Done.
/// </summary>
public static class Program
{
    const string StartupBanner = "===== Obsidian MCP Server starting — {0:yyyy-MM-dd HH:mm:ss} =====";
    const string TimeoutMessage = "Request timed out after {0:0}s.";
    static readonly Logger _logger = new Logger();

    static async Task Main()
    {

        string? baseUrl = GetApiBaseUrl();

        Console.SetOut(Console.Error);

        _logger.Log(string.Format(StartupBanner, DateTime.Now));

        string apiKey = GetApiKey();

        _logger.Log($"{ServerConfig.ApiKeyEnvVar} found.");

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            UseCookies = false
        };

        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = ServerConfig.WriteTimeout
        };

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(ServerConfig.AuthScheme, apiKey);
        http.DefaultRequestHeaders.ConnectionClose = true;
        _logger.Log($"HttpClient ready. Base URL: {baseUrl}, ReadTimeout: {ServerConfig.ReadTimeout.TotalSeconds}s, WriteTimeout: {ServerConfig.WriteTimeout.TotalSeconds}s, ConnectionClose=true");

        IObsidianClient client = new ObsidianClient(http, _logger);

        var server = new McpServer(
        [
            new GetStatusTool(client),
            new ListFilesTool(client),
            new ReadNoteTool(client),
            new WriteNoteTool(client),
            new AppendNoteTool(client),
            new DeleteNoteTool(client),
            new SearchTool(client),
            new GetActiveFileTool(client),
            new OpenFileTool(client),
            new ReplaceInPlaceTool(client),
            new InsertInPlaceTool(client),
        ], _logger);

        var noBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();
        using var reader = new StreamReader(stdin, noBom);
        using var writer = new StreamWriter(stdout, noBom) { AutoFlush = true };

        _logger.Log("Listening on stdin...");

        try
        {
            while (true)
            {
                string line = await reader.ReadLineAsync() ?? throw new InvalidOperationException("Failed to read from stdin.");

                if (line is null)
                {
                    _logger.Log("stdin closed. Exiting.");
                    break;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                _logger.Log($"IN:  {line}");

                if (!TryParseRequest(line, out var request) || request == null)
                {
                    continue;
                }

                JsonObject? response = await ProcessRequestAsync(server, request);

                if (response is null) continue;

                var json = response.ToJsonString();

                _logger.Log($"OUT: {json}");

                try
                {
                    await writer.WriteLineAsync(json);
                }
                catch (Exception ex)
                {
                    _logger.Log($"{ex.Message}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Fatal error in main loop. Exiting.", ex);
        }

        _logger.Log("Server stopped.");
    }

    private static async Task<JsonObject?> ProcessRequestAsync(McpServer server, JsonObject request)
    {
        JsonObject? response;

        try
        {
            using var cts = new CancellationTokenSource(ServerConfig.RequestTimeout);
            response = await server.HandleRequest(request, cts.Token).WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.Log($"TIMEOUT handling method '{request[McpServer.KeyMethod]}'");
            response = McpServer.Error(
                request[McpServer.KeyId],
                McpServer.InternalError,
                string.Format(TimeoutMessage, ServerConfig.RequestTimeout.TotalSeconds));
        }
        catch (Exception ex)
        {
            _logger.Log($"Unhandled error: {ex}");
            response = McpServer.Error(request[McpServer.KeyId], McpServer.InternalError, ex.Message);
        }

        return response;
    }

    private static bool TryParseRequest(string line, out JsonObject? request)
    {
        request = null;

        try
        {
            request = JsonNode.Parse(line)?.AsObject();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log($"{ex.Message}");
        }

        return false;
    }

    private static string GetApiBaseUrl()
    {
        //var baseUrl = Environment.GetEnvironmentVariable("OBSIDIAN_API_BASE_URL");
        var baseUrl = ServerConfig.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.Log($"{ServerConfig.ApiKeyEnvVar} environment variable is not set. Exiting.");
            Environment.Exit(1);
        }

        return baseUrl;
    }

    private static string GetApiKey()
    {
        var apiKey = Environment.GetEnvironmentVariable(ServerConfig.ApiKeyEnvVar) ?? "";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.Log($"{ServerConfig.ApiKeyEnvVar} environment variable is not set. Exiting.");
            Environment.Exit(1);
        }

        return apiKey;
    }
}