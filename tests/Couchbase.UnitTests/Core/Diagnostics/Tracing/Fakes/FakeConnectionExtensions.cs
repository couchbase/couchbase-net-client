using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes
{
   internal static class FakeConnectionExtensions
    {
        public static void AddTags(this IConnection connection, IRequestSpan span)
        {
            span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalHostname, connection.LocalEndPoint.ToString());
            span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalPort, ((IPEndPoint)connection.LocalEndPoint).Port.ToString());
            span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemoteHostname, connection.EndPoint.ToString());
            span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemotePort, ((IPEndPoint)connection.EndPoint).Port.ToString());
            span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalId, connection.ConnectionId.ToString());
        }
    }
}
