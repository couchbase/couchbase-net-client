using System;
using Couchbase.Core;

namespace Couchbase.Management.Eventing
{
    public class EventingFunctionErrorContext : IErrorContext
    {
        public string Message { get; init; }

        public dynamic Info { get; init; }
    }
}
