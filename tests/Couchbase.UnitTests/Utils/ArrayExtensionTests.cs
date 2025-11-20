using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Core;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class ArrayExtensionsTests
    {
        private static readonly Encoding Utf8NoBomEncoding = new UTF8Encoding(false);

        #region Shuffle

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(20)]
        public void Shuffle_AnyLength_Shuffles(int length)
        {
            // Arrange

            var list = Enumerable.Range(0, length).ToList();

            // Act

            list = list.Shuffle();

            // Assert

            Assert.NotNull(list);
            Assert.Equal(length, list.Count);
        }

        #endregion

        #region IsJson

        [Theory]
        [InlineData("", 0, 0, false)]
        [InlineData("a", 0, 1, false)]
        [InlineData("abc", 0, 3, false)]
        [InlineData("[]", 0, 2, true)]
        [InlineData("{}", 0, 2, true)]
        [InlineData("xx{\"a\":1}yy", 2, 7, true)]
        [InlineData("xx[\"abc\"]yy", 2, 7, true)]
        public void IsJson_ExpectedResult(string value, int offset, int length, bool expectedResult)
        {
            // Arrange

            var bytes = Utf8NoBomEncoding.GetBytes(value);

            // Act

            var result = bytes.AsSpan(offset, length).IsJson();

            // Assert

            Assert.Equal(expectedResult, result);
        }

        #endregion

        #region RandomOrDefault

        [Fact]
        public void RandomOrDefault_Success()
        {
            var dict = new Dictionary<string, ClusterNode>
            {
                {"127.0.0.1", MakeFakeClusterNode("127.0.0.1") },
                {"127.0.0.2", MakeFakeClusterNode("127.0.0.2") },
                {"127.0.0.3", MakeFakeClusterNode("127.0.0.3") }
            };

            var node = dict.RandomOrDefault();

            Assert.NotNull(node.Value);
        }

        [Fact]
        public void RandomOrDefault_Where_Clause()
        {
            var dict = new Dictionary<string, ClusterNode>
            {
                {"127.0.0.1", MakeFakeClusterNode("127.0.0.1") },
                {"127.0.0.2", MakeFakeClusterNode("127.0.0.2") },
                {"127.0.0.3", MakeFakeClusterNode("127.0.0.3") }
            };

            var node = dict.RandomOrDefault(x => x.Value.HasViews);

            Assert.True(node.Value.HasViews);
        }

        [Fact]
        public void RandomOrDefault_Where_Clause_No_Matches()
        {
            var dict = new Dictionary<string, ClusterNode>
            {
                {"127.0.0.1", MakeFakeClusterNode("127.0.0.1") },
                {"127.0.0.2", MakeFakeClusterNode("127.0.0.2") },
                {"127.0.0.3", MakeFakeClusterNode("127.0.0.3") }
            };

            var node = dict.RandomOrDefault(x => x.Value.HasAnalytics);

            Assert.Null(node.Value);
        }

        [Fact]
        public void RandomOrDefault_Where_Distribution()
        {
            // select half of 6 nodes
            RandomDistribution( 6, dict =>
                dict.RandomOrDefault(x => String.Compare(x.Value.NodesAdapter.Hostname,(dict.Count/2).ToString("D3")) <0 ));
        }

        [Fact]
        public void RandomOrDefault_Distribution()
        {
            // all of 3 nodes
            RandomDistribution(3, dict => dict.RandomOrDefault());
        }

        private void RandomDistribution(int numNodes,  Func<Dictionary<string,ClusterNode>, KeyValuePair<string,ClusterNode>> func){
        IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder
                .AddFilter(level => level >= LogLevel.Debug)
            );
            var loggerFactory = ServiceProviderServiceExtensions.GetService<ILoggerFactory>(serviceCollection.BuildServiceProvider());
            loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);
            var logger = loggerFactory.CreateLogger<ArrayExtensionsTests>();

            // conduct 100 tests, each test will produce hits on 3 nodes,
            // use a sample size of 1000 for each test
            // We collect pass/fail of 0.05 confidence (5%) the chi-squared 3-1=2 df chi-squared test.
            // Of the 100 tess, if we find they are failing at 20% or more, we fail the randomness.
            // I conducted 10,000 runs, there were none with with failures of 20%.
            var nTests = 100;
            var sampleSize = 1000;

            var dict = new Dictionary<string, ClusterNode> { };
            for (int i = 0; i < numNodes; i++)
            {
                // hostnames are 000, 001, 002, ...
                var node = MakeFakeClusterNode( i.ToString("D3"));
                dict[node.NodesAdapter.Hostname] = node;
            }

            var fails = 0;
            for (int t = 0; t < nTests; t++)
            {
                var histo = new Dictionary<string, Int32> { };
                for (int i = 0; i < sampleSize; i++)
                {
                    var kvPair = func(dict); //dict.RandomOrDefault(x => x.Value.HasViews);
                    int count;
                    histo.TryGetValue(kvPair.Key, out count);
                    histo[kvPair.Key] = count + 1;
                }

                if (histo.Count != 3)
                {
                    throw new Exception("test should have three (filtered) nodes with hits on each of them. nodes: "+numNodes+", hits: "+histo.Count);
                }

                var chi = Chi2FromFreqs(histo.Values.ToArray(), sampleSize / histo.Count);
                // degrees of freedom = (histo.Count - 1)
                // df = 2, probability 0.95/0.05 -> 5.991
                // https://www.itl.nist.gov/div898/handbook/eda/section3/eda3674.htm
                logger.LogTrace("chi2: " + chi);
                if (chi > 5.991)
                {
                    ++fails;
                }
            }

            logger.LogDebug("fails/nTests: " + (float)fails / nTests);
            if ((float)fails / nTests > 0.2) // 0.05 fails are expected, 0.20 is not.
            {
                throw new Exception("random failed random test " + ((float)fails / nTests));
            }
        }

        #endregion

        #region AreEqual_T

        [Fact]
        public void AreEqual_T_UnequalSizes_ReturnsFalse()
        {
            int[] a = [1, 2];
            int[] b = [1, 2, 3];

            Assert.False(a.AreEqual(b));
        }

        [Fact]
        public void AreEqual_T_NullAB_ReturnsTrue()
        {
            int[] a = null;
            int[] b = null;

            Assert.True(a.AreEqual(b));
        }

        [Fact]
        public void AreEqual_T_NullA_ReturnsFalse()
        {
            int[] a = null;
            int[] b = [1, 2];

            Assert.False(a.AreEqual(b));
        }

        [Fact]
        public void AreEqual_T_NullB_ReturnsFalse()
        {
            int[] a = [1, 2];
            int[] b = null;

            Assert.False(a.AreEqual(b));
        }

        [Fact]
        public void AreEqual_Int32_NotEqual()
        {
            int[] a = [1, 2];
            int[] b = [1, 3];

            Assert.False(a.AreEqual(b));
        }

        [Fact]
        public void AreEqual_String_NotEqual()
        {
            string[] a = ["one", "two"];
            string[] b = ["one", "three"];

            Assert.False(a.AreEqual(b));
        }

        [Fact]
        public void AreEqual_Int32_Equal()
        {
            int[] a = [1, 2];
            int[] b = [1, 2];

            Assert.True(a.AreEqual(b));
        }

        [Fact]
        public void AreEqual_String_Equal()
        {
            string[] a = ["one", "two"];
            string[] b = ["one", "two"];

            Assert.True(a.AreEqual(b));
        }

        #endregion

        #region AreEqual_ShortArray

        [Fact]
        public void AreEqual_ShortArray_UnequalSizesScenario1_ReturnsFalse()
        {
            short[][] a = [[1, 2], null];
            short[][] b = [[1, 2]];

            Assert.False(a.AreEqual(b));
        }

        [Fact]
        public void AreEqual_ShortArray_UnequalSizesScenario2_ReturnsFalse()
        {
            short[][] a = [[1, 2], null];
            short[][] b = [[1, 2, 3], null];

            Assert.False(a.AreEqual(b));
        }

        [Fact]
        public void AreEqual_ShortArray_UnequalSizesScenario3_ReturnsFalse()
        {
            short[][] a = [[1], [2]];
            short[][] b = [[1], [2, 3]];

            Assert.False(a.AreEqual(b));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void AreEqual_ShortArray_NullElement_ReturnsFalse(int nullElementIndex)
        {
            var a = new short[2][];
            a[nullElementIndex] = [1, 2];
            var b = new short[2][];
            b[1 - nullElementIndex] = [1, 2];

            Assert.False(a.AreEqual(b));
        }

        [Fact]
        public void AreEqual_ShortArray_Equal()
        {
            short[][] a = [[1, 2], null];
            short[][] b = [[1, 2], null];

            Assert.True(a.AreEqual(b));
        }

        #endregion

        #region Helpers

        private ClusterNode MakeFakeClusterNode(string hostname)
        {
            return new ClusterNode(
                new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password")),
                new Mock<IConnectionPoolFactory>().Object,
                new Mock<ILogger<ClusterNode>>().Object,
                new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new Mock<ICircuitBreaker>().Object,
                new Mock<ISaslMechanismFactory>().Object,
                new TypedRedactor(RedactionLevel.None),
                new HostEndpointWithPort(hostname, 11210),
                new NodeAdapter
                {
                    Hostname = hostname,
                    Views = 8091
                },
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object);
        }

        #endregion

        public static double Chi2FromFreqs(int[] observed, double expected)
        {
            double sum = 0.0;
            for (int i = 0; i < observed.Length; ++i)
            {
                sum += ((observed[i] - expected) *
                        (observed[i] - expected)) / expected;
            }
            return sum;
        }
    }
}
