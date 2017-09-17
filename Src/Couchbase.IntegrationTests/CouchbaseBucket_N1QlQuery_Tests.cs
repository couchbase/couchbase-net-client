using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Version;
using Couchbase.IntegrationTests.Utils;
using Couchbase.N1QL;
using Couchbase.Views;
using Moq;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    // ReSharper disable once InconsistentNaming
    public class CouchbaseBucket_N1QlQuery_Tests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _cluster = new Cluster(Utils.TestConfiguration.GetCurrentConfiguration());
            _cluster.SetupEnhancedAuth();
            _bucket = _cluster.OpenBucket();
        }

        [Test]
        public void Test_QueryNoDeadlock()
        {
            // Using an asynchronous HttpClient request within an MVC Web API action may cause
            // a deadlock when we wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                _bucket.Query<dynamic>("SELECT * FROM `beer-sample` LIMIT 10");

                // If view queries are incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Test]
        public void Test_QueryAsyncNoDeadlock()
        {
            // Using an asynchronous view query within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                _bucket.QueryAsync<dynamic>("SELECT * FROM `beer-sample` LIMIT 10").Wait();

                // If view queries are incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Test]
        public void Test_ReadYourOwnWrite()
        {
            Assert.IsTrue(_bucket.SupportsEnhancedDurability);

            var doc = new Document<DocumentContent>
            {
                Id = "Test_ReadYourOwnWrite",
                Content = new DocumentContent()
                {
                    Value = new Random().Next(0, 100000)
                }
            };

            var result = _bucket.Upsert(doc);
            Assert.True(result.Success);

            var state = MutationState.From(result.Document);

            var request = new QueryRequest("SELECT d.* FROM default as d WHERE `value` = $1 LIMIT 1")
                .AddPositionalParameter(doc.Content.Value)
                .ConsistentWith(state);

            var queryResult = _bucket.Query<DocumentContent>(request);

            Assert.True(queryResult.Success, queryResult.ToString());
            Assert.IsNotEmpty(queryResult.Rows);
            Assert.AreEqual(doc.Content.Value, queryResult.Rows.First().Value);
        }

        [Test]
        public void Test_ScanWait()
        {
            var doc = new Document<DocumentContent>
            {
                Id = "Test_ReadYourOwnWrite_WaitScan",
                Content = new DocumentContent
                {
                    Value = new Random().Next(0, 100000)
                }
            };

            var result = _bucket.Upsert(doc);
            Assert.True(result.Success);

            var state = MutationState.From(result.Document);

            var request = new QueryRequest("SELECT d.* FROM default as d WHERE `value` = $1 LIMIT 1")
                .AddPositionalParameter(doc.Content.Value)
                .ConsistentWith(state)
                .ScanWait(TimeSpan.FromSeconds(10));

            var queryResult = _bucket.Query<DocumentContent>(request);

            Assert.True(queryResult.Success, queryResult.ToString());
            Assert.IsNotEmpty(queryResult.Rows);
            Assert.AreEqual(doc.Content.Value, queryResult.Rows.First().Value);
        }

        public class DocumentContent
        {
            public int Value { get; set; }
        }

        [Test]
        public void Test_Streaming()
        {
            //arrange
            var request = new QueryRequest("SELECT * FROM `travel-sample` LIMIT 100;").UseStreaming(true);

            //act
            using (var result = _bucket.Query<dynamic>(request))
            {
                //assert
                Assert.IsTrue(typeof(StreamingQueryResult<dynamic>) == result.GetType());
            }
        }

        [Test]
        public void Test_Streaming_Errors()
        {
            //arrange
            var request = new QueryRequest("SELECT * FROM `adfas` LIMIT 100;").UseStreaming(true);

            //act
            using (var result = _bucket.Query<dynamic>(request))
            {
                //assert
                Assert.IsFalse(result.Success);
                Assert.AreNotEqual(QueryStatus.Success, result.Status);
                Assert.IsNotEmpty(result.Errors);
                Assert.IsEmpty(result);
            }
        }

        [Test]
        public void Test_Can_Do_Multiple_Streaming_Requests()
        {
            //arrange
            var request = new QueryRequest("SELECT * FROM `travel-sample` LIMIT 1;").UseStreaming(true);

            //act
            using (var result = _bucket.Query<DocumentContent>(request))
            {
                var rows = result.ToList();
                Assert.AreEqual(1, rows.Count);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(0, result.Errors.Count);
            }
            using (var result = _bucket.Query<DocumentContent>(request))
            {
                var rows = result.ToList();
                Assert.AreEqual(1, rows.Count);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(0, result.Errors.Count);
            }
        }

        [Test]
        public void Test_Streaming_NotAdhoc()
        {
            //arrange
            var request = new QueryRequest("SELECT * FROM `travel-sample` LIMIT 100;")
                .UseStreaming(true)
                .AdHoc(false);

            //act
            using (var result = _bucket.Query<dynamic>(request))
            {
                //assert
                Assert.IsTrue(result.Success);
                Assert.IsTrue(typeof(StreamingQueryResult<dynamic>) == result.GetType());
            }
        }

        [Test]
        public async Task Test_StreamingAsync_NotAdhoc()
        {
            //arrange
            var request = new QueryRequest("SELECT * FROM `travel-sample` LIMIT 100;")
                .UseStreaming(true)
                .AdHoc(false);

            //act
            using (var result = await _bucket.QueryAsync<dynamic>(request))
            {
                //assert
                Assert.IsTrue(result.Success);
                Assert.IsTrue(typeof(StreamingQueryResult<dynamic>) == result.GetType());
            }
        }

        [Test]
        public void Test_Streaming_SelectScalar()
        {
            if (_bucket.GetClusterVersion() < new ClusterVersion(new Version(5, 0, 0)))
            {
                Assert.Ignore("SELECT RAW is not supported on clusters before version 5.0");
            }

            //arrange
            var request = new QueryRequest("SELECT RAW `travel-sample`.`call-sign` FROM `travel-sample` WHERE type = 'airline' LIMIT 100;")
                .UseStreaming(true)
                .AdHoc(false);

            //act
            using (var result = _bucket.Query<string>(request))
            {
                //assert
                Assert.IsTrue(result.Success);
                Assert.IsTrue(typeof(StreamingQueryResult<string>) == result.GetType());
                Assert.IsNotEmpty(result.First());
            }
        }

        [Test(Description = "Simulates creating an index, executing a non-adhoc query so a client side query plan is created, drop the index, " +
                            "execute another query and have the client recognise the index is not longer available so it recreates a query plan. " +
                            "Results can be seen in a HTTP debugger." +
                            "NOTE: it's not currently possible to mock this because the HTTP Client is used directly in QueryClient")]
        public async Task Should_Reprepare_Query_If_Not_Adhoc_And_Receive_IndexNotFound()
        {
            var request = QueryRequest.Create("SELECT META().id FROM `default` WHERE name = $1 OFFSET 0 LIMIT 20;")
                .AddPositionalParameter(new object[] { "Bob" })
                .AdHoc(false);

            const string indexName = "test-index";
            var manager = _bucket.CreateManager(TestConfiguration.Settings.AdminUsername, TestConfiguration.Settings.AdminPassword);

            await manager.CreateN1qlIndexAsync(indexName, false, new [] { "name"});
            await manager.WatchN1qlIndexesAsync(new List<string> {indexName}, TimeSpan.FromSeconds(10));
            var result1 = await _bucket.QueryAsync<dynamic>(request);
            Assert.IsTrue(result1.Success);

            await manager.DropN1qlIndexAsync(indexName);
            var result2 = await _bucket.QueryAsync<dynamic>(request);
            Assert.IsTrue(result2.Success);
        }

        [Test(Description = "https://issues.couchbase.com/browse/NCBC-1350")]
        public void Test_DefaultValueHandlingIgnoreAndPoplate_BadQuery_NoException()
        {
            using (var cluster = new Cluster(TestConfiguration.GetConfiguration("ignoreandpopulate")))
            {
                cluster.SetupEnhancedAuth();
                var bucket = cluster.OpenBucket();
                try
                {

                    var request = new QueryRequest("SELEC * FROM `travel-sample` LIMIT 1");

                    var queryResult = bucket.Query<DocumentContent>(request);

                    Assert.False(queryResult.Success, queryResult.ToString());
                    Assert.IsNull(queryResult.Exception);
                    Assert.IsEmpty(queryResult.Rows);
                }
                finally
                {
                    cluster.CloseBucket(bucket);
                }
            }
        }

        [Test(Description = "https://issues.couchbase.com/browse/NCBC-1447")]
        [TestCase(true)]
        [TestCase(false)]
        public async Task PrettyPrint_AffectsWhitespace(bool pretty)
        {
            string content = null;

            var dataMapper = new Mock<IDataMapper>();
            dataMapper
                .Setup(p => p.Map<QueryResultData<dynamic>>(It.IsAny<Stream>()))
                .Callback((Stream stream) =>
                {
                    using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                    {
                        content = reader.ReadToEnd();
                    }
                });

            var request = new QueryRequest("SELECT META().id FROM `default` WHERE name = $1 LIMIT 2;")
                {
                    DataMapper = dataMapper.Object
                }
                .Pretty(pretty);

            await _bucket.QueryAsync<dynamic>(request);

            Assert.IsNotNull(content);

            // Test to see if content contains whitespace at the beginning of lines
            // Even with pretty=false it will still have line feeds and some spaces
            Assert.AreEqual(pretty, Regex.IsMatch(content, @"^ ", RegexOptions.Multiline));
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.CloseBucket(_bucket);
            _cluster.Dispose();
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
