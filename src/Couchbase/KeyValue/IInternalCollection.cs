namespace Couchbase.KeyValue
{
    /// <summary>
    /// Interface for any non-public methods or properties that are needed on a <see cref="ICouchbaseCollection"/>.
    /// </summary>
    internal interface IInternalCollection
    {
        /// <summary>
        /// Gets or sets the identifier for a <see cref="ICouchbaseCollection"/>.
        /// </summary>
        uint? Cid { get; set; }
    }
}
