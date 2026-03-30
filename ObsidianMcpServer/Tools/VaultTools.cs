using System.Text.Json.Nodes;

class ListFilesTool : ToolBase
{
    readonly IObsidianClient _client;
    internal ListFilesTool(IObsidianClient client) => _client = client;

    public override string Name        => "obsidian_list_files";
    public override string Description => "List files in the Obsidian vault. Set recursive=true to walk all subdirectories and return every file in the vault.";
    public override JsonObject InputSchema => Props(
        Prop(ArgKeys.Path,      TypeString,  "Vault-relative directory path (leave empty for root)."),
        Prop(ArgKeys.Recursive, TypeBoolean, "If true, recursively list all files in all subdirectories. Default false."));

    public override Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default)
        => _client.ListFiles(args, ct);
}

class ReadNoteTool : ToolBase
{
    readonly IObsidianClient _client;
    internal ReadNoteTool(IObsidianClient client) => _client = client;

    public override string Name        => "obsidian_read_note";
    public override string Description => "Read the Markdown content of a note.";
    public override JsonObject InputSchema => Props(
        Prop(ArgKeys.Path, TypeString, "Vault-relative file path, e.g. folder/note.md.", required: true));

    public override Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default)
        => _client.ReadNote(args, ct);
}

class WriteNoteTool : ToolBase
{
    readonly IObsidianClient _client;
    internal WriteNoteTool(IObsidianClient client) => _client = client;

    public override string Name        => "obsidian_write_note";
    public override string Description => "Create or completely overwrite a note with new Markdown content.";
    public override JsonObject InputSchema => Props(
        Prop(ArgKeys.Path,    TypeString, "Vault-relative file path.", required: true),
        Prop(ArgKeys.Content, TypeString, "Full Markdown content to write.", required: true));

    public override Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default)
        => _client.WriteNote(args, ct);
}

class AppendNoteTool : ToolBase
{
    readonly IObsidianClient _client;
    internal AppendNoteTool(IObsidianClient client) => _client = client;

    public override string Name        => "obsidian_append_note";
    public override string Description => "Append text to the end of an existing note.";
    public override JsonObject InputSchema => Props(
        Prop(ArgKeys.Path,    TypeString, "Vault-relative file path.", required: true),
        Prop(ArgKeys.Content, TypeString, "Markdown text to append.", required: true));

    public override Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default)
        => _client.AppendNote(args, ct);
}

class DeleteNoteTool : ToolBase
{
    readonly IObsidianClient _client;
    internal DeleteNoteTool(IObsidianClient client) => _client = client;

    public override string Name        => "obsidian_delete_note";
    public override string Description => "Permanently delete a note from the vault.";
    public override JsonObject InputSchema => Props(
        Prop(ArgKeys.Path, TypeString, "Vault-relative file path.", required: true));

    public override Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default)
        => _client.DeleteNote(args, ct);
}
