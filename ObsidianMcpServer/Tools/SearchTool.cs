using System.Text.Json.Nodes;

class SearchTool : ToolBase
{
    readonly IObsidianClient _client;
    internal SearchTool(IObsidianClient client) => _client = client;

    public override string Name        => "obsidian_search";
    public override string Description => "Search across all notes using simple full-text search.";
    public override JsonObject InputSchema => Props(
        Prop(ArgKeys.Query,         TypeString,  "Search query.", required: true),
        Prop(ArgKeys.ContextLength, TypeInteger, "Characters of context around each match (default 100)."));

    public override Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default)
        => _client.SearchNotes(args, ct);
}
