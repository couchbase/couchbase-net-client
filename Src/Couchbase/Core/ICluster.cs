
using System;
using Couchbase.Core.Buckets;

namespace Couchbase.Core
{
    internal interface ICluster : IDisposable
    {
        IBucket OpenBucket(string bucketname, string password);

        IBucket OpenBucket(string bucketname);

        void CloseBucket(IBucket bucket);

        IClusterInfo Info { get; }
    }
}
