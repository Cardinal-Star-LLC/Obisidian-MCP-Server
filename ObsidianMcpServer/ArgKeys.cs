/// <summary>
/// JSON argument key names shared between ObsidianClient (which reads them)
/// and tool schema definitions (which declare them via Prop()).
/// Centralised here so a rename is a one-line change.
/// </summary>
static class ArgKeys
{
    public const string Path          = "path";
    public const string Content       = "content";
    public const string Recursive     = "recursive";
    public const string Query         = "query";
    public const string ContextLength = "contextLength";
    public const string NewLeaf       = "newLeaf";
}
