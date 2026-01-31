using System;

namespace Couchbase.Management.Views
{
    /// <summary>
    /// Creates an <see cref="IViewIndexManager"/> for a given bucket.
    /// </summary>
    [Obsolete("The View service has been deprecated use the Query service instead.")]
    internal interface IViewIndexManagerFactory
    {
        /// <summary>
        /// Creates an <see cref="IViewIndexManager"/> for a given bucket.
        /// </summary>
        /// <param name="bucketName">Name of the bucket.</param>
        /// <returns>The <see cref="IViewIndexManager"/>.</returns>
        public IViewIndexManager Create(string bucketName);
    }
}
