using System;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Test.Common;
using Xunit;

namespace Couchbase.Extensions.OpenTelemetry.IntegrationTests
{
    /// <summary>
    /// These tests generate actual OTel tracing and metrics data and require a running OTel collector.
    /// An easy option is to use docker to run the .NET Aspire dashboards:
    ///
    ///   docker run --rm -it \
    ///     -p 18888:18888 \
    ///     -p 4317:18889 -d \
    ///     --name aspire-dashboard \
    ///     mcr.microsoft.com/dotnet/aspire-dashboard:8.0.2
    ///
    /// The dashboard will be available at http://localhost:18888, the authentication token is in the console output:
    ///
    ///   docker logs aspire-dashboard
    /// </summary>
    [Collection(NonParallelDefinition.Name)]
    public class OtelExporterTests(OtelClusterFixture fixture) : IClassFixture<OtelClusterFixture>
    {
        [Fact]
        public async Task Loop1000InsertUpsertRemoveOps()
        {
            var collection = await fixture.GetDefaultCollectionAsync().ConfigureAwait(true);
            var key = Guid.NewGuid().ToString();

            for (var i = 0; i < 1000; i++)
            {
                try
                {
                    await collection.InsertAsync(key, new { name = "mike mike mike mike mike mike mike mike mike mike mike mike mike mike mike" }).ConfigureAwait(true);
                    await collection.UpsertAsync(key, new { name = "john john john john john john john john john john john john john john john" }).ConfigureAwait(true);

                    using (var result = await collection.GetAsync(key).ConfigureAwait(true))
                    {
                        var content = result.ContentAs<dynamic>();

                        Assert.Equal("john john john john john john john john john john john john john john john", (string)content.name);
                    }
                }
                finally
                {
                    try
                    {
                        await collection.RemoveAsync(key).ConfigureAwait(true);
                    }
                    catch (DocumentNotFoundException)
                    {
                    }
                }
            }
        }
    }
}
