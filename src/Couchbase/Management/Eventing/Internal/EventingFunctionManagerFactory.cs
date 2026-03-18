#nullable enable
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

namespace Couchbase.Management.Eventing.Internal;

internal interface IEventingFunctionManagerFactory
{
    EventingFunctionManager CreateClusterLevel();
    EventingFunctionManager CreateScoped(IScope scope);
}

internal class EventingFunctionManagerFactory(
    IEventingFunctionService service,
    ILoggerFactory loggerFactory,
    IRequestTracer tracer,
    IServiceUriProvider serviceUriProvider) : IEventingFunctionManagerFactory
{
    public EventingFunctionManager CreateClusterLevel() => new EventingFunctionManager(service,
        loggerFactory.CreateLogger<EventingFunctionManager>(), tracer, serviceUriProvider);

    public EventingFunctionManager CreateScoped(IScope scope) => new EventingFunctionManager(service,
        loggerFactory.CreateLogger<EventingFunctionManager>(), tracer, serviceUriProvider, new EventingFunctionKeyspace(
            bucketName: scope.Bucket.Name,
            scopeName: scope.Name,
            collectionName: null
        ));
}
