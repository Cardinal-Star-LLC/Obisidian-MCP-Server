using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ObsidianMcpServer;

class InsertInPlaceTool : ToolBase
{
    readonly IObsidianClient _client;

    public InsertInPlaceTool(IObsidianClient client) => _client = client;

    public override string Name => "insert_in_place";

    public override string Description => "Insert text before or after a given anchor in a vault note.";

    public override JsonObject InputSchema => Props(
        Prop(ArgKeys.Path, TypeString, "Vault-relative note path.", required: true),
        Prop("anchor", TypeString, "String to locate.", required: true),
        Prop("text", TypeString, "Text to insert.", required: true),
        Prop("position", TypeString, "Insert before or after anchor; valid values: Before, After.", required: true),
        Prop("caseSensitive", TypeBoolean, "Optional case‑sensitive flag.", required: true),
        Prop("regex", TypeBoolean, "Optional regex flag.", required: false));

    public override async Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default)
    {
        var path = RequiredString(args, ArgKeys.Path);
        var anchor = RequiredString(args, "anchor");
        var insert = RequiredString(args, "text");
        var position = RequiredString(args, "position");
        var caseSensitive = args["caseSensitive"]?.GetValue<bool>() ?? true;
        var useRegex = args["regex"]?.GetValue<bool>() ?? false;

        if (string.IsNullOrWhiteSpace(anchor))
            throw new ArgumentException("Anchor cannot be empty.");

        if (string.IsNullOrWhiteSpace(insert))
            throw new ArgumentException("Insert text cannot be empty.");

        if (position != "Before" && position != "After")
            throw new ArgumentException("Position must be 'Before' or 'After'.");

        var readArgs = new JsonObject { [ArgKeys.Path] = path };
        string content = await _client.ReadNote(readArgs, ct);

        int idx = FindAnchorIndex(content, anchor, caseSensitive, useRegex);
        if (idx < 0)
            return $"Anchor '{anchor}' not found in '{path}'.";

        content = position switch
        {
            "Before" => content.Insert(idx, insert),
            "After" => content.Insert(idx + anchor.Length, insert),
            _ => throw new InvalidOperationException($"Invalid position '{position}'.")
        };

        var writeArgs = new JsonObject { [ArgKeys.Path] = path, [ArgKeys.Content] = content };
        string writeResult = await _client.WriteNote(writeArgs, ct);

        if (!writeResult.StartsWith("HTTP 2"))
            return $"Error writing to '{path}': {writeResult}";

        return $"Inserted '{insert}' {position.ToLowerInvariant()} '{anchor}' in '{path}'.";
    }

    static int FindAnchorIndex(string content, string anchor, bool caseSensitive, bool useRegex)
    {
        if (useRegex)
        {
            var pattern = caseSensitive ? Regex.Escape(anchor) : $"(?i:{Regex.Escape(anchor)})";
            var regExOptions = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            var match = Regex.Match(content, pattern, regExOptions, TimeSpan.FromSeconds(10));
            return match.Success ? match.Index : -1;
        }

        if (!caseSensitive)
        {
            var lowerContent = content.ToLowerInvariant();
            var lowerAnchor = anchor.ToLowerInvariant();
            return lowerContent.IndexOf(lowerAnchor);
        }

        return content.IndexOf(anchor);
    }
}