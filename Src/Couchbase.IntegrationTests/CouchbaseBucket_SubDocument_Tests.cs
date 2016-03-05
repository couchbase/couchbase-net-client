using System.Collections.Generic;
using Couchbase.Core;
using Couchbase.IO;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class CouchbaseBucket_SubDocument_Tests
    {

        private ICluster _cluster;
        private IBucket _bucket;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _cluster = new Cluster(Utils.TestConfiguration.GetCurrentConfiguration());
            _bucket = _cluster.OpenBucket();
        }

#region Retrieval Commands

        [Test]
        public void LookupIn_MultiCommands_ReturnsCorrectCount()
        {
            var key = "LookupIn_MultiCommands_ReturnsCorrectCount";
            _bucket.Upsert(key, new {foo = "bar", bar="foo"});

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo").Get("bar");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(2, result.Value.Count);
        }

        [Test]
        public void LookupIn_Get_PathExists_ReturnsSuccess()
        {
            var key = "LookupIn_Get_PathExists_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void LookupIn_Get_MissingPath_ReturnsSubDocPathNotFound()
        {
            var key = "LookupIn_MultiCommands_ReturnsSubDocPathNotFound";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("boo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus("boo"));
        }

        [Test]
        public void LookupIn_Exists_PathExists_ReturnsSuccess()
        {
            var key = "LookupIn_Get_PathExists_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void LookupIn_Exists_MissingPath_ReturnsSubDocPathNotFound()
        {
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
        public void MutateIn_InsertDictionary_ValidPath_ReturnsSuccess()
        {
            var key = "MutateIn_InsertDictionary_ValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>()});

            var builder = _bucket.MutateIn<dynamic>(key).Insert("bar.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess()
        {
            var key = "MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = _bucket.MutateIn<dynamic>(key).Insert("par.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSubDocPathExists()
        {
            var key = "MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSubDocPathExists";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string> {{ "baz", "foo"}}});

            var builder = _bucket.MutateIn<dynamic>(key).Insert("bar.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathExists, result.OpStatus(0));
        }

        [Test]
        public void MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess()
        {
            var key = "MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess";
            _bucket.Insert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = _bucket.MutateIn<dynamic>(key).Insert("par.baz", "faz", false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        public void MutateIn_InsertDictionary_DuplicatePath_ReturnsSubDocPathExists()
        {
            var key = "MutateIn_InsertDictionary_DuplicatePath_ReturnsSubDocPathExists";
            _bucket.Insert(key, new { foo = "bar", bar = new Dictionary<string, string> { {"baz", "faz"} } });

            var builder = _bucket.MutateIn<dynamic>(key).Insert("bar.baz", "baz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathExists, result.OpStatus(0));
        }

        [Test]
        public void MutateIn_InsertDictionary_InvalidPath_ReturnsSubDocInvalidPath()
        {
            var key = "MutateIn_InsertDictionary_InvalidPath_ReturnsSubDocInvalidPath";
            _bucket.Insert(key, new { foo = "bar", bar = new Dictionary<string, string> { { "baz", "faz" } } });

            var builder = _bucket.MutateIn<dynamic>(key).Insert("bar[0]", "baz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathMismatch, result.OpStatus(0));
        }

        [Test]
        public void MutateIn_Upsert_Dictionary_ValidPath_ReturnsMuchSuccess()
        {
            var key = "MutateIn_Upsert_Dictionary_ReturnsMuchSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = _bucket.MutateIn<dynamic>(key).Upsert("bar.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess()
        {
            var key = "MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = _bucket.MutateIn<dynamic>(key).Upsert("par.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess()
        {
            var key = "MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = _bucket.MutateIn<dynamic>(key).Upsert("par.baz", "faz", false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        public void MutateIn_Upsert_Dictionary_DuplicatePath_ReturnsSucesss()
        {
            var key = "MutateIn_Upsert_Dictionary_DuplicatePath_ReturnsSucesss";
            _bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string> { { "baz", "faz" } } });

            var builder = _bucket.MutateIn<dynamic>(key).Upsert("bar.baz", "baz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_Upsert_Dictionary_InvalidPath_ReturnsSubDocInvalidPath()
        {
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
        public void MutateIn_Replace_WithInvalidPath_ReturnsSubPathMultiFailure()
        {
            var key = "MutateIn_Replace_WithInvalidPath_ReturnsSubPathMultiFailure";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Replace("foo", "cas").Insert("bah", "bab", false).Replace("meh", "frack").Replace("hoo", "foo");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(2));
        }

        [Test]
        public void MutateIn_Replace_WithValidPath_ReturnsSuccess()
        {
            var key = "MutateIn_Replace_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Replace("foo", "foo").Replace("bar", "bar");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_Delete_WithValidPath_ReturnsSuccess()
        {
            var key = "MutateIn_Delete_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Remove("foo").Replace("bar", "bar");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_Delete_WithInValidPath_ReturnsSubDocPathNotFound()
        {
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
        public void MutateIn_PushBack_WithValidPath_ReturnsSuccess()
        {
            var key = "MutateIn_PushBack_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> {1,2,3} });

            var builder = _bucket.MutateIn<dynamic>(key).PushBack("bar", 4, false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_PushBack_WithInValidPath_ReturnsSubDocPathDoesNotExist()
        {
            var key = "MutateIn_PushBack_WithValidPath_ReturnsSubDocPathDoesNotExist";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> { 1, 2, 3 } });

            var builder = _bucket.MutateIn<dynamic>(key).PushBack("baz", 4, false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        public void MutateIn_Insert_WithValidPath_ReturnsSuccess()
        {
            var key = "MutateIn_Insert_WithValidPathAndCreate_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", count = 0 });

            var builder = _bucket.MutateIn<dynamic>(key).Insert("baz", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_ArrayInsert_WithValidPath_ReturnsSuccess()
        {
            var key = "MutateIn_Insert_WithValidPathAndCreate_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> {} });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayInsert("bar[0]", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_ArrayInsert_WithInValidPath_ReturnsSubDocPathInvalid()
        {
            var key = "MutateIn_Insert_WithValidPathAndCreate_SubDocPathInvalid";
            _bucket.Upsert(key, new { foo = "bar", bar = new List<int> {0} });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayInsert("bar", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathInvalid, result.OpStatus(0));
        }

        [Test]
        public void MutateIn_AddUnique_WithValidPathAndCreateParentsTrue_ReturnsSuccess()
        {
            var key = "MutateIn_AddUnique_WithValidPathAndCreateParentsTrue_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", baz = new List<int> { 1, 2 } });

            var builder = _bucket.MutateIn<dynamic>(key).AddUnique("anotherArray", "arrayInsert");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_AddUnique_WithValidPathAndCreateAndNumeric_ReturnsSuccess()
        {
            var key = "MutateIn_AddUnique_WithValidPathAndCreateAndNumeric_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", baz = new List<int> { 1, 2 } });

            var builder = _bucket.MutateIn<dynamic>(key).AddUnique("anumericarray", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_ArrayInsert_WithValidPathAndCreateAndNumeric_ReturnsSuccess()
        {
            var key = "MutateIn_AddUnique_WithValidPathAndCreateAndNumeric_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", baz = new List<int> { 1, 2 } });

            var builder = _bucket.MutateIn<dynamic>(key).ArrayInsert("baz[2]", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        #endregion

        [Test]
        public void MutateIn_Counter_WithValidPathAndCreateParentsFalse_ReturnsSucess()
        {
            var key = "MutateIn_Counter_WithInValidPathAndCreateParentsFalse_ReturnsSucess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", count=0 });

            var builder = _bucket.MutateIn<dynamic>(key).Counter("baz", 1, false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        public void MutateIn_Counter_WithValidPathAndCreateParentsTrue_ReturnsSuccess()
        {
            var key = "MutateIn_Counter_WithValidPathAndCreateParentsTrue_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo", count = 0 });

            var builder = _bucket.MutateIn<dynamic>(key).Counter("baz", 1, true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _cluster.CloseBucket(_bucket);
            _cluster.Dispose();
        }
    }
}
