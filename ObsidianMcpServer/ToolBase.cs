using System.Text.Json.Nodes;

/// <summary>
/// Abstract base for all ITool implementations.
/// Provides protected schema-building helpers (Props, Prop, EmptySchema)
/// and RequiredString so concrete tools stay free of boilerplate.
/// </summary>
abstract class ToolBase : ITool
{
    public abstract string       Name        { get; }
    public abstract string       Description { get; }
    public abstract JsonObject   InputSchema { get; }
    public abstract Task<string> ExecuteAsync(JsonObject args, CancellationToken ct = default);

    // ── JSON Schema structural keys ───────────────────────────────────────────
    protected const string SchemaType        = "type";
    protected const string SchemaProperties  = "properties";
    protected const string SchemaRequired    = "required";
    protected const string SchemaDescription = "description";

    // ── JSON Schema type values ───────────────────────────────────────────────
    protected const string TypeObject  = "object";
    protected const string TypeString  = "string";
    protected const string TypeBoolean = "boolean";
    protected const string TypeInteger = "integer";

    // ── Schema helpers ────────────────────────────────────────────────────────

    protected static JsonObject EmptySchema() => new()
    {
        [SchemaType]       = TypeObject,
        [SchemaProperties] = new JsonObject()
    };

    protected static JsonObject Props(params (string name, string type, string desc, bool required)[] props)
    {
        var properties = new JsonObject();
        var required   = new JsonArray();
        foreach (var (n, t, d, r) in props)
        {
            properties[n] = new JsonObject { [SchemaType] = t, [SchemaDescription] = d };
            if (r) required.Add(n);
        }
        var schema = new JsonObject { [SchemaType] = TypeObject, [SchemaProperties] = properties };
        if (required.Count > 0) schema[SchemaRequired] = required;
        return schema;
    }

    protected static (string name, string type, string desc, bool required)
        Prop(string name, string type, string desc, bool required = false)
        => (name, type, desc, required);

    // ── Argument helpers ──────────────────────────────────────────────────────

    protected static string RequiredString(JsonObject args, string key)
    {
        var val = args[key]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(val))
            throw new ArgumentException($"Required argument '{key}' is missing or empty.");
        return val;
    }
}
