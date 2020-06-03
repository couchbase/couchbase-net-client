using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Utils;

namespace Couchbase.Core.Diagnostics.Tracing
{
    internal static class TracingExtensions
    {
        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

        internal static long ToMicroseconds(this TimeSpan duration)
        {
            return duration.Ticks / TicksPerMicrosecond;
        }

        internal static IInternalSpan WithDefaultAttributes(this IInternalSpan span)
        {
            span.SetAttribute(CouchbaseTags.OpenTracingTags.Component, ClientIdentifier.GetClientDescription());
            span.SetAttribute(CouchbaseTags.OpenTracingTags.DbType, CouchbaseTags.DbTypeCouchbase);
            span.SetAttribute(CouchbaseTags.OpenTracingTags.SpanKind, CouchbaseTags.OpenTracingTags.SpanKindClient);
            return span;
        }
    }
}
