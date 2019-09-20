using System;

namespace Couchbase.Management.Users
{
    internal class GroupNotFoundException : Exception
    {
        public GroupNotFoundException(string groupName) :
            base($"Group with name {groupName} not found")
        { }
    }
}
