using System.Text.Json.Nodes;

class GetStatusTool : ToolBase
{
    readonly IObsidianClient _client;
    internal GetStatusTool(IObsidianClient client) => _client = client;

    public override string     Name        => "obsidian_status";
    public override string     Description => "Get the Obsidian server status and vault information.";
    public override JsonObject InputSchema => EmptySchema();

    public override Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default)
        => _client.GetStatus(ct);
}
