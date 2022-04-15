using Couchbase.Management.Query;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Management.Query
{
    public class QueryGeneratorTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public QueryGeneratorTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Theory]
        [InlineData("default", "index1", new[] {"field1", "field2"}, "_default", "_default", "CREATE INDEX `index1` ON `default`.`_default`.`_default`(field1,field2) USING GSI WITH {\"defer_build\":False};")]
        [InlineData("default", "index1", new[] { "field1", "field2" }, null, null, "CREATE INDEX `index1` ON `default`(field1,field2) USING GSI WITH {\"defer_build\":False};")]
        public void Test_CreateIndexStatement(string bucketName, string indexName, IEnumerable<string> fields, string scopeName, string collectionName, string expected)
        {
            //arrange
            var options = new CreateQueryIndexOptions().ScopeName(scopeName).CollectionName(collectionName);

            //act
            var statement = QueryGenerator.CreateIndexStatement(bucketName, indexName, fields, options);

            _outputHelper.WriteLine(statement);

            //assert
            Assert.Equal(expected, statement);
        }

        [Theory]
        [InlineData("default", "index1", "_default", "_default", "BUILD INDEX ON `default`.`_default`.`_default`(index1) USING GSI;")]
        [InlineData("default", "index1", null, null, "BUILD INDEX ON `default`(index1) USING GSI;")]
        [InlineData("`default`", "index1", null, null, "BUILD INDEX ON `default`(index1) USING GSI;")]
        [InlineData("`default", "index1", null, null, "BUILD INDEX ON `default`(index1) USING GSI;")]
        [InlineData("default`", "index1", null, null, "BUILD INDEX ON `default`(index1) USING GSI;")]
        public void Test_CreateDeferredIndexStatement(string bucketName, string indexName, string scopeName, string collectionName, string expected)
        {
            //arrange
            var options = new BuildDeferredQueryIndexOptions().ScopeName(scopeName).CollectionName(collectionName);

            //act
            var statement = QueryGenerator.CreateDeferredIndexStatement(bucketName, indexName, options);

            _outputHelper.WriteLine(statement);

            //assert
            Assert.Equal(expected, statement);
        }


        [Theory]
        [InlineData("default", "_default", "_default", "CREATE PRIMARY INDEX ON `default`.`_default`.`_default` USING GSI WITH {\"defer_build\":False};")]
        [InlineData("default", null, null, "CREATE PRIMARY INDEX ON `default` USING GSI WITH {\"defer_build\":False};")]
        public void Test_CreatePrimaryIndexStatement(string bucketName, string scopeName, string collectionName, string expected)
        {
            //arrange
            var options = new CreatePrimaryQueryIndexOptions().ScopeName(scopeName).CollectionName(collectionName);

            //act
            var statement = QueryGenerator.CreatePrimaryIndexStatement(bucketName, options);

            _outputHelper.WriteLine(statement);

            //assert
            Assert.Equal(expected, statement);
        }

        [Theory]
        [InlineData("default", "index1", "_default", "_default", "DROP INDEX `index1` ON `default`.`_default`.`_default` USING GSI;")]
        [InlineData("default", "index1", null, null, "DROP INDEX `default`.`index1` USING GSI;")]
        public void Test_CreateDropIndexStatement(string bucketName, string indexName, string scopeName, string collectionName, string expected)
        {
            //arrange
            var options = new DropQueryIndexOptions().ScopeName(scopeName).CollectionName(collectionName);

            //act
            var statement = QueryGenerator.CreateDropIndexStatement(bucketName, indexName, options);

            _outputHelper.WriteLine(statement);

            //assert
            Assert.Equal(expected, statement);
        }

        [Theory]
        [InlineData("default", "_default", "_default", "DROP PRIMARY INDEX ON `default`.`_default`.`_default` USING GSI;")]
        [InlineData("default", null, null, "DROP PRIMARY INDEX ON `default` USING GSI;")]
        public void Test_CreateDropPrimaryIndexStatement(string bucketName, string scopeName, string collectionName, string expected)
        {
            //arrange
            var options = new DropPrimaryQueryIndexOptions().ScopeName(scopeName).CollectionName(collectionName);

            //act
            var statement = QueryGenerator.CreateDropPrimaryIndexStatement(bucketName, options);

            _outputHelper.WriteLine(statement);

            //assert
            Assert.Equal(expected, statement);
        }

        [Theory]
        [InlineData("_default", "_default", "SELECT idx.* FROM system:indexes AS idx WHERE ((((bucket_id = $bucketName) AND scope_id = $scopeName) AND keyspace_id = $collectionName) OR (bucket_id IS MISSING AND keyspace_id = $bucketName)) AND `using` = \"gsi\" ORDER BY is_primary DESC, name ASC")]
        [InlineData("_default", null, "SELECT idx.* FROM system:indexes AS idx WHERE (((bucket_id = $bucketName) AND scope_id = $scopeName) OR (bucket_id IS MISSING AND keyspace_id = $bucketName)) AND `using` = \"gsi\" ORDER BY is_primary DESC, name ASC")]
        [InlineData(null, null, "SELECT idx.* FROM system:indexes AS idx WHERE ((bucket_id = $bucketName) OR (bucket_id IS MISSING AND keyspace_id = $bucketName)) AND `using` = \"gsi\" ORDER BY is_primary DESC, name ASC")]
        public void Test_CreateGetAllIndexesStatement(string scopeName, string collectionName, string expected)
        {
            //arrange
            var options = new GetAllQueryIndexOptions().ScopeName(scopeName).CollectionName(collectionName);

            //act
            var statement = QueryGenerator.CreateGetAllIndexesStatement(options);

            _outputHelper.WriteLine(statement);

            //assert
            Assert.Equal(expected, statement);
        }
    }
}
