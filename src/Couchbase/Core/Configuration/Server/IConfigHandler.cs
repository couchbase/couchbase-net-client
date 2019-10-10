using System;
using System.Threading;

namespace Couchbase.Core.Configuration.Server
{
    internal interface IConfigHandler : IDisposable
    {
        CancellationTokenSource TokenSource { get; set; }
        event ConfigHandler.BucketConfigHandler ConfigChanged;
        void Start(CancellationTokenSource tokenSource);
        void Stop();
        void Poll(CancellationToken token = default(CancellationToken));
        void Process();
        void Publish(BucketConfig config);
        void Subscribe(BucketBase bucket);
        void Unsubscribe(BucketBase bucket);
        BucketConfig Get(string bucketName);
        void Clear();
    }
}
