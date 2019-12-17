using System;

namespace Couchbase.Management.Users
{
    [Serializable]
    internal class UserExistsException : Exception
    {
        public UserExistsException(string username)
            : base($"User with username {username} already exists")
        {

        }
    }
}
