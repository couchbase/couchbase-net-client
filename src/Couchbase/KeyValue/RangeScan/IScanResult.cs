using System;

namespace Couchbase.KeyValue.RangeScan
{
    /// <summary>
    /// An individual result from a KV Range Scan
    /// </summary>
    public interface IScanResult
    {
        /// <summary>
        /// Indicates if the scan was requested with IDs only.
        /// </summary>
        bool IdOnly { get; }

        /// <summary>
        /// The identifier for the document.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Returns the content of the scan result if <see cref="ScanOptions.IdsOnly"/> is false.
        /// </summary>
        /// <typeparam name="T">The underlying document type; POCO or dynamic/object for example.</typeparam>
        /// <returns></returns>
        T ContentAs<T>();

        public byte[] ContentAsBytes();
        public string ContentAsString();

        /// <summary>
        /// The time in which the document will expire and be evicted from the cluster.
        /// </summary>
        DateTime? ExpiryTime { get; }

        /// <summary>
        /// Compare and Set value for optimistic locking of a document.
        /// </summary>
        ulong Cas { get; }


    }
}
