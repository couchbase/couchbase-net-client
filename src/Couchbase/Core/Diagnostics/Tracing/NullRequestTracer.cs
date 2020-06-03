using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.Diagnostics.Tracing
{
    internal class NullRequestTracer : IRequestTracer
    {
        internal static readonly NullRequestTracer Instance = new NullRequestTracer();
        private static readonly NullSpan NullSpanInstance = new NullSpan();
        public IInternalSpan InternalSpan(string operationName, IRequestSpan parent) => NullSpanInstance;

        public IRequestSpan RequestSpan(string operationName, IRequestSpan parent) => NullSpanInstance;

        private class NullSpan : IInternalSpan
        {
            public void Dispose() { }

            public void Finish() { }

            public IInternalSpan StartPayloadEncoding() => this;

            public IInternalSpan StartDispatch() => this;

            public IInternalSpan SetAttribute(string key, string value) => this;
        }
    }
}
