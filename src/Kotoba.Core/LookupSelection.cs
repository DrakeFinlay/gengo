namespace Kotoba.Core;

/// <summary>Opening a definition is intentional; rendering, focus and history reads are not.</summary>
public sealed class LookupSelection
{
    public string? Current { get; private set; }
    public bool Open(string id)
    {
        var changed = Current != id; Current = id; return changed;
    }
    public void Clear() => Current = null;
}
