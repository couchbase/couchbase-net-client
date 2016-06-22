using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class MutationTokenTests
    {
        [Test]
        public void BucketName_IsCurrentBucket()
        {
            var expectedBucketName = "travel-sample";
            var bucket = ClusterHelper.GetBucket(expectedBucketName);
            Assert.IsTrue(bucket.SupportsEnhancedDurability);

            var doc = new Document<dynamic>
            {
                Id = "MutationTokenTests#BucketName_IsCurrentBucket",
                Content = new
                {
                    Name="foo",
                    Age = 22
                }
            };

            var result = bucket.Upsert(doc);
            Assert.AreEqual(expectedBucketName, result.Document.Token.BucketRef);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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

#endregion
