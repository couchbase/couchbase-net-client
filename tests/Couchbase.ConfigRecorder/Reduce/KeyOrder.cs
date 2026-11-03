namespace Couchbase.ConfigRecorder.Reduce;

/// <summary>
/// The order in which the server writes object keys, merged over many objects.
/// A key seen for the first time goes right after the key that comes before it in that object.
/// </summary>
internal sealed class KeyOrder
{
    private readonly List<string> _keys = [];

    public IReadOnlyList<string> Keys => _keys;

    public void Merge(IEnumerable<string> keys)
    {
        var at = -1;
        foreach (var key in keys)
        {
            var index = _keys.IndexOf(key);
            if (index < 0)
            {
                _keys.Insert(at + 1, key);
                index = at + 1;
            }

            at = Math.Max(at, index);
        }
    }
}
