using System;

namespace Couchbase.Management.Collections
{
    public class ScopeNotFoundException : CouchbaseException
    {
        public ScopeNotFoundException() { }

        public ScopeNotFoundException(string scopeName)
            : base($"Scope with name {scopeName} not found")
        { }
    }
}
