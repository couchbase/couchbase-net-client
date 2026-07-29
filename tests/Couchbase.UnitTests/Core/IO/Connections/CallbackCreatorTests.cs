using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Connections;

public class CallbackCreatorTests
{
    private static CallbackCreator CreateSubject(bool ignoreNameMismatch = false) =>
        new CallbackCreator(
            ignoreNameMismatch,
            NullLogger<object>.Instance,
            new Mock<IRedactor>().Object,
            certs: null);

    [Fact]
    public void Callback_NoCertificatePresented_ReturnsFalse()
    {
        var subject = CreateSubject();

        var accepted = subject.Callback(
            sender: new object(),
            certificate: null,
            chain: null,
            SslPolicyErrors.RemoteCertificateNotAvailable);

        Assert.False(accepted);
    }

    [Fact]
    public void Callback_NoChainPresented_ReturnsFalse()
    {
        var subject = CreateSubject();
        using var cert = new X509Certificate2();

        var accepted = subject.Callback(
            sender: new object(),
            certificate: cert,
            chain: null,
            SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.False(accepted);
    }
}
