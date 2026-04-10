using System;
using System.Net.Http;
using System.Net.Sockets;
using Couchbase.Core.IO.HTTP;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.HTTP;

public class HttpRequestExceptionExtensionsTests
{
    [Theory]
    [InlineData(SocketError.ConnectionRefused)]
    [InlineData(SocketError.HostNotFound)]
    [InlineData(SocketError.HostUnreachable)]
    [InlineData(SocketError.NetworkUnreachable)]
    [InlineData(SocketError.AddressNotAvailable)]
    [InlineData(SocketError.NetworkDown)]
    public void Returns_True_For_PreConnect_SocketErrors_Regardless_Of_Idempotency(SocketError code)
    {
        var ex = new HttpRequestException("transport", new SocketException((int)code));

        Assert.True(ex.IsRetriableTransportError(isReadOnly: true));
        Assert.True(ex.IsRetriableTransportError(isReadOnly: false));
    }

    [Theory]
    [InlineData(SocketError.ConnectionReset)]
    [InlineData(SocketError.ConnectionAborted)]
    [InlineData(SocketError.TimedOut)]
    public void Ambiguous_SocketError_Only_Retries_When_ReadOnly(SocketError code)
    {
        var ex = new HttpRequestException("transport", new SocketException((int)code));

        Assert.True(ex.IsRetriableTransportError(isReadOnly: true));
        Assert.False(ex.IsRetriableTransportError(isReadOnly: false));
    }

    [Fact]
    public void No_Inner_SocketException_Only_Retries_When_ReadOnly()
    {
        var ex = new HttpRequestException("just an http error");

        Assert.True(ex.IsRetriableTransportError(isReadOnly: true));
        Assert.False(ex.IsRetriableTransportError(isReadOnly: false));
    }

    [Fact]
    public void Walks_Inner_Exception_Chain_To_Find_SocketException()
    {
        // SocketException nested two levels deep behind generic Exceptions
        var socketEx = new SocketException((int)SocketError.ConnectionRefused);
        var middle = new InvalidOperationException("middle", socketEx);
        var ex = new HttpRequestException("outer", middle);

        // Pre-connect error → retry even for mutations
        Assert.True(ex.IsRetriableTransportError(isReadOnly: false));
    }

#if NET8_0_OR_GREATER
    [Theory]
    [InlineData(HttpRequestError.NameResolutionError)]
    [InlineData(HttpRequestError.ConnectionError)]
    [InlineData(HttpRequestError.SecureConnectionError)]
    public void HttpRequestError_PreConnect_Categories_Always_Retry(HttpRequestError httpRequestError)
    {
        var ex = new HttpRequestException(httpRequestError, "transport");

        Assert.True(ex.IsRetriableTransportError(isReadOnly: true));
        Assert.True(ex.IsRetriableTransportError(isReadOnly: false));
    }

    [Fact]
    public void HttpRequestError_Unknown_Without_SocketException_Only_Retries_When_ReadOnly()
    {
        // HttpRequestError.Unknown is the default — should fall through to socket-error
        // inspection, find nothing, and return isReadOnly.
        var ex = new HttpRequestException(HttpRequestError.Unknown, "transport");

        Assert.True(ex.IsRetriableTransportError(isReadOnly: true));
        Assert.False(ex.IsRetriableTransportError(isReadOnly: false));
    }
#endif
}
