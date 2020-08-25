using System.Collections.Generic;
using System.Linq;
using Couchbase.Management.Users;
using Xunit;

namespace Couchbase.UnitTests.Management
{
    public class UserTests
    {
        [Fact]
        public void Test_GetUserFormValues_RoleApplicableToAllBucket()
        {
            //Setup

            const string roleApplicableToAllBucket = "data_reader[*]";

            var user = new User("username")
            {
                DisplayName = "DisplayName",
                Domain = "Domain",
                Roles = new List<Role>
                {
                    new Role("data_reader")
                }
            };

            //Act

            var formValues = user.GetUserFormValues();
            var roles = formValues.Where(x => x.Key.Equals("roles"));

            //Assert

            Assert.Equal(roles.First().Value, roleApplicableToAllBucket);
        }

        [Fact]
        public void Test_GetUserFormValues_RoleLimitedToBucket()
        {
            //Setup

            const string roleLimitedToBucket = "data_reader[bucket]";

            var user = new User("username")
            {
                DisplayName = "DisplayName",
                Domain = "Domain",
                Roles = new List<Role>
                {
                    new Role("data_reader", "bucket")
                }
            };


            //Act

            var formValues = user.GetUserFormValues();
            var roles = formValues.Where(x => x.Key.Equals("roles"));

            //Assert

            Assert.Equal(roles.First().Value, roleLimitedToBucket);
        }

        [Fact]
        public void Test_GetUserFormValues_RoleLimitedToBucketScope()
        {
            //Setup

            const string roleLimitedToBucketScope = "data_reader[bucket:s]";

            var user = new User("username")
            {
                DisplayName = "DisplayName",
                Domain = "Domain",
                Roles = new List<Role>
                {
                    new Role("data_reader", "bucket", "s")
                }
            };


            //Act

            var formValues = user.GetUserFormValues();
            var roles = formValues.Where(x => x.Key.Equals("roles"));

            //Assert
            Assert.Equal(roles.First().Value, roleLimitedToBucketScope);
        }

        [Fact]
        public void Test_GetUserFormValues_RoleLimitedToBucketScopeCollection()
        {
            //Setup

            const string roleLimitedToBucketScopeCollection = "data_reader[bucket:s:c]";

            var user = new User("username")
            {
                DisplayName = "DisplayName",
                Domain = "Domain",
                Roles = new List<Role>
                {
                    new Role("data_reader", "bucket", "s", "c")
                }
            };

            //Act

            var formValues = user.GetUserFormValues();
            var roles = formValues.Where(x => x.Key.Equals("roles"));

            //Assert
            Assert.Equal(roles.First().Value, roleLimitedToBucketScopeCollection);
        }

        [Fact]
        public void Test_GetUserFormValues_All()
        {
            //Setup
            const string all = "data_reader[*],data_reader[bucket],data_reader[bucket:s],data_reader[bucket:s:c]";

            var user = new User("username")
            {
                DisplayName = "DisplayName",
                Domain = "Domain",
                Roles = new List<Role>
                {
                    new Role("data_reader"),
                    new Role("data_reader", "bucket"),
                    new Role("data_reader", "bucket", "s"),
                    new Role("data_reader", "bucket", "s", "c")
                }
            };

            //Act

            var formValues = user.GetUserFormValues();
            var roles = formValues.Where(x => x.Key.Equals("roles"));

            //Assert
            Assert.Equal(roles.First().Value,all);
        }
    }
}
