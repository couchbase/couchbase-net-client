using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface IUserManager
    {
        User Get();

        IEnumerable<User> GetAll();

        Task Insert(string userName, UserOptions options);

        Task Remove(string userName);
    }
}
