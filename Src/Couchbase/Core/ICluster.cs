
using System;

namespace Couchbase.Core
{
    internal interface ICluster : IDisposable
    {
        IBucket OpenBucket(string bucketName, string passWord, string userName);

        IBucket OpenBucket(string bucketName);

        void CloseBucket(IBucket bucket);

        IClusterInfo Info { get; }
    }
}
