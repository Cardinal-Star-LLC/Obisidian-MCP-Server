using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ObsidianMcpServer;

class ReplaceInPlaceTool : ToolBase
{
    readonly IObsidianClient _client;

    public ReplaceInPlaceTool(IObsidianClient client) => _client = client;

    public override string Name => "replace_in_place";

    public override string Description => "Replace all occurrences of a string inside a vault note.";

    public override JsonObject InputSchema => Props(
        Prop(ArgKeys.Path, TypeString, "Vault-relative note path.", required: true),
        Prop("search", TypeString, "String to find.", required: true),
        Prop("replace", TypeString, "String to replace with.", required: true),
        Prop("caseSensitive", TypeBoolean, "Optional case‑sensitive flag.", required: true),
        Prop("regex", TypeBoolean, "Optional regex flag.", required: false));

    public override async Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default)
    {
        var path = RequiredString(args, ArgKeys.Path);
        var search = RequiredString(args, "search");
        var replace = RequiredString(args, "replace");
        var caseSensitive = args["caseSensitive"]?.GetValue<bool>() ?? true;
        var useRegex = args["regex"]?.GetValue<bool>() ?? false;

        if (string.IsNullOrWhiteSpace(search))
            throw new ArgumentException("Search string cannot be empty.");

        if (string.IsNullOrWhiteSpace(replace))
            throw new ArgumentException("Replace string cannot be empty.");

        var readArgs = new JsonObject { [ArgKeys.Path] = path };
        string content = await _client.ReadNote(readArgs, ct);

        if (useRegex)
        {
            var pattern = caseSensitive ? Regex.Escape(search) : $"(?i:{Regex.Escape(search)})";
            content = Regex.Replace(content, pattern, replace);
        }
        else if (!caseSensitive)
        {
            content = ReplaceCaseInsensitive(content, search, replace);
        }
        else
        {
            content = content.Replace(search, replace);
        }

        var writeArgs = new JsonObject { [ArgKeys.Path] = path, [ArgKeys.Content] = content };
        string writeResult = await _client.WriteNote(writeArgs, ct);

        if (!writeResult.StartsWith("HTTP 2"))
            return $"Error writing to '{path}': {writeResult}";

        return $"Replaced '{search}' → '{replace}' in '{path}'.";
    }

    static string ReplaceCaseInsensitive(string content, string search, string replace)
    {
        var lowerContent = content.ToLowerInvariant();
        var lowerSearch = search.ToLowerInvariant();
        int idx = 0;
        while ((idx = lowerContent.IndexOf(lowerSearch, idx, StringComparison.Ordinal)) != -1)
        {
            content = content.Remove(idx, search.Length).Insert(idx, replace);
            idx += replace.Length;
            lowerContent = content.ToLowerInvariant();
        }
        return content;
    }
}