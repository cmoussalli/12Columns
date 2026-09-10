namespace TwelveColumns.Demo;

/// <summary>
/// Stands in for whatever a real app would persist dashboards to — a database, a file, a
/// user profile. The demo only needs the round trip to be visible, so it keeps the JSON in
/// memory for the lifetime of the process.
/// </summary>
public sealed class LayoutStore
{
    private readonly Dictionary<string, string> _saved = new(StringComparer.Ordinal);

    public void Save(string key, string json) => _saved[key] = json;

    public string? Load(string key) => _saved.GetValueOrDefault(key);

    public bool Has(string key) => _saved.ContainsKey(key);
}
