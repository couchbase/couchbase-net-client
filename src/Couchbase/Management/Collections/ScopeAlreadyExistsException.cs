using System;

namespace Couchbase.Management.Collections
{
    public class ScopeAlreadyExistsException : Exception
    {
        public ScopeAlreadyExistsException(string scopeName)
            : base($"Scope with name {scopeName} already exists")
        { }
    }
}
