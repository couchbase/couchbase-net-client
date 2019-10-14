using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class ArrayExtensionsTests
    {
        private static readonly Encoding Utf8NoBomEncoding = new UTF8Encoding(false);

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

        [Fact]
        public void GetRandom_Where_Clause()
        {
            var dict = new Dictionary<string, ClusterNode>
            {
                {"127.0.0.1", new ClusterNode(new ClusterContext(null, new ClusterOptions())) {NodesAdapter =  new NodeAdapter(){Views = 8092}}},
                {"127.0.0.2", new ClusterNode(new ClusterContext(null, new ClusterOptions()))  {NodesAdapter =  new NodeAdapter(){Views = 8092}}},
                {"127.0.0.3", new ClusterNode(new ClusterContext(null, new ClusterOptions())) {NodesAdapter =  new NodeAdapter(){Views = 8092}}},
            };

            var node = dict.GetRandom(x => x.Value.HasViews);

            Assert.True(node.Value.HasViews);
        }

        [Fact]
        public void GetRandom_Where_Clause_No_Matches()
        {
            var dict = new Dictionary<string, ClusterNode>
            {
                {"127.0.0.1", new ClusterNode(new ClusterContext(null, new ClusterOptions())) {NodesAdapter =  new NodeAdapter(){Views = 8092}}},
                {"127.0.0.2", new ClusterNode(new ClusterContext(null, new ClusterOptions())) {NodesAdapter =  new NodeAdapter(){Views = 8092}}},
                {"127.0.0.3", new ClusterNode(new ClusterContext(null, new ClusterOptions())) {NodesAdapter =  new NodeAdapter(){Views = 8092}}},
            };

            var node = dict.GetRandom(x => x.Value.HasKv);

            Assert.Null(node.Value);
        }
    }
}
