using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes
{
    internal class FakeConnection : IConnection
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public string ContextId { get; }
        public ulong ConnectionId { get; internal set; }
        public bool IsAuthenticated { get; set; }
        public bool IsSecure { get; }
        public bool IsConnected { get; }
        public EndPoint EndPoint { get; internal set; }
        public EndPoint LocalEndPoint { get; internal set; }
        public bool IsDead { get; }
        public TimeSpan IdleTime { get; }
        public ServerFeatureSet ServerFeatures { get; set; }

        public string RemoteHost => throw new NotImplementedException();

        public string LocalHost => throw new NotImplementedException();

        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, IOperation operation, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask CloseAsync(TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public void AddTags(IRequestSpan span)
        {
            span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalHostname, ((IPEndPoint)LocalEndPoint).Address.ToString());
            span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalPort, ((IPEndPoint) LocalEndPoint).Port.ToString());
            span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemoteHostname, ((IPEndPoint)EndPoint).Address.ToString());
            span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemotePort, ((IPEndPoint) EndPoint).Port.ToString());
            span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalId, ConnectionId.ToString());
        }
    }
}
