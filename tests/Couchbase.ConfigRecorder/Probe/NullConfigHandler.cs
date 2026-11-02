using Couchbase.Core.Configuration.Server;

namespace Couchbase.ConfigRecorder.Probe;

/// <summary>
/// Drops every config. The probe nodes publish pushed configs to their context, and the probe does not use them.
/// </summary>
internal sealed class NullConfigHandler : IConfigHandler
{
    public void Start(bool withPolling = false)
    {
    }

    public void Publish(BucketConfig config)
    {
    }

    public void Subscribe(IConfigUpdateEventSink configSubscriber)
    {
    }

    public void Unsubscribe(IConfigUpdateEventSink configSubscriber)
    {
    }

    public BucketConfig Get(string bucketName) => throw new NotSupportedException();

    public void Clear()
    {
    }

    public void Dispose()
    {
    }
}
