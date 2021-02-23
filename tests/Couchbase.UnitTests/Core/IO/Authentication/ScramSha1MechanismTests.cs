using System;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Authentication
{
    public class ScramSha1MechanismTests
    {
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void ScramShaMechanism_WhenUsernameIsInvalidinCtor_ThrowArgumentNullException(string username)
        {
            string password = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            // ReSharper disable once ObjectCreationAsStatement
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.Throws<ArgumentNullException>(() =>
                new ScramShaMechanism(MechanismType.ScramSha256, username, password,
                    new Mock<ILogger<ScramShaMechanism>>().Object, NoopRequestTracer.Instance,
                    Mock.Of<IOperationConfigurator>()));
        }

        [Fact]
        public void ScramShaMechanism_Authenticate_IsPopulated()
        {
            var password = "secret";
            var username = "authenticated";

            var mech = new ScramShaMechanism(MechanismType.ScramSha256, username, password,
                new Mock<ILogger<ScramShaMechanism>>().Object, NoopRequestTracer.Instance,
                Mock.Of<IOperationConfigurator>());
            Assert.True(!string.IsNullOrEmpty(mech.ClientNonce));
        }

        [Fact]
        public void ScramShaMechanism_ClientNonce_IsPopulated()
        {
            var password = "secret";
            var username = "authenticated";

            var mech = new ScramShaMechanism(MechanismType.ScramSha256, username, password,
                new Mock<ILogger<ScramShaMechanism>>().Object, NoopRequestTracer.Instance,
                Mock.Of<IOperationConfigurator>());
            Assert.True(!string.IsNullOrEmpty(mech.ClientNonce));
        }
    }
}
