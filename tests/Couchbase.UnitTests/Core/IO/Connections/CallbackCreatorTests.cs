using System.Net.Security;
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
}
