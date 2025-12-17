using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Transactions.Config;
using Couchbase.Transactions.Tests.UnitTests.Mocks;
using Xunit;

namespace Couchbase.Transactions.Tests.UnitTests
{
    public class AttemptContextTests
    {
        ////[Fact]
        ////public async Task Get_Found_Vs_NotFound()
        ////{
        ////    var mockDocs = new List<TransactionGetResult>()
        ////    {
        ////        new ObjectGetResult("found-id", new object())
        ////    };

        ////    var cluster = MockCollection.CreateMockCluster(mockDocs);
        ////    var ctx = new AttemptContext(new TransactionContext(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, TransactionConfigBuilder.Create().Build(), null));
        ////    var bucket = await cluster.BucketAsync("test-bucket").ConfigureAwait(true);
        ////    var collection = bucket.DefaultCollection();
        ////    var notFoundDoc = await ctx.GetOptionalAsync(collection, "notFound-id").ConfigureAwait(true);
        ////    Assert.Null(notFoundDoc);
        ////    var foundDoc = await ctx.GetOptionalAsync(collection, "found-id").ConfigureAwait(true);
        ////    Assert.NotNull(foundDoc);
        ////    await Assert.ThrowsAsync<DocumentNotFoundException>(async () =>
        ////    {
        ////        var getThrows = await ctx.GetAsync(collection, "notFound2-id").ConfigureAwait(true);
        ////    });

        ////}
    }
}
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
