#nullable enable
using System;
using System.Diagnostics;

namespace Couchbase.Core.Diagnostics.Tracing
{
    internal class NullRequestTracer : IRequestTracer
    {
        internal static readonly NullRequestTracer Instance = new NullRequestTracer();
        internal static readonly IInternalSpan NullSpanInstance = new NullSpan();
        public IInternalSpan InternalSpan(string operationName, IRequestSpan parent) => NullSpanInstance;

        public IRequestSpan RequestSpan(string operationName, IRequestSpan parent) => NullSpanInstance;

        private class NullSpan : IInternalSpan
        {
            public Activity? Activity => null;

            /// <inheritdoc />
            public bool IsNullSpan => true;

            public void Dispose() { }

            public TimeSpan? Duration => null;

            public IInternalSpan StartPayloadEncoding() => this;

            public IInternalSpan StartDispatch() => this;

            public IInternalSpan WithTag(string key, string value) => this;
        }
    }
}
