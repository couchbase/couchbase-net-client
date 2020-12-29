using System;
using System.Threading;
using Couchbase.Core.IO.Operations;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    public class CancellationTokenPairTests
    {
        #region CanBeCanceled

        [Fact]
        public void CanBeCanceled_DefaultToken_False()
        {
            // Arrange

            var tokenPair = default(CancellationTokenPair);

            // Assert

            Assert.False(tokenPair.CanBeCanceled);
        }

        [Fact]
        public void CanBeCanceled_ExternalToken_True()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            var tokenPair = CancellationTokenPair.FromExternalToken(cts.Token);

            // Assert

            Assert.True(tokenPair.CanBeCanceled);
        }

        [Fact]
        public void CanBeCanceled_InternalToken_True()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            var tokenPair = CancellationTokenPair.FromInternalToken(cts.Token);

            // Assert

            Assert.True(tokenPair.CanBeCanceled);
        }

        [Fact]
        public void CanBeCanceled_SameToken_True()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            var tokenPair = new CancellationTokenPair(cts.Token, cts.Token);

            // Assert

            Assert.True(tokenPair.CanBeCanceled);
        }

        [Fact]
        public void CanBeCanceled_TwoTokens_True()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();
            var tokenPair = new CancellationTokenPair(cts.Token, cts2.Token);

            // Assert

            Assert.True(tokenPair.CanBeCanceled);
        }

        #endregion

        #region IsCancellationRequested

        [Fact]
        public void IsCancellationRequested_DefaultToken_False()
        {
            // Arrange

            var tokenPair = default(CancellationTokenPair);

            // Assert

            Assert.False(tokenPair.IsCancellationRequested);
        }

        [Fact]
        public void IsCancellationRequested_TokensNotCanceled_False()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(cts.Token, cts2.Token);

            // Assert

            Assert.False(tokenPair.IsCancellationRequested);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void IsCancellationRequested_TokensCanceled_True(bool cancelInternal, bool cancelExternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(cts.Token, cts2.Token);

            if (cancelExternal)
            {
                cts.Cancel();
            }

            if (cancelInternal)
            {
                cts2.Cancel();
            }

            // Assert

            Assert.True(tokenPair.IsCancellationRequested);
        }

        #endregion

        #region IsExternalCancellation

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsExternalCancellation_ExternalCanceled_True(bool hasInternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(cts.Token, hasInternal ? cts2.Token : default);
            cts.Cancel();

            // Act

            var result = tokenPair.IsExternalCancellation;

            // Assert

            Assert.True(result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsExternalCancellation_ExternalNotCanceled_False(bool hasInternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(cts.Token, hasInternal ? cts2.Token : default);
            cts2.Cancel();

            // Act

            var result = tokenPair.IsExternalCancellation;

            // Assert

            Assert.False(result);
        }

        #endregion

        #region IsInternalCancellation

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsInternalCancellation_InternalCanceled_True(bool hasExternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(hasExternal ? cts2.Token : default, cts.Token);
            cts.Cancel();

            // Act

            var result = tokenPair.IsInternalCancellation;

            // Assert

            Assert.True(result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsInternalCancellation_InternalNotCanceled_False(bool hasExternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(hasExternal ? cts2.Token : default, cts.Token);
            cts2.Cancel();

            // Act

            var result = tokenPair.IsInternalCancellation;

            // Assert

            Assert.False(result);
        }

        #endregion

        #region CanceledToken

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CanceledToken_ExternalCanceled_ExternalToken(bool hasInternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(cts.Token, hasInternal ? cts2.Token : default);
            cts.Cancel();

            // Act

            var result = tokenPair.CanceledToken;

            // Assert

            Assert.Equal(cts.Token, result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CanceledToken_InternalCanceled_InternalToken(bool hasExternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(hasExternal ? cts2.Token : default, cts.Token);
            cts.Cancel();

            // Act

            var result = tokenPair.CanceledToken;

            // Assert

            Assert.Equal(cts.Token, result);
        }

        [Fact]
        public void CanceledToken_NothingCanceled_DefaultToken()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(cts.Token, cts2.Token);

            // Act

            var result = tokenPair.CanceledToken;

            // Assert

            Assert.Equal(default, result);
        }

        #endregion

        #region ThrowIfCancellationRequested

        [Fact]
        public void ThrowIfCancellationRequested_DefaultToken_False()
        {
            // Arrange

            var tokenPair = default(CancellationTokenPair);

            // Assert

            tokenPair.ThrowIfCancellationRequested();
        }

        [Fact]
        public void ThrowIfCancellationRequested_TokensNotCanceled_False()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(cts.Token, cts2.Token);

            // Assert

            tokenPair.ThrowIfCancellationRequested();
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void ThrowIfCancellationRequested_TokensCanceled_Throws(bool cancelInternal, bool cancelExternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(cts.Token, cts2.Token);

            if (cancelExternal)
            {
                cts.Cancel();
            }

            if (cancelInternal)
            {
                cts2.Cancel();
            }

            // Assert

            Assert.Throws<OperationCanceledException>(tokenPair.ThrowIfCancellationRequested);
        }

        #endregion

        #region Register

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Register_ExternalCanceled_FiresCallback(bool hasInternal, bool hasState)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(cts.Token, hasInternal ? cts2.Token : default);

            // Act

            var callbackFired = false;
            if (hasState)
            {
                var expectedState = new object();
                tokenPair.Register(state =>
                {
                    Assert.Same(expectedState, state);
                    callbackFired = true;
                }, expectedState);
            }
            else
            {
                tokenPair.Register(() => callbackFired = true);
            }

            cts.Cancel();

            // Assert

            Assert.True(callbackFired);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Register_InternalCanceled_FiresCallback(bool hasExternal, bool hasState)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            var tokenPair = new CancellationTokenPair(hasExternal ? cts2.Token : default, cts.Token);

            // Act

            var callbackFired = false;
            if (hasState)
            {
                var expectedState = new object();
                tokenPair.Register(state =>
                {
                    Assert.Same(expectedState, state);
                    callbackFired = true;
                }, expectedState);
            }
            else
            {
                tokenPair.Register(() => callbackFired = true);
            }

            cts.Cancel();

            // Assert

            Assert.True(callbackFired);
        }

        #endregion
    }
}
