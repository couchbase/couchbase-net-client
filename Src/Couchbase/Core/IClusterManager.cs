using System;
using System.Collections.Generic;
using Couchbase.Configuration.Server.Providers;

namespace Couchbase.Core
{
    internal interface IClusterManager : IConfigPublisher, IDisposable 
    {
        List<IConfigProvider> ConfigProviders { get; }

        IConfigProvider GetProvider(string name);

        IBucket CreateBucket(string bucketName);

        IBucket CreateBucket(string bucketName, string password);

        void DestroyBucket(IBucket bucket);
    }
}
