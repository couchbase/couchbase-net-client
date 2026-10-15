using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.HTTP;

/// <summary>
/// Tests that block many thread-pool threads at once. They are in the Stress collection so that they cannot
/// starve other tests of threads; see <see cref="StressCollection"/>.
/// </summary>
[Collection("Stress")]
[Trait("Category", "Stress")]
public class CouchbaseHttpClientFactoryStressTests
{
    [Fact]
    public async Task Create_ConcurrentCallsWithAuthenticatorChange_IsThreadSafe()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithConnectionString("couchbases://localhost");

        var certFactory1 = CouchbaseHttpClientFactoryTests.CreateMockCertificateFactory();
        var certAuth1 = new CertificateAuthenticator(certFactory1.Object);
        clusterOptions.Authenticator = certAuth1;

        using var context = new ClusterContext(null, clusterOptions);

        var factory = new CouchbaseHttpClientFactory(
            context,
            new Mock<ILogger<CouchbaseHttpClientFactory>>().Object,
            new Redactor(RedactionLevel.None),
            CreateCallbackFactory());

        var barrier = new Barrier(10);
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act - Simulate concurrent calls with authenticator changes
        await DedicatedThreads.RunAll(10, taskIndex =>
        {
            try
            {
                barrier.SignalAndWait();

                // Some tasks change the authenticator
                if (taskIndex % 2 == 0)
                {
                    var newCertFactory = CouchbaseHttpClientFactoryTests.CreateMockCertificateFactory();
                    var newCertAuth = new CertificateAuthenticator(newCertFactory.Object);
                    clusterOptions.Authenticator = newCertAuth;
                }

                // All tasks call Create
                using var client = factory.Create();
                Assert.NotNull(client);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert - No exceptions should have been thrown
        Assert.Empty(exceptions);
        Assert.NotNull(factory._sharedHandler);
    }

    private static ICertificateValidationCallbackFactory CreateCallbackFactory()
    {
        var callbackFactory = new Mock<ICertificateValidationCallbackFactory>();
        callbackFactory
            .Setup(x => x.CreateForHttp())
            .Returns((sender, certificate, chain, errors) => true);
        return callbackFactory.Object;
    }
}
