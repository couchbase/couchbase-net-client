namespace Couchbase;

/// <summary>
/// Adds additional functionality for internal use and testing to the ICluster interface
/// </summary>
internal interface IClusterExtended
{
    /// <summary>
    /// Removes a <see cref="IBucket"/> from the internal cache.
    /// </summary>
    /// <param name="bucketName">The name of the <see cref="IBucket"/> to remove.</param>
    void RemoveBucket(string bucketName);

    /// <summary>
    /// Checks for the existence of a cached <see cref="IBucket"/>.
    /// </summary>
    /// <param name="bucketName">The name of the <see cref="IBucket"/>.</param>
    /// <returns>True if the <see cref="IBucket"/> is found; otherwise false.</returns>
    bool BucketExists(string bucketName);
}
