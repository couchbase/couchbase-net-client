using System;

namespace Couchbase.Management.Collections;

public class UpdateCollectionSettings
{
    /// <summary>
    /// The maximum Time-To-Live (TTL) for new documents in the collection. If left unset, it defaults to no expiry.
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
    public UpdateCollectionSettings(TimeSpan? expiry = null, bool? history = null)
    {
        MaxExpiry = expiry;
        History = history;
    }

    public static UpdateCollectionSettings Default => new UpdateCollectionSettings();
}
