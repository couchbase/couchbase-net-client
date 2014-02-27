
using System;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO.Operations;

namespace Couchbase.Core
{
    public interface IBucket : IDisposable
    {
        string Name { get; }

        IOperationResult<T> Insert<T>(string key, T value);

        IOperationResult<T> Get<T>(string key);
    }
}
