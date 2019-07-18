using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public static class UserManagerExtensions
    {
        public static Task<User> GetAsync(this IUserManager userManager, string username)
        {
            return userManager.GetAsync(username, GetUserOptions.Default);
        }

        public static Task<User> GetAsync(this IUserManager userManager, string username, Action<GetUserOptions> configureOptions)
        {
            var options = new GetUserOptions();
            configureOptions(options);

            return userManager.GetAsync(username, options);
        }

        public static Task<IEnumerable<User>> GetAllAsync(this IUserManager userManager)
        {
            return userManager.GetAllAsync(GetAllUserOptions.Default);
        }

        public static Task<IEnumerable<User>> GetAllAsync(this IUserManager userManager, Action<GetAllUserOptions> configureOptions)
        {
            var options = new GetAllUserOptions();
            configureOptions(options);

            return userManager.GetAllAsync(options);
        }

        public static Task CreateAsync(this IUserManager userManager, string username, string password, IEnumerable<UserRole> roles)
        {
            return userManager.CreateAsync(username, password, roles, CreateUserOptions.Default);
        }

        public static Task CreateAsync(this IUserManager userManager, string username, string password, IEnumerable<UserRole> roles, Action<CreateUserOptions> configureOptions)
        {
            var options = new CreateUserOptions();
            configureOptions(options);

            return userManager.CreateAsync(username, password, roles, options);
        }

        public static Task UpsertAsync(this IUserManager userManager, string username, IEnumerable<UserRole> roles)
        {
            return userManager.UpsertAsync(username, roles, UpsertUserOptions.Default);
        }

        public static Task UpsertAsync(this IUserManager userManager, string username, IEnumerable<UserRole> roles, Action<UpsertUserOptions> configureOptions)
        {
            var options = new UpsertUserOptions();
            configureOptions(options);

            return userManager.UpsertAsync(username, roles, options);
        }

        public static Task DropAsync(this IUserManager userManager, string username)
        {
            return userManager.DropAsync(username, DropUserOptions.Default);
        }

        public static Task DropAsync(this IUserManager userManager, string username, Action<DropUserOptions> configureOptions)
        {
            var options = new DropUserOptions();
            configureOptions(options);

            return userManager.DropAsync(username, options);
        }
    }
}
