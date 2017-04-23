using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class ClusterHelperTests
    {
        [Test]
        public void Initialized_AfterInit_ReturnsTrue()
        {
            try
            {
                // Arrange

                Assert.False(ClusterHelper.Initialized);

                // Act

                ClusterHelper.Initialize(new ClientConfiguration());

                // Assert

                Assert.True(ClusterHelper.Initialized);
            }
            finally
            {
                // Cleanup

                ClusterHelper.Close();
            }
        }

        [Test]
        public void Initialized_AfterClose_ReturnsFalse()
        {
            try
            {
                // Arrange

                Assert.False(ClusterHelper.Initialized);

                ClusterHelper.Initialize(new ClientConfiguration());

                Assert.True(ClusterHelper.Initialized);

                // Act

                ClusterHelper.Close();

                // Assert

                Assert.False(ClusterHelper.Initialized);
            }
            finally
            {
                // Cleanup

                ClusterHelper.Close();
            }
        }
    }
}
