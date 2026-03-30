using System.Text.Json.Nodes;

/// <summary>
/// Abstraction over the Obsidian Local REST API.
/// McpServer and all tools depend on this, never on the concrete HttpClient.
/// CancellationToken is threaded through every operation so the RequestTimeout
/// in Program can cancel cleanly without leaving orphaned HTTP requests.
/// </summary>
interface IObsidianClient
{
    Task<string> GetStatus(CancellationToken ct = default);
    Task<string> ListFiles(JsonObject args, CancellationToken ct = default);
    Task<string> ReadNote(JsonObject args, CancellationToken ct = default);
    Task<string> WriteNote(JsonObject args, CancellationToken ct = default);
    Task<string> AppendNote(JsonObject args, CancellationToken ct = default);
    Task<string> DeleteNote(JsonObject args, CancellationToken ct = default);
    Task<string> SearchNotes(JsonObject args, CancellationToken ct = default);
    Task<string> GetActiveFile(CancellationToken ct = default);
    Task<string> OpenFile(JsonObject args, CancellationToken ct = default);
}
