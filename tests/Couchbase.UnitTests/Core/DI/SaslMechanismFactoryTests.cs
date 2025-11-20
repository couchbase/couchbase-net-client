using System;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.DI
{
    public class SaslMechanismFactoryTests
    {
        [Theory]
        [InlineData(MechanismType.Plain, typeof(PlainSaslMechanism))]
        [InlineData(MechanismType.ScramSha1, typeof(ScramShaMechanism))]
        public void Create_GivenType_ExpectedType(MechanismType mechanismType, Type expectedType)
        {
            var username = "ted";
            var password = "secret";

            var saslMechanismFactory = new SaslMechanismFactory(
                new Mock<ILogger<PlainSaslMechanism>>().Object,
                new Mock<ILogger<ScramShaMechanism>>().Object,
                NoopRequestTracer.Instance,
                Mock.Of<IOperationConfigurator>());

            var result = saslMechanismFactory.CreatePasswordMechanism(mechanismType, username, password);

            Assert.IsAssignableFrom(expectedType, result);
        }

        [Theory]
        [InlineData(MechanismType.CramMd5)]
        [InlineData(MechanismType.ScramSha256)]
        [InlineData(MechanismType.ScramSha512)]
        public void Create_UnsupportedType_ArgumentOutOfRangeException(MechanismType mechanismType)
        {
            var username = "ted";
            var password = "secret";

            var saslMechanismFactory = new SaslMechanismFactory(
                new Mock<ILogger<PlainSaslMechanism>>().Object,
                new Mock<ILogger<ScramShaMechanism>>().Object,
                NoopRequestTracer.Instance,
                Mock.Of<IOperationConfigurator>());

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                saslMechanismFactory.CreatePasswordMechanism(mechanismType, username, password));

            Assert.Equal("mechanismType", ex.ParamName);
        }
    }
}
