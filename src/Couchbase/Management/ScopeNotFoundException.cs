using System;

namespace Couchbase.Management
{
    public class ScopeNotFoundException : Exception
    {
        public ScopeNotFoundException(string scopeName)
            : base($"Scope with name {scopeName} not found")
        { }
    }
}