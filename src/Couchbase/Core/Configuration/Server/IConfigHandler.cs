using System;

#nullable enable

namespace Couchbase.Core.Configuration.Server
{
    internal interface IConfigHandler : IDisposable
    {
        void Start(bool withPolling = false);
        void Publish(BucketConfig config);
        void Subscribe(BucketBase bucket);
        void Unsubscribe(BucketBase bucket);
        BucketConfig Get(string bucketName);
        void Clear();
    }
}
