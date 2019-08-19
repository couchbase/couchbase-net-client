using System;

namespace Couchbase.Management
{
    public class ScopeAlreadyExistsException : Exception
    {
        public ScopeAlreadyExistsException(string scopeName)
            : base($"Scope with name {scopeName} already exists")
        { }
    }
}