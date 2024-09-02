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
        public void CanBeCanceled_NoExternalToken_True()
        {
            // Arrange

            using var cts = new CancellationTokenPairSource();

            // Assert

            Assert.True(cts.Token.CanBeCanceled);
            Assert.True(cts.TokenPair.CanBeCanceled);
        }

        [Fact]
        public void CanBeCanceled_ExternalToken_True()
        {
            // Arrange

            using var cts = new CancellationTokenSource();
            using var tokenPair = CancellationTokenPairSource.FromExternalToken(cts.Token);

            // Assert

            Assert.True(tokenPair.Token.CanBeCanceled);
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

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(externalCts.Token);

            // Assert

            Assert.False(cts.IsCancellationRequested);
            Assert.False(cts.TokenPair.IsCancellationRequested);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void IsCancellationRequested_TokensCanceled_True(bool cancelInternal, bool cancelExternal)
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(externalCts.Token);

            if (cancelExternal)
            {
                externalCts.Cancel();
            }

            if (cancelInternal)
            {
                cts.Cancel();
            }

            // Assert

            Assert.True(cts.IsCancellationRequested);
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

        [Fact]
        public void IsExternalCancellation_ExternalCanceled_True()
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(externalCts.Token);
            externalCts.Cancel();

            // Act

            var result = cts.IsExternalCancellation;

            // Assert

            Assert.True(result);
            Assert.True(cts.TokenPair.IsExternalCancellation);
        }

        [Fact]
        public void IsExternalCancellation_ExternalNotCanceled_False()
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(externalCts.Token);
            cts.Cancel();

            // Act

            var result = cts.IsExternalCancellation;

            // Assert

            Assert.False(result);
            Assert.False(cts.TokenPair.IsExternalCancellation);
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

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(hasExternal ? externalCts.Token : default);
            cts.Cancel();

            // Act

            var result = cts.IsInternalCancellation;

            // Assert

            Assert.True(result);
            Assert.True(cts.TokenPair.IsInternalCancellation);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IsInternalCancellation_InternalNotCanceled_False(bool hasExternal)
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(hasExternal ? externalCts.Token : default);
            externalCts.Cancel();

            // Acts

            var result = cts.IsInternalCancellation;

            // Assert

            Assert.False(result);
            Assert.False(cts.TokenPair.IsInternalCancellation);
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

        [Fact]
        public void CanceledToken_ExternalCanceled_ExternalToken()
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(externalCts.Token);
            externalCts.Cancel();

            // Act

            var result = cts.CanceledToken;

            // Assert

            Assert.Equal(externalCts.Token, result);
            Assert.Equal(externalCts.Token, cts.TokenPair.CanceledToken);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CanceledToken_InternalCanceled_InternalToken(bool hasExternal)
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(hasExternal ? externalCts.Token : default);
            cts.Cancel();

            // Act

            var result = cts.CanceledToken;

            // Assert

            Assert.Equal(cts.Token, result);
            Assert.Equal(cts.Token, cts.TokenPair.CanceledToken);
        }

        [Fact]
        public void CanceledToken_NothingCanceled_DefaultToken()
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(externalCts.Token);

            // Act

            var result = cts.CanceledToken;

            // Assert

            Assert.Equal(default, result);
            Assert.Equal(default, cts.TokenPair.CanceledToken);
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

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(externalCts.Token);

            // Assert

            cts.Token.ThrowIfCancellationRequested();
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void ThrowIfCancellationRequested_TokensCanceled_Throws(bool cancelInternal, bool cancelExternal)
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(externalCts.Token);

            if (cancelExternal)
            {
                externalCts.Cancel();
            }

            if (cancelInternal)
            {
                cts.Cancel();
            }

            // Assert

            var ex = Assert.Throws<OperationCanceledException>(cts.TokenPair.ThrowIfCancellationRequested);

            if (cancelExternal)
            {
                // Prefers external
                Assert.Equal(externalCts.Token, ex.CancellationToken);
            }
            else if (cancelInternal)
            {
                Assert.Equal(cts.Token, ex.CancellationToken);
            }
        }

        #endregion

        #region Register

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Register_ExternalCanceled_FiresCallback(bool hasState)
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();

            using var cts = new CancellationTokenPairSource(externalCts.Token);

            // Act

            var callbackFired = false;
            if (hasState)
            {
                var expectedState = new object();
                cts.Token.Register(state =>
                {
                    Assert.Same(expectedState, state);
                    callbackFired = true;
                }, expectedState);
            }
            else
            {
                cts.Token.Register(() => callbackFired = true);
            }

            externalCts.Cancel();

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

            using var externalCts = new CancellationTokenSource();

            using var cts = new CancellationTokenPairSource(hasExternal ? externalCts.Token : default);

            // Act

            var callbackFired = false;
            if (hasState)
            {
                var expectedState = new object();
                cts.Token.Register(state =>
                {
                    Assert.Same(expectedState, state);
                    callbackFired = true;
                }, expectedState);
            }
            else
            {
                cts.Token.Register(() => callbackFired = true);
            }

            cts.Cancel();

            // Assert

            Assert.True(callbackFired);
        }

        #endregion

        #region TryReset

#if NET6_0_OR_GREATER

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TryReset_IsCancelled_ReturnsFalse(bool cancelExternal)
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(externalCts.Token);

            if (cancelExternal)
            {
                externalCts.Cancel();
            }
            else
            {
                cts.Cancel();
            }

            // Act

            var result = cts.TryReset();

            // Assert

            Assert.False(result);
        }

        [Fact]
        public void TryReset_IsNotCancelled_ReturnsTrue()
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(externalCts.Token);

            // Act

            var result = cts.TryReset();

            // Assert

            Assert.True(result);
        }

        [Fact]
        public void TryReset_ExternalCanceledAfterReset_NotCanceled()
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(externalCts.Token);

            // Act

            var result = cts.TryReset();
            externalCts.Cancel();

            // Assert

            Assert.True(result);
            Assert.False(cts.IsCancellationRequested);
        }

        [Fact]
        public async Task TryReset_TimeoutAfterReset_NotCanceled()
        {
            // Arrange

            using var externalCts = new CancellationTokenSource();
            using var cts = new CancellationTokenPairSource(TimeSpan.FromMilliseconds(100), externalCts.Token);

            // Act

            var result = cts.TryReset();
            await Task.Delay(TimeSpan.FromMilliseconds(150));

            // Assert

            Assert.True(result);
            Assert.False(cts.IsCancellationRequested);
        }

#endif

        #endregion

        #region FromTimeout

        [Fact]
        public async Task FromTimeout_TimesOut_CancelsInternal()
        {
            // Arrange

            var tcs = new TaskCompletionSource<bool>();

            // Act

            using var tokenPair = CancellationTokenPairSource.FromTimeout(TimeSpan.FromMilliseconds(50));
            tokenPair.Token.Register(() => tcs.SetResult(true));
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
            tokenPair.Token.Register(() => tcs.SetResult(true));
            await tcs.Task;

            // Assert

            Assert.True(tokenPair.IsExternalCancellation);
        }

        #endregion
    }
}
