
using System;

namespace Couchbase.Core
{
    internal interface ICluster : IDisposable
    {
        IBucket OpenBucket(string bucketName, string passWord, string userName);

        IBucket OpenBucket(string bucketName);

        IClusterInfo Info { get; }
    }
}
