using System;

namespace Couchbase.Management
{
    [Serializable]
    internal class UserAlreadyExistsException : Exception
    {
        public UserAlreadyExistsException(string username)
            : base($"User with username {username} already exists")
        {

        }
    }
}
