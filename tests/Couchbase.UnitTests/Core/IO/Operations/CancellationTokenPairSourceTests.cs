using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    public class CancellationTokenPairSourceTests
    {
        #region CanBeCanceled

        [Fact]
        public void CanBeCanceled_DefaultTokenPair_False()
        {
            // Arrange

            var tokenPair = default(CancellationTokenPair);

            // Assert

            Assert.False(tokenPair.CanBeCanceled);
        }

        [Fact]
        public void CanBeCanceled_DefaultTokens_False()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var tokenPair = new CancellationTokenPairSource(default, default);

            // Assert

            Assert.False(tokenPair.CanBeCanceled);
            Assert.False(tokenPair.TokenPair.CanBeCanceled);
        }

        [Fact]
        public void CanBeCanceled_ExternalToken_True()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var tokenPair = CancellationTokenPairSource.FromExternalToken(cts.Token);

            // Assert

            Assert.True(tokenPair.CanBeCanceled);
            Assert.True(tokenPair.TokenPair.CanBeCanceled);
        }

        [Fact]
        public void CanBeCanceled_InternalToken_True()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var tokenPair = CancellationTokenPairSource.FromInternalToken(cts.Token);

            // Assert

            Assert.True(tokenPair.CanBeCanceled);
            Assert.True(tokenPair.TokenPair.CanBeCanceled);
        }

        [Fact]
        public void CanBeCanceled_SameToken_True()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var tokenPair = new CancellationTokenPairSource(cts.Token, cts.Token);

            // Assert

            Assert.True(tokenPair.CanBeCanceled);
            Assert.True(tokenPair.TokenPair.CanBeCanceled);
        }

        [Fact]
        public void CanBeCanceled_TwoTokens_True()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();
            using var tokenPair = new CancellationTokenPairSource(cts.Token, cts2.Token);

            // Assert

            Assert.True(tokenPair.CanBeCanceled);
            Assert.True(tokenPair.TokenPair.CanBeCanceled);
        }

        #endregion

        #region IsCancellationRequested

        [Fact]
        public void IsCancellationRequested_DefaultTokenPair_False()
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

            using var tokenPair = new CancellationTokenPairSource(cts.Token, cts2.Token);

            // Assert

            Assert.False(tokenPair.IsCancellationRequested);
            Assert.False(tokenPair.TokenPair.IsCancellationRequested);
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

            using var tokenPair = new CancellationTokenPairSource(cts.Token, cts2.Token);

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
            Assert.True(tokenPair.TokenPair.IsCancellationRequested);
        }

        #endregion

        #region IsExternalCancellation

        [Fact]
        public void IsExternalCancellation_DefaultTokenPair_False()
        {
            // Arrange

            var tokenPair = default(CancellationTokenPair);

            // Assert

            Assert.False(tokenPair.IsExternalCancellation);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsExternalCancellation_ExternalCanceled_True(bool hasInternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            using var tokenPair = new CancellationTokenPairSource(cts.Token, hasInternal ? cts2.Token : default);
            cts.Cancel();

            // Act

            var result = tokenPair.IsExternalCancellation;

            // Assert

            Assert.True(result);
            Assert.True(tokenPair.TokenPair.IsExternalCancellation);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsExternalCancellation_ExternalNotCanceled_False(bool hasInternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            using var tokenPair = new CancellationTokenPairSource(cts.Token, hasInternal ? cts2.Token : default);
            cts2.Cancel();

            // Act

            var result = tokenPair.IsExternalCancellation;

            // Assert

            Assert.False(result);
            Assert.False(tokenPair.TokenPair.IsExternalCancellation);
        }

        #endregion

        #region IsInternalCancellation

        [Fact]
        public void IsInternalCancellation_DefaultTokenPair_False()
        {
            // Arrange

            var tokenPair = default(CancellationTokenPair);

            // Assert

            Assert.False(tokenPair.IsInternalCancellation);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsInternalCancellation_InternalCanceled_True(bool hasExternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            using var tokenPair = new CancellationTokenPairSource(hasExternal ? cts2.Token : default, cts.Token);
            cts.Cancel();

            // Act

            var result = tokenPair.IsInternalCancellation;

            // Assert

            Assert.True(result);
            Assert.True(tokenPair.TokenPair.IsInternalCancellation);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsInternalCancellation_InternalNotCanceled_False(bool hasExternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            using var tokenPair = new CancellationTokenPairSource(hasExternal ? cts2.Token : default, cts.Token);
            cts2.Cancel();

            // Act

            var result = tokenPair.IsInternalCancellation;

            // Assert

            Assert.False(result);
            Assert.False(tokenPair.TokenPair.IsInternalCancellation);
        }

        #endregion

        #region CanceledToken

        [Fact]
        public void CanceledToken_DefaultTokenPair_DefaultToken()
        {
            // Arrange

            var tokenPair = default(CancellationTokenPair);

            // Assert

            Assert.Equal(default, tokenPair.CanceledToken);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CanceledToken_ExternalCanceled_ExternalToken(bool hasInternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            using var tokenPair = new CancellationTokenPairSource(cts.Token, hasInternal ? cts2.Token : default);
            cts.Cancel();

            // Act

            var result = tokenPair.CanceledToken;

            // Assert

            Assert.Equal(cts.Token, result);
            Assert.Equal(cts.Token, tokenPair.TokenPair.CanceledToken);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CanceledToken_InternalCanceled_InternalToken(bool hasExternal)
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            using var tokenPair = new CancellationTokenPairSource(hasExternal ? cts2.Token : default, cts.Token);
            cts.Cancel();

            // Act

            var result = tokenPair.CanceledToken;

            // Assert

            Assert.Equal(cts.Token, result);
            Assert.Equal(cts.Token, tokenPair.TokenPair.CanceledToken);
        }

        [Fact]
        public void CanceledToken_NothingCanceled_DefaultToken()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();

            using var tokenPair = new CancellationTokenPairSource(cts.Token, cts2.Token);

            // Act

            var result = tokenPair.CanceledToken;

            // Assert

            Assert.Equal(default, result);
            Assert.Equal(default, tokenPair.TokenPair.CanceledToken);
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

            using var tokenPair = new CancellationTokenPairSource(cts.Token, cts2.Token);

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

            using var tokenPair = new CancellationTokenPairSource(cts.Token, cts2.Token);

            if (cancelExternal)
            {
                cts.Cancel();
            }

            if (cancelInternal)
            {
                cts2.Cancel();
            }

            // Assert

            var ex = Assert.Throws<OperationCanceledException>(tokenPair.ThrowIfCancellationRequested);

            if (cancelExternal)
            {
                // Prefers external
                Assert.Equal(cts.Token, ex.CancellationToken);
            }
            else if (cancelInternal)
            {
                Assert.Equal(cts2.Token, ex.CancellationToken);
            }
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

            using var tokenPair = new CancellationTokenPairSource(cts.Token, hasInternal ? cts2.Token : default);

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

            using var tokenPair = new CancellationTokenPairSource(hasExternal ? cts2.Token : default, cts.Token);

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

        #region FromTimeout

        [Fact]
        public async Task FromTimeout_TimesOut_CancelsInternal()
        {
            // Arrange

            var tcs = new TaskCompletionSource<bool>();

            // Act

            using var tokenPair = CancellationTokenPairSource.FromTimeout(TimeSpan.FromMilliseconds(50));
            tokenPair.Register(() => tcs.SetResult(true));
            await tcs.Task;

            // Assert

            Assert.True(tokenPair.IsInternalCancellation);
        }

        [Fact]
        public async Task FromTimeout_ExternalCancellation_CancelsExternal()
        {
            // Arrange

            var tcs = new TaskCompletionSource<bool>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act

            using var tokenPair = CancellationTokenPairSource.FromTimeout(TimeSpan.FromMilliseconds(50), cts.Token);
            tokenPair.Register(() => tcs.SetResult(true));
            await tcs.Task;

            // Assert

            Assert.True(tokenPair.IsExternalCancellation);
        }

        #endregion
    }
}
