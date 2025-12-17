using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Xunit;

namespace Couchbase.IntegrationTests.Core.IO.Connections
{
    public class SslConnectionTests : IClassFixture<SslClusterFixture>
    {
        private readonly SslClusterFixture _fixture;

        public SslConnectionTests(SslClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [CouchbaseCertificateFact(CertFilePath = "required")]
        public async Task ParallelOperations()
        {
                ClusterOptions options = _fixture.GetClusterOptions();
                _fixture.Log("ParallelOperations: " + _fixture.GetCertsFilePath());
                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                var certs = new X509Certificate2Collection();

#if NET5_0_OR_GREATER
                _fixture.Log(_fixture.GetCertsFilePath());
                certs.ImportFromPemFile(_fixture.GetCertsFilePath());
#else
                const string endCert = "-----END CERTIFICATE-----";
                var certFileText = File.ReadAllText(_fixture.GetCertsFilePath());
                var certStrings =
 certFileText.Split(new[] {endCert}, StringSplitOptions.RemoveEmptyEntries);
                foreach (var certString in certStrings)
                {
                    if (certString.Contains("BEGIN CERTIFICATE"))
                    {
                        var bytes = Encoding.Default.GetBytes(certString + endCert);
                        var cert = new X509Certificate2(bytes);
                        certs.Add(cert);
                    }
                }
#endif
                store.AddRange(certs);
                var findByValue = certs[0].Thumbprint;
                // Find certificate in local store
                options.X509CertificateFactory = CertificateFactory.GetCertificatesFromStore(
                    new CertificateStoreSearchCriteria()
                    {
                        StoreLocation = StoreLocation.CurrentUser,
                        StoreName = StoreName.My,
                        X509FindType = X509FindType.FindByThumbprint,
                        FindValue = findByValue
                    });

                var cluster =
                    await Cluster.ConnectAsync(_fixture.GetClusterOptions().ConnectionString,
                        options);
                await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));
                var bucket = await cluster.BucketAsync(_fixture.BucketName);
                var collection = await bucket.DefaultCollectionAsync();
                var key = Guid.NewGuid().ToString();

                try
                {
                    await collection.InsertAsync(key, new { name = "mike" }).ConfigureAwait(true);

                    async Task DoOneHundredGets()
                    {
                        for (var i = 0; i < 100; i++)
                        {
                            using var result = await collection.GetAsync(key).ConfigureAwait(true);

                            var content = result.ContentAs<dynamic>();

                            Assert.Equal("mike", (string)content.name);
                        }
                    }

                    var parallelTasks = Enumerable.Range(1, 8)
                        .Select(_ => DoOneHundredGets())
                        .ToList();

                    await Task.WhenAll(parallelTasks).ConfigureAwait(true);
                }
                finally
                {
                    await collection.RemoveAsync(key).ConfigureAwait(true);
                }
        }

        [CouchbaseCertificateFact(CertFilePath = "required")]
        public async Task MultiCertsTest()
        {
            ClusterOptions options = _fixture.GetClusterOptions();
            _fixture.Log("MultiCertsTest: " + _fixture.GetCertsFilePath());
            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            var certs = new X509Certificate2Collection();

#if NET5_0_OR_GREATER
            _fixture.Log(_fixture.GetCertsFilePath());
            certs.ImportFromPemFile(_fixture.GetCertsFilePath());
#else
                const string endCert = "-----END CERTIFICATE-----";
                var certFileText = File.ReadAllText(_fixture.GetCertsFilePath());
                var certStrings =
 certFileText.Split(new[] {endCert}, StringSplitOptions.RemoveEmptyEntries);
                foreach (var certString in certStrings)
                {
                    if (certString.Contains("BEGIN CERTIFICATE"))
                    {
                        var bytes = Encoding.Default.GetBytes(certString + endCert);
                        var cert = new X509Certificate2(bytes);
                        certs.Add(cert);
                    }
                }
#endif
            store.AddRange(certs);
            var findByValue = certs[0].Thumbprint;
            // Find certificate in local store
            options.X509CertificateFactory = CertificateFactory.GetCertificatesFromStore(
                new CertificateStoreSearchCriteria()
                {
                    StoreLocation = StoreLocation.CurrentUser,
                    StoreName = StoreName.My,
                    X509FindType = X509FindType.FindByThumbprint,
                    FindValue = findByValue
                });

            var cluster =
                await Cluster.ConnectAsync(_fixture.GetClusterOptions().ConnectionString, options);
            await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));
            var bucket = await cluster.BucketAsync(_fixture.BucketName);
            var key = Guid.NewGuid().ToString();

            try
            {
                await bucket.DefaultCollection().UpsertAsync(key, "Multicerts test data");
                var result = await bucket.DefaultCollection().GetAsync(key);

                Assert.Equal("Multicerts test data", (string)result.ContentAs<dynamic>());
            }
            finally
            {
                await bucket.DefaultCollection().RemoveAsync(key);
            }

            store.RemoveRange(certs);
            store.Close();
        }
    }
}
