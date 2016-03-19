using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.IO;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class CouchbaseBucket_SubDocument_Tests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        public void Setup(bool useMutation)
        {
            var config = Utils.TestConfiguration.GetCurrentConfiguration();
            config.BucketConfigs.First().Value.UseEnhancedDurability = useMutation;
            _cluster = new Cluster(config);
            _bucket = _cluster.OpenBucket();
        }

#region Retrieval Commands

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_MultiCommands_ReturnsCorrectCount(bool useMutation)
        {
            Setup(useMutation);

            var key = "LookupIn_MultiCommands_ReturnsCorrectCount";
            _bucket.Upsert(key, new {foo = "bar", bar="foo"});

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo").Get("bar");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(2, result.Value.Count);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_PathExists_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            Setup(useMutation);
            var key = "LookupIn_Get_PathExists_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_MultiExists_PathExists_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupIn_MultiExists_PathExists_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Exists("foo").Exists("bar");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_MultiExists_PathExists_ReturnsSubDocMultiPathFailure(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupIn_MultiExists_PathExists_ReturnsSubDocMultiPathFailure";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Exists("foo").Exists("car");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_MissingPath_ReturnsSubDocPathNotFound(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupIn_MultiCommands_ReturnsSubDocPathNotFound";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("boo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus("boo"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_MultiGet_MissingPath_ReturnsSubDocPathNotFound(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupIn_MultiGet_MissingPath_ReturnsSubDocPathNotFound";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("boo").Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus("boo"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Exists_PathExists_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupIn_Get_PathExists_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Exists("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_SingleExists_PathDoesNotExist_ReturnsSubDocPathNotFound(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupIn_Get_PathExists_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Exists("baz");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_SingleExists_MissingPath_ReturnsSubDocPathNotFound(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupIn_MultiCommands_ReturnsSubDocPathNotFound";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("baz");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        #endregion

#region Dictionary Insertion Commands

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_ValidPath_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_InsertDictionary_ValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>()});

            var builder = _bucket.MutateIn<dynamic>(key).Insert("bar.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = _bucket.MutateIn<dynamic>(key).Insert("par.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSubDocPathExists(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSubDocPathExists";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string> {{ "baz", "foo"}}});

            var builder = _bucket.MutateIn<dynamic>(key).Insert("bar.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathExists, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess";
            _bucket.Insert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = _bucket.MutateIn<dynamic>(key).Insert("par.baz", "faz", false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_DuplicatePath_ReturnsSubDocPathExists(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_InsertDictionary_DuplicatePath_ReturnsSubDocPathExists";
            _bucket.Insert(key, new { foo = "bar", bar = new Dictionary<string, string> { {"baz", "faz"} } });

            var builder = _bucket.MutateIn<dynamic>(key).Insert("bar.baz", "baz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathExists, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_InvalidPath_ReturnsSubDocInvalidPath(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_InsertDictionary_InvalidPath_ReturnsSubDocInvalidPath";
            _bucket.Insert(key, new { foo = "bar", bar = new Dictionary<string, string> { { "baz", "faz" } } });

            var builder = _bucket.MutateIn<dynamic>(key).Insert("bar[0]", "baz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathMismatch, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Upsert_Dictionary_ValidPath_ReturnsMuchSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Upsert_Dictionary_ReturnsMuchSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = _bucket.MutateIn<dynamic>(key).Upsert("bar.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = _bucket.MutateIn<dynamic>(key).Upsert("par.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = _bucket.MutateIn<dynamic>(key).Upsert("par.baz", "faz", false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Upsert_Dictionary_DuplicatePath_ReturnsSucesss(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Upsert_Dictionary_DuplicatePath_ReturnsSucesss";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string> { { "baz", "faz" } } });

            var builder = _bucket.MutateIn<dynamic>(key).Upsert("bar.baz", "baz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Upsert_Dictionary_InvalidPath_ReturnsSubDocInvalidPath(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Upsert_Dictionary_InvalidPath_ReturnsSubDocInvalidPath";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string> { { "baz", "faz" } } });

            var builder = _bucket.MutateIn<dynamic>(key).Upsert("bar[0]", "baz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathMismatch, result.OpStatus(0));
        }


        #endregion

#region Generic Modification Commands

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Replace_WithInvalidPath_ReturnsSubPathMultiFailure(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Replace_WithInvalidPath_ReturnsSubPathMultiFailure";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Replace("foo", "cas").Insert("bah", "bab", false).Replace("meh", "frack").Replace("hoo", "foo");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(2));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Replace_WithValidPath_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Replace_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Replace("foo", "foo").Replace("bar", "bar");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Delete_WithValidPath_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Delete_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Remove("foo").Replace("bar", "bar");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Delete_WithInValidPath_ReturnsSubDocPathNotFound(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Delete_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Remove("baz").Replace("bar", "bar");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        #endregion

#region Array commands

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayPrepend_WithValidPathAndMultipleValues_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Arrayprepend_WithValidPathAndMultipleValues_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> { 1, 2, 3 } });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayPrepend("bar", false, 1, 2, 3, 4);
            var result = builder.Execute();

            var expected = new[] { 1, 2, 3, 4, 1, 2, 3};
            var fragment = _bucket.LookupIn<dynamic>(key).Get("bar").Execute();
            var actual = fragment.Content<int[]>(0);

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayPrepend_WithInValidPath_ReturnsSubDocPathDoesNotExist(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_ArrayPrepend_WithInValidPath_ReturnsSubDocPathDoesNotExist";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> { 1, 2, 3 } });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayPrepend("baz", false, 1, 3, 4);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayAppend_WithValidPathAndMultipleValues_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_ArrayAppend_WithValidPathAndMultipleValues_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> { 1, 2, 3 } });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayAppend("bar", false, 1,2,3,4);
            var result = builder.Execute();

            var expected = new [] {1, 2, 3, 1, 2, 3, 4};
            var fragment = _bucket.LookupIn<dynamic>(key).Get("bar").Execute();
            var actual = fragment.Content<int[]>(0);

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayAppend_WithValidPath_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_ArrayAppend_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> {1,2,3} });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayAppend("bar", 4, false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayAppend_WithInValidPath_ReturnsSubDocPathDoesNotExist(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_ArrayAppend_WithInValidPath_ReturnsSubDocPathDoesNotExist";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> { 1, 2, 3 } });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayAppend("baz", 4, false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Insert_WithValidPath_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Insert_WithValidPathAndCreate_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", count = 0 });

            var builder = _bucket.MutateIn<dynamic>(key).Insert("baz", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayInsert_WithValidPath_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Insert_WithValidPathAndCreate_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> {} });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayInsert("bar[0]", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayInsert_WithMultipleValues_ReturnsSuccess(bool useMutation)
        {
            //arrange
            Setup(useMutation);
            var key = "MutateIn_ArrayInsert_WithMultipleValues_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> { } });

            //act
            var builder = _bucket.MutateIn<dynamic>(key).ArrayInsert("bar[0]", 1,2,3);
            var result = builder.Execute();

            var fragment = _bucket.LookupIn<dynamic>(key).Get("bar").Execute();
            var actual = fragment.Content<int[]>(0);
            var expected = new[] { 1, 2, 3 };

            //assert
            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayInsert_WithInValidPath_ReturnsSubDocPathInvalid(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Insert_WithValidPathAndCreate_SubDocPathInvalid";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> {0} });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayInsert("bar", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathInvalid, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_AddUnique_WithValidPathAndCreateParentsTrue_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_AddUnique_WithValidPathAndCreateParentsTrue_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", baz = new List<int> { 1, 2 } });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayAddUnique("bazs", "dd", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_AddUnique_WithValidPathAndCreateAndNumeric_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_AddUnique_WithValidPathAndCreateAndNumeric_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", baz = new List<int> { 1, 2 } });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayAddUnique("anumericarray", 1, true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_AddUnique_WithValidPathAndCreateAndExpires_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_AddUnique_WithValidPathAndCreateAndExpires_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", baz = new List<int> { 1, 2 } });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayAddUnique("anumericarray", 1, true).WithExpiry(new TimeSpan(0, 0, 10, 0));
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayInsert_WithValidPathAndCreateAndNumeric_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_AddUnique_WithValidPathAndCreateAndNumeric_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", baz = new List<int> { 1, 2 } });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayInsert("baz[2]", 1);
            var result = builder.Execute();

            var fragment = _bucket.LookupIn<dynamic>(key).Get("baz").Execute();
            var actual = fragment.Content<int[]>(0);
            var expected = new []{1,2,1};

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(expected, actual);
        }

        #endregion

#region counter tests

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Counter_WithValidPathAndCreateParentsFalse_ReturnsSucess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Counter_WithInValidPathAndCreateParentsFalse_ReturnsSucess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", count=0 });

            var builder = _bucket.MutateIn<dynamic>(key).Counter("baz", 1, false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Counter_WithValidPathAndCreateParentsTrue_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Counter_WithValidPathAndCreateParentsTrue_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", count = 0 });

            var builder = _bucket.MutateIn<dynamic>(key).Counter("baz", 1, true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        #endregion

#region single op tests

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_StatusReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_OpStatusReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ReturnsCountOfOne(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsCountOfOne";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(1, result.Value.Count);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ContentWithIndexReturnsBar(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsCountOfOne";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual("bar", result.Content<string>(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ContentWithPathReturnsBar(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsCountOfOne";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual("bar", result.Content("foo"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ContentWithPathReturnsArray(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupInBuilder_SingleGet_ContentWithPathReturnsArray";
            _bucket.Upsert(key, new { foo = "bar", bar = new List <int>{1, 2, 3} });

            var expected = new List<int> { 1, 2, 3 };
            var builder = _bucket.LookupIn<dynamic>(key).Get("bar");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(expected, result.Content<List<int>>("bar"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ContentWithPathReturnsObject(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupInBuilder_SingleGet_ContentWithPathReturnsObject";
            dynamic poo = new {baz = "faz"};
            _bucket.Upsert(key, new { foo = "bar", bar = poo });

            var expected = new {baz = "faz"};
            var builder = _bucket.LookupIn<dynamic>(key).Get("bar");
            var result = (DocumentFragment<dynamic>)builder.Execute();
            var actual = result.Content<dynamic>("bar");
            Assert.AreEqual(expected.baz, actual.baz.Value);
        }

        public class Foo
        {
            public string baz { get; set; }
        }
        #endregion

        [TearDown]
        public void TestFixtureTearDown()
        {
            _cluster.CloseBucket(_bucket);
            _cluster.Dispose();
        }
    }
}
