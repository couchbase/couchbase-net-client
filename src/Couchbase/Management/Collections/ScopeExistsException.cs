using System;

namespace Couchbase.Management.Collections
{
    public class ScopeExistsException : Exception
    {
        public ScopeExistsException(string scopeName)
            : base($"Scope with name {scopeName} already exists")
        { }
    }
}
