namespace Couchbase.Core.Monitoring
{
    public interface IEndpointDiagnostics
    {
        /// <summary>
        /// Gets the service type.
        /// </summary>
        ServiceType Type { get; }

        /// <summary>
        /// Gets the report ID.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the local endpoint address including port.
        /// </summary>
        string Local { get; }

        /// <summary>
        /// Gets the remote endpoint address including port.
        /// </summary>
        string Remote { get; }

        /// <summary>
        /// Gets the last activity for the service endpoint express as microseconds.
        /// </summary>
        long? LastActivity { get; }

        /// <summary>
        /// Gets the latency for service endpint expressed as microseconds.
        /// </summary>
        long? Latency { get; }

        /// <summary>
        /// Gets the scope for the service endpoint.
        /// This could be the bucket name for <see cref="ServiceType.KeyValue"/> service endpoints.
        /// </summary>
        string Scope { get; }
    }
}
