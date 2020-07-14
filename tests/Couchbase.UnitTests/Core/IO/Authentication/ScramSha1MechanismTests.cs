using System;
using Couchbase.Core.IO.Authentication;
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
            var transcoder = new LegacyTranscoder();
            string password = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            // ReSharper disable once ObjectCreationAsStatement
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.Throws<ArgumentNullException>(() =>
                new ScramShaMechanism(transcoder, MechanismType.ScramSha256, username, password,
                    new Mock<ILogger<ScramShaMechanism>>().Object));
        }

        [Fact]
        public void ScramShaMechanism_Authenticate_IsPopulated()
        {
            var transcoder = new LegacyTranscoder();
            var password = "secret";
            var username = "authenticated";

            var mech = new ScramShaMechanism(transcoder, MechanismType.ScramSha256, username, password,
                new Mock<ILogger<ScramShaMechanism>>().Object);
            Assert.True(!string.IsNullOrEmpty(mech.ClientNonce));
        }

        [Fact]
        public void ScramShaMechanism_ClientNonce_IsPopulated()
        {
            var transcoder = new LegacyTranscoder();
            var password = "secret";
            var username = "authenticated";

            var mech = new ScramShaMechanism(transcoder, MechanismType.ScramSha256, username, password,
                new Mock<ILogger<ScramShaMechanism>>().Object);
            Assert.True(!string.IsNullOrEmpty(mech.ClientNonce));
        }

        [Fact]
        public void ScramShaMechanism_WhenTranscoderIsNullinCtor_ThrowArgumentNullException()
        {
            ITypeTranscoder transcoder = null;
            var username = "beef";
            var password = "stew";

            // ReSharper disable once ExpressionIsAlwaysNull
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Throws<ArgumentNullException>(() => new ScramShaMechanism(transcoder, MechanismType.ScramSha256,
                username, password, new Mock<ILogger<ScramShaMechanism>>().Object));
        }
    }
}
