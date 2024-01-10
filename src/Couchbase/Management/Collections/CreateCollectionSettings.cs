using System;

namespace Couchbase.Management.Collections;

public class CreateCollectionSettings
{
    public static readonly TimeSpan NoExpiry = TimeSpan.FromSeconds(-1);

    /// <summary>
    /// The maximum Time-To-Live (TTL) for new documents in the collection.
    /// Unset or 0 : Uses the Bucket's MaxExpiry
    /// -1 (TimeSpan.FromSeconds(-1)) : Documents do not expire. You can use the static constant NoExpiry for this.
    /// </summary>
    public TimeSpan? MaxExpiry { get; set; }

    /// <summary>
    /// Whether history retention override is enabled on this collection. If left unset, it defaults to the bucket-level setting.
    /// </summary>
    public bool? History { get; set; }

    /// <summary>
    /// Optional settings when creating a new Collection.
    /// </summary>
    /// <param name="expiry">See <see cref="MaxExpiry"/></param>
    /// <param name="history">See <see cref="History"/></param>
    public CreateCollectionSettings(TimeSpan? expiry = null, bool? history = null)
    {
        MaxExpiry = expiry;
        History = history;
    }
    public static CreateCollectionSettings Default => new CreateCollectionSettings();
}
