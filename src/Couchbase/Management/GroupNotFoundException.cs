using System;

namespace Couchbase.Management
{
    internal class GroupNotFoundException : Exception
    {
        public GroupNotFoundException(string groupName) :
            base($"Group with name {groupName} not found")
        { }
    }
}
