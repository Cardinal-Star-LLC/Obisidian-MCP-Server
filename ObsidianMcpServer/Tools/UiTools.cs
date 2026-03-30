using System.Text.Json.Nodes;

class GetActiveFileTool : ToolBase
{
    readonly IObsidianClient _client;
    internal GetActiveFileTool(IObsidianClient client) => _client = client;

    public override string     Name        => "obsidian_get_active_file";
    public override string     Description => "Get the content of the currently open file in Obsidian.";
    public override JsonObject InputSchema => EmptySchema();

    public override Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default)
        => _client.GetActiveFile(ct);
}

class OpenFileTool : ToolBase
{
    readonly IObsidianClient _client;
    internal OpenFileTool(IObsidianClient client) => _client = client;

    public override string Name        => "obsidian_open_file";
    public override string Description => "Open a file in the Obsidian UI.";
    public override JsonObject InputSchema => Props(
        Prop(ArgKeys.Path,    TypeString,  "Vault-relative file path.", required: true),
        Prop(ArgKeys.NewLeaf, TypeBoolean, "Open in a new pane (default false)."));

    public override Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default)
        => _client.OpenFile(args, ct);
}
