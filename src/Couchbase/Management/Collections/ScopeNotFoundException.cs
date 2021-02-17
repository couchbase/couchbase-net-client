using System;
using Couchbase.Core.Retry;

namespace Couchbase.Management.Collections
{
    public class ScopeNotFoundException : CouchbaseException, IRetryable
    {
        public ScopeNotFoundException() { }

        public ScopeNotFoundException(string scopeName)
            : base($"Scope with name {scopeName} not found")
        { }
    }
}
