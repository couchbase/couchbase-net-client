using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Users
{
    public interface IUserManager
    {
        Task<UserAndMetaData> GetUserAsync(string username, GetUserOptions? options = null);

        Task<IEnumerable<UserAndMetaData>> GetAllUsersAsync(GetAllUsersOptions? options = null);

        Task UpsertUserAsync(User user, UpsertUserOptions? options = null);

        Task DropUserAsync(string username, DropUserOptions? options = null);

        Task<IEnumerable<RoleAndDescription>> GetRolesAsync(AvailableRolesOptions? options = null);

        Task<Group> GetGroupAsync(string groupName, GetGroupOptions? options = null);

        Task<IEnumerable<Group>> GetAllGroupsAsync(GetAllGroupsOptions? options = null);

        Task UpsertGroupAsync(Group group, UpsertGroupOptions? options = null);

        Task DropGroupAsync(string groupName, DropGroupOptions? options = null);
    }
}
