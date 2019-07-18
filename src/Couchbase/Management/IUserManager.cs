using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface IUserManager
    {
        Task<User> GetAsync(string username, GetUserOptions options);

        Task<IEnumerable<User>> GetAllAsync(GetAllUserOptions options);

        Task CreateAsync(string username, string password, IEnumerable<UserRole> roles, CreateUserOptions options);

        Task UpsertAsync(string username, IEnumerable<UserRole> roles, UpsertUserOptions options);

        Task DropAsync(string username, DropUserOptions options);
    }
}
