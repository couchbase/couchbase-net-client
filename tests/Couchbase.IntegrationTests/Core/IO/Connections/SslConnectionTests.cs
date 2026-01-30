using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Test.Common.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests.Core.IO.Connections
{
    public class SslConnectionTests(SslClusterFixture fixture)
        : IClassFixture<SslClusterFixture>
    {
        [CouchbaseCertificateFact(CertFilePath = "required")]
        public async Task ParallelOperations()
        {
                var options = fixture.GetClusterOptions();
                fixture.Log("ParallelOperations: " + fixture.GetCertsFilePath());
                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                var certs = new X509Certificate2Collection();

#if NET5_0_OR_GREATER
                fixture.Log(fixture.GetCertsFilePath());
                certs.ImportFromPemFile(fixture.GetCertsFilePath());
#else
                const string endCert = "-----END CERTIFICATE-----";
                var certFileText = File.ReadAllText(fixture.GetCertsFilePath());
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
#pragma warning disable CS0618 // Type or member is obsolete
                options?.X509CertificateFactory = CertificateFactory.GetCertificatesFromStore(
                    new CertificateStoreSearchCriteria
                    {
                        StoreLocation = StoreLocation.CurrentUser,
                        StoreName = StoreName.My,
                        X509FindType = X509FindType.FindByThumbprint,
                        FindValue = findByValue
                    });
#pragma warning restore CS0618 // Type or member is obsolete

                await using var cluster =
                    await Cluster.ConnectAsync(fixture.GetClusterOptions()?.ConnectionString ?? throw new InvalidOperationException(),
                        options);
                await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));
                await using var bucket = await cluster.BucketAsync(fixture.BucketName);
                var collection = await bucket.DefaultCollectionAsync();
                var key = Guid.NewGuid().ToString();

                try
                {
                    await collection.InsertAsync(key, new { name = "mike" });

                    async Task DoOneHundredGets()
                    {
                        for (var i = 0; i < 100; i++)
                        {
                            using var result = await collection.GetAsync(key);

                            var content = result.ContentAs<dynamic>();

                            Assert.Equal("mike", (string)content.name);
                        }
                    }

                    var parallelTasks = Enumerable.Range(1, 8)
                        .Select(_ => DoOneHundredGets())
                        .ToList();

                    await Task.WhenAll(parallelTasks);
                }
                finally
                {
                    await collection.RemoveAsync(key);
                }
        }

        [CouchbaseCertificateFact(CertFilePath = "required")]
        public async Task MultiCertsTest()
        {
            var options = fixture.GetClusterOptions();
            fixture.Log("MultiCertsTest: " + fixture.GetCertsFilePath());
            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            var certs = new X509Certificate2Collection();

#if NET5_0_OR_GREATER
            fixture.Log(fixture.GetCertsFilePath());
            certs.ImportFromPemFile(fixture.GetCertsFilePath());
#else
                const string endCert = "-----END CERTIFICATE-----";
                var certFileText = File.ReadAllText(fixture.GetCertsFilePath());
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
#pragma warning disable CS0618 // Type or member is obsolete
            options?.X509CertificateFactory = CertificateFactory.GetCertificatesFromStore(
                new CertificateStoreSearchCriteria
                {
                    StoreLocation = StoreLocation.CurrentUser,
                    StoreName = StoreName.My,
                    X509FindType = X509FindType.FindByThumbprint,
                    FindValue = findByValue
                });
#pragma warning restore CS0618 // Type or member is obsolete

            var cluster =
                await Cluster.ConnectAsync(fixture.GetClusterOptions()?.ConnectionString ?? throw new InvalidOperationException(), options);
            await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));
            var bucket = await cluster.BucketAsync(fixture.BucketName);
            var key = Guid.NewGuid().ToString();

            try
            {
                // ReSharper disable once MethodHasAsyncOverload
                await bucket.DefaultCollection().UpsertAsync(key, "Multicerts test data");
                // ReSharper disable once MethodHasAsyncOverload
                var result = await bucket.DefaultCollection().GetAsync(key);

                Assert.Equal("Multicerts test data", (string)result.ContentAs<dynamic>());
            }
            finally
            {
                // ReSharper disable once MethodHasAsyncOverload
                await bucket.DefaultCollection().RemoveAsync(key);
            }

            store.RemoveRange(certs);
            store.Close();
        }
    }
}
