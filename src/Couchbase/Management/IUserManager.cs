using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface IUserManager
    {
        Task<UserAndMetaData> GetUserAsync(string username, GetUserOptions options);

        Task<IEnumerable<UserAndMetaData>> GetAllUsersAsync(GetAllUsersOptions options);

        Task UpsertUserAsync(User user, UpsertUserOptions options);

        Task DropUserAsync(string username, DropUserOptions options);

        Task<IEnumerable<RoleAndDescription>> AvailableRolesAsync(AvailableRolesOptions options);

        Task<Group> GetGroupAsync(string groupName, GetGroupOptions options);

        Task<IEnumerable<Group>> GetAllGroupsAsync(GetAllGroupsOptions options);

        Task UpsertGroupAsync(Group group, UpsertGroupOptions options);

        Task DropGroupAsync(string groupName, DropGroupOptions options);
    }
}
