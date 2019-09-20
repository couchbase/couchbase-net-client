using System;

namespace Couchbase.Management.Users
{
    public class UserNotFoundException : Exception
    {
        public UserNotFoundException(string username)
            : base($"User with username {username} was not found")
        {

        }
    }
}
