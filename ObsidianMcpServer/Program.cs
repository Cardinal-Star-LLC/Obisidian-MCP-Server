using ObsidianMcpServer;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;


/// <summary>
/// Composition root and stdio transport.
/// The only file that references concrete types — everything else depends on abstractions.
/// To add a new tool: create a class implementing ITool, register it here. Done.
/// </summary>
class Program
{
    const string StartupBanner = "===== Obsidian MCP Server starting — {0:yyyy-MM-dd HH:mm:ss} =====";
    const string TimeoutMessage = "Request timed out after {0:0}s.";

    static async Task Main()
    {
        Console.SetOut(Console.Error);

        var logger = new Logger();

        logger.Log(string.Format(StartupBanner, DateTime.Now));

        var apiKey = Environment.GetEnvironmentVariable(ServerConfig.ApiKeyEnvVar) ?? "";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.Log($"ERROR: {ServerConfig.ApiKeyEnvVar} environment variable is not set. Exiting.");
            Environment.Exit(1);
        }
        logger.Log($"{ServerConfig.ApiKeyEnvVar} found.");

        // Obsidian's embedded server closes connections after each response;
        // ConnectionClose = true prevents stale pooled connections from hanging.
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            UseCookies = false
        };
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(ServerConfig.BaseUrl),
            // Set to WriteTimeout (the larger of the two) so HttpClient never
            // fires before our own per-operation linked tokens do.
            Timeout     = ServerConfig.WriteTimeout
        };
        http.DefaultRequestHeaders.Authorization  = new AuthenticationHeaderValue(ServerConfig.AuthScheme, apiKey);
        http.DefaultRequestHeaders.ConnectionClose = true;
        logger.Log($"HttpClient ready. Base URL: {ServerConfig.BaseUrl}, ReadTimeout: {ServerConfig.ReadTimeout.TotalSeconds}s, WriteTimeout: {ServerConfig.WriteTimeout.TotalSeconds}s, ConnectionClose=true");

        // ── Composition root ──────────────────────────────────────────────────
        // Register tools here. McpServer discovers name, schema, and handler
        // from each ITool — no switch statements, no hardcoded lists.
        IObsidianClient client = new ObsidianClient(http, logger);

        var server = new McpServer(new ITool[]
        {
            new GetStatusTool(client),
            new ListFilesTool(client),
            new ReadNoteTool(client),
            new WriteNoteTool(client),
            new AppendNoteTool(client),
            new DeleteNoteTool(client),
            new SearchTool(client),
            new GetActiveFileTool(client),
            new OpenFileTool(client),
        }, logger);

        // ── stdio transport ───────────────────────────────────────────────────
        var noBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var stdin  = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();
        using var reader = new StreamReader(stdin,  noBom);
        using var writer = new StreamWriter(stdout, noBom) { AutoFlush = true };

        logger.Log("Listening on stdin...");

        while (true)
        {
            string? line;
            try   { line = await reader.ReadLineAsync(); }
            catch (Exception ex) { logger.Log($"stdin read error: {ex.Message}"); break; }

            if (line is null) { logger.Log("stdin closed. Exiting."); break; }
            if (string.IsNullOrWhiteSpace(line)) continue;

            logger.Log($"IN:  {line}");

            JsonObject? request;
            try   { request = JsonNode.Parse(line)?.AsObject(); }
            catch (Exception ex) { logger.Log($"JSON parse error: {ex.Message}"); continue; }
            if (request is null) continue;

            JsonObject? response;
            try
            {
                using var cts = new CancellationTokenSource(ServerConfig.RequestTimeout);
                response = await server.HandleRequest(request, cts.Token).WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.Log($"TIMEOUT handling method '{request[McpServer.KeyMethod]}'");
                response = McpServer.Error(
                    request[McpServer.KeyId],
                    McpServer.InternalError,
                    string.Format(TimeoutMessage, ServerConfig.RequestTimeout.TotalSeconds));
            }
            catch (Exception ex)
            {
                logger.Log($"Unhandled error: {ex}");
                response = McpServer.Error(request[McpServer.KeyId], McpServer.InternalError, ex.Message);
            }

            if (response is not null)
            {
                var json = response.ToJsonString();
                logger.Log($"OUT: {json}");
                try   { await writer.WriteLineAsync(json); }
                catch (Exception ex) { logger.Log($"stdout write error: {ex.Message}"); break; }
            }
        }

        logger.Log("Server stopped.");
    }
}
