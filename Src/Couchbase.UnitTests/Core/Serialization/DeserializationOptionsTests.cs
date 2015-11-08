using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Serialization;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core.Serialization
{
    [TestFixture]
    public class DeserializationOptionsTests
    {
        #region HasSettings

        [Test]
        public void HasSettings_DefaultObject_ReturnsFalse()
        {
            // Arrange

            var settings = new DeserializationOptions();

            // Act

            var result = settings.HasSettings;

            // Assert

            Assert.False(result);
        }

        [Test]
        public void HasSettings_WithCustomObjectCreator_ReturnsTrue()
        {
            // Arrange

            var settings = new DeserializationOptions()
            {
                CustomObjectCreator = new Mock<ICustomObjectCreator>().Object
            };

            // Act

            var result = settings.HasSettings;

            // Assert

            Assert.True(result);
        }

        #endregion
    }
}
