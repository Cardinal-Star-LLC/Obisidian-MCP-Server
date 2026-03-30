using System.Text.Json.Nodes;

/// <summary>
/// A single MCP tool. Implement this to add a new tool without modifying
/// McpServer — open for extension, closed for modification.
/// </summary>
interface ITool
{
    /// <summary>MCP tool name as sent over the wire (e.g. "obsidian_read_note").</summary>
    string Name { get; }

    /// <summary>Human-readable description returned in tools/list.</summary>
    string Description { get; }

    /// <summary>JSON Schema object describing the tool's accepted arguments.</summary>
    JsonObject InputSchema { get; }

    /// <summary>Execute the tool and return a plain-text result string.</summary>
    Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default);
}
