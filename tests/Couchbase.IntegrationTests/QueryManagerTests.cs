using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Management.Query;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class QueryManagerTests
    {
        [Fact]
        public async Task Test()
        {
            var cluster = await Cluster.ConnectAsync(
                "couchbase://localhost",
                "Administrator",
                "password");

            var manager = cluster.QueryIndexes;
            await manager.CreatePrimaryIndexAsync("default",
                new CreatePrimaryQueryIndexOptions()
                    .Deferred(false)
                    .IgnoreIfExists(true));

            Console.WriteLine("No error yet...");
          //  Console.ReadLine();

            await manager.CreatePrimaryIndexAsync("default",
                new CreatePrimaryQueryIndexOptions()
                    .Deferred(false)
                    .IgnoreIfExists(true));

            await cluster.DisposeAsync();
        }
    }
}
