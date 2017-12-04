using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.SubDocument;
using Couchbase.IntegrationTests.Utils;
using Couchbase.IO;
using Moq;
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
            _cluster.SetupEnhancedAuth();
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
        public void LookupIn_Get_PathExists_ReturnsValue(bool useMutation)
        {
            Setup(useMutation);

            var key = "LookupIn_Get_PathExists_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual("bar", result.Content<string>("foo"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_PocoPathExists_ReturnsValue(bool useMutation)
        {
            Setup(useMutation);

            var key = "LookupIn_Get_PathExists_ReturnsSuccess";
            _bucket.Upsert(key, new SimpleDoc { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<SimpleDoc>(key).Get("foo");
            var result = (DocumentFragment<SimpleDoc>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual("bar", result.Content<string>("foo"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_PocoPathExists_ValueIsCalled_ReturnsCount(bool useMutation)
        {
            Setup(useMutation);

            var key = "LookupIn_Get_PocoPathExists_ValueIsCalled_ReturnsValue";
            _bucket.Upsert(key, new SimpleDoc { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<SimpleDoc>(key).Get("foo");
            var result = (DocumentFragment<SimpleDoc>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.IsInstanceOf(typeof(ICollection<OperationSpec>), result.Value);
            Assert.AreEqual(1, result.Value.Count);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_PocoPathExists_DocumentFragment_Value_ReturnsICollectionOfOperationSpecs_IfCast(bool useMutation)
        {
            Setup(useMutation);

            var key = "LookupIn_Get_PocoPathExists_DocumentFragment_Value_ReturnsICollectionOfOperationSpecs";
            _bucket.Upsert(key, new SimpleDoc { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<SimpleDoc>(key).Get("foo");
            var result = (DocumentFragment<SimpleDoc>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.IsInstanceOf(typeof(ICollection<OperationSpec>), result.Value);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_PocoPathExists_DocumentFragment_Value_Returns_Null_IfNotCast(bool useMutation)
        {
            Setup(useMutation);

            var key = "LookupIn_Get_PocoPathExists_DocumentFragment_Value_Returns_Null_IfNotCast";
            _bucket.Upsert(key, new SimpleDoc { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<SimpleDoc>(key).Get("foo");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.IsNull(result.Value);
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

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_SinglePath_Exists_FailsWhenPathDoesNotExist(bool useMutation)
        {
            Setup(useMutation);
            var doc = new Document<dynamic>
            {
                Id = "Foo::123",
                Content = new
                {
                    Username = "mgroves",
                    Profile = new
                    {
                        PhoneNumber = "123-456-7890",
                        Address = new
                        {
                            Street = "123 Main Rd",
                            City = "Columbus",
                            State = "Ohio"
                        }
                    }
                }
            };
            _bucket.Upsert(doc);

            var subDoc2 = _bucket.LookupIn<dynamic>("Foo::123").Exists("profile.address.province").Execute();
            Assert.IsFalse(subDoc2.Exists("profile.address.province"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_SinglePath_Exists_SucceedsWhenPathExists(bool useMutation)
        {
            Setup(useMutation);
            var doc = new Document<dynamic>
            {
                Id = "Foo::123",
                Content = new
                {
                    Username = "mgroves",
                    Profile = new
                    {
                        PhoneNumber = "123-456-7890",
                        Address = new
                        {
                            Street = "123 Main Rd",
                            City = "Columbus",
                            State = "Ohio"
                        }
                    }
                }
            };
            _bucket.Upsert(doc);

            var subDoc = _bucket.LookupIn<dynamic>("Foo::123").Exists("profile.address.state").Execute();
            Assert.IsTrue(subDoc.Exists("profile.address.state"));
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

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_SucceedsWhenPathIsHiearchial(bool useMutation)
        {
            Setup(useMutation);
            const string id = "puppy";
            _bucket.Upsert(new Document<dynamic>
            {
                Id = id,
                Content = new
                {
                    Type = "dog",
                    Breed = "Pitbull/Chihuahua",
                    Name = "Puppy",
                    Toys = new List<string> {"squeaker", "ball", "shoe"},
                    Owner = new
                    {
                        Type = "servant",
                        Name = "Don Knotts",
                        Age = 63
                    },
                    Attributes = new Dictionary<string, object>
                    {
                        {"Fleas", true},
                        {"Color", "white"},
                        {"EyeColor", "brown"},
                        {"Age", 5},
                        {"Dirty", true},
                        {"Sex", "female"}
                    },
                    Counts = new List<object> {1}
                }
            });

            var builder = _bucket.LookupIn<dynamic>(id).
                Get("type").
                Get("name").
                Get("owner").
                Exists("notfound");

            var fragment = builder.Execute();
            Assert.IsTrue(fragment.OpStatus("type") == ResponseStatus.Success);
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
        public void MutateIn_Replace_SinglePocoWithValidPath_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Replace_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new SimpleDoc() { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<SimpleDoc>(key).Replace("foo", "foo");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Replace_SinglePocoWithValidPath_ValueChanges(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Replace_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new SimpleDoc() { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<SimpleDoc>(key).Replace("foo", "foo");
            builder.Execute();

            var result = _bucket.Get<SimpleDoc>(key);

            Assert.True(result.Success);
            Assert.AreEqual("foo", result.Value.foo);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Remove_MultiWithValidPath_ReturnsSuccess(bool useMutation)
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
        public void MutateIn_Remove_MultiWithInValidPath_ReturnsSubDocPathNotFound(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Delete_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Remove("baz").Replace("bar", "bar");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Remove_SingleWithValidPath_ReturnsSuccess(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Delete_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Remove("foo");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Remove_SingleWithInValidPath_ReturnsSubDocPathNotFound(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_Delete_WithValidPath_ReturnsSuccess";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Remove("baz");
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

        [Test(Description = "https://issues.couchbase.com/browse/NCBC-1348")]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_SingleCounterSmallValue_ReturnsValue(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_SingleCounterSmallValue_ReturnsValue";
            _bucket.Upsert(key, new { count = 0 });

            var builder = _bucket.MutateIn<dynamic>(key).Counter("count", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(1, result.Content<int>("count"));
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

        [Test(Description = "https://issues.couchbase.com/browse/NCBC-1348")]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ReturnsShortValue(bool useMutation)
        {
            Setup(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsShortValue";

            const int value = 3;
            _bucket.Upsert(key, new { foo = value });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();
            var actual = result.Content<int>("foo");
            Assert.AreEqual(value, actual);
        }

        [Test(Description = "https://issues.couchbase.com/browse/NCBC-1349")]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_SingleReplace_ReturnsMutationTokenWithEnhancedDurability(bool useMutation)
        {
            Setup(useMutation);
            var key = "MutateIn_SingleReplace_ReturnsMutationTokenWithEnhancedDurability";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Replace("foo", "foo");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(useMutation, result.Token.IsSet);
        }

        public class Foo
        {
            public string baz { get; set; }
        }
        #endregion

        #region async

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task LookupIn_ExecuteAsync_GetsResult(bool useMutation)
        {
            Setup(useMutation);

            var key = "LookupIn_ExecuteAsync_NoDeadlock";
            await _bucket.UpsertAsync(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo");

            var result = await builder.ExecuteAsync();

            Assert.AreEqual("bar", result.Content<string>(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task LookupInMulti_ExecuteAsync_GetsResult(bool useMutation)
        {
            Setup(useMutation);

            var key = "LookupIn_ExecuteAsync_NoDeadlock";
            await _bucket.UpsertAsync(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("foo").Get("bar");

            var result = await builder.ExecuteAsync();

            Assert.AreEqual("bar", result.Content<string>(0));
            Assert.AreEqual("foo", result.Content<string>(1));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_ExecuteAsync_NoDeadlock(bool useMutation)
        {
            // Using an asynchronous call within an MVC Web API action can cause
            // a deadlock if you wait for the result synchronously.

            Setup(useMutation);

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                var key = "LookupIn_ExecuteAsync_NoDeadlock";
                _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

                var builder = _bucket.LookupIn<dynamic>(key).Get("foo");

                builder.ExecuteAsync().Wait();

                // If execution is incorrectly awaiting on the current SynchronizationContext
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
        [TestCase(true)]
        [TestCase(false)]
        public async Task MutateIn_ExecuteAsync_ModifiesDocument(bool useMutation)
        {
            Setup(useMutation);

            var key = "MutateIn_ExecuteAsync_ModifiesDocument";
            await _bucket.UpsertAsync(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.MutateIn<dynamic>(key).Replace("foo", "baz");

            var result = await builder.ExecuteAsync();

            Assert.IsTrue(result.Success);

            var document = await _bucket.GetDocumentAsync<dynamic>(key);

            Assert.AreEqual("baz", document.Content.foo.ToString());
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task MutateInMulti_ExecuteAsync_ModifiesDocument(bool useMutation)
        {
            Setup(useMutation);

            var key = "MutateIn_ExecuteAsync_ModifiesDocument_" + useMutation;
            var upsert = await _bucket.UpsertAsync(key, new { foo = "bar", bar = "foo" });
            Assert.AreEqual(ResponseStatus.Success, upsert.Status);

            var builder = _bucket.MutateIn<dynamic>(key).Replace("foo", "baz").Replace("bar", "fot");

            var result = await builder.ExecuteAsync();
            Assert.AreEqual(ResponseStatus.Success, result.Status);

            var document = await _bucket.GetDocumentAsync<dynamic>(key);
            Assert.AreEqual(ResponseStatus.Success, document.Status);

            Assert.AreEqual("baz", document.Content.foo.ToString());
            Assert.AreEqual("fot", document.Content.bar.ToString());
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ExecuteAsync_NoDeadlock(bool useMutation)
        {
            // Using an asynchronous call within an MVC Web API action can cause
            // a deadlock if you wait for the result synchronously.

            Setup(useMutation);

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                var key = "MutateIn_ExecuteAsync_NoDeadlock";
                _bucket.Upsert(key, new {foo = "bar", bar = "foo"});

                var builder = _bucket.MutateIn<dynamic>(key).Replace("foo", "baz");

                builder.ExecuteAsync().Wait();

                // If execution is incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        #endregion

        #region helpers

        private class SimpleDoc
        {
            public string foo { get; set; }
            public string bar { get; set; }
        }

        #endregion

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Count(bool useMutation)
        {
            Setup(useMutation);

            var key = "LookupIn_MultiCommands_ReturnsCorrectCount";
            _bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = _bucket.LookupIn<dynamic>(key).Get("fo4").Get("bar");
            var result = builder.Execute();

            Assert.AreEqual(2, result.Count());
        }

        #region XATTRs

        private const string XAttrsNotSupported = "XATTRs not supported.";

        private bool SupportsXAttributes()
        {
            var bucket = (CouchbaseBucket) _bucket;
            return bucket != null && bucket.SupportsSubdocXAttributes;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Can_Create_Get_And_Check_Single_Xattr_Exists(bool useMutation)
        {
            Setup(useMutation);

            if (!SupportsXAttributes())
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Create_Get_And_Check_Single_Xattr_Exists";
            const string username = "jack";
            _bucket.Upsert(key, new {first = "foo", last = "bar"});

            var mutateResult = _bucket.MutateIn<dynamic>(key)
                .Upsert("_data.created_by", username, SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var getResult = _bucket.LookupIn<dynamic>(key)
                .Get("_data.created_by", SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(username, getResult.Content<string>(0));

            var existsResult = _bucket.LookupIn<dynamic>(key)
                .Exists("_data.created_by", SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(existsResult.Success);
        }

        [Test]
        public void Can_Create_And_Count_Subdoc_Property()
        {
            if (!TestConfiguration.Settings.EnhancedAuth)
            {
                Assert.Ignore("Requires CB server 5.0+");
            }

            using (var cluster = new Cluster(Utils.TestConfiguration.GetDefaultConfiguration()))
            {
                cluster.SetupEnhancedAuth();
                var bucket = cluster.OpenBucket("default");

                const string key = "Can_Create_And_Count_Subdoc_Property";
                var fruit = new List<string> {"apple", "pear", "bannana", "peach"};
                bucket.Upsert(key, new {fruit});

                var getResult = bucket.LookupIn<dynamic>(key)
                    .GetCount("fruit")
                    .Execute();

                Assert.IsTrue(getResult.Success);
                Assert.AreEqual(fruit.Count, getResult.Content<int>(0));
            }
        }

        [Test]
        public void Can_Create_And_Count_Subdoc_Property_With_Multi_Operations()
        {
            if (!TestConfiguration.Settings.EnhancedAuth)
            {
                Assert.Ignore("Requires CB server 5.0+");
            }

            using (var cluster = new Cluster(Utils.TestConfiguration.GetDefaultConfiguration()))
            {
                cluster.SetupEnhancedAuth();
                var bucket = cluster.OpenBucket("default");

                const string key = "Can_Create_And_Count_Subdoc_Property_With_Multi_Operations";
                var fruit = new List<string> { "apple", "pear", "bannana", "peach" };
                bucket.Upsert(key, new { fruit });

                var getResult = bucket.LookupIn<dynamic>(key)
                    .Get("fruit")
                    .GetCount("fruit")
                    .Execute();

                Assert.IsTrue(getResult.Success);
                Assert.AreEqual(fruit, getResult.Content<List<string>>(0));
                Assert.AreEqual(fruit.Count, getResult.Content<int>(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Can_Create_Get_And_Check_Multiple_Xattrs_Exist(bool useMutation)
        {
            Setup(useMutation);

            if (!SupportsXAttributes())
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Create_Get_And_Check_Multiple_Xattrs_Exist";
            _bucket.Upsert(key, new {foo = "bar"});

            const string createdBy = "jack";
            const string modifiedBy = "jill";

            var mutateResult = _bucket.MutateIn<dynamic>(key)
                .Upsert("_data.created_by", createdBy, SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr)
                .Upsert("_data.modified_by", modifiedBy, SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var getResult = _bucket.LookupIn<dynamic>(key)
                .Get("_data.created_by", SubdocPathFlags.Xattr)
                .Get("_data.modified_by", SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(createdBy, getResult.Content<string>(0));
            Assert.AreEqual(modifiedBy, getResult.Content<string>(1));

            var existsResult = _bucket.LookupIn<dynamic>(key)
                .Exists("_data.created_by", SubdocPathFlags.Xattr)
                .Exists("_data.modified_by", SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(existsResult.Success);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void XATTRS_Persist_After_Upsert_Or_Replace(bool useMutation)
        {
            Setup(useMutation);

            if (!SupportsXAttributes())
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "XATTRS_Persist_After_Replace";
            const string field = "_data.created_by";
            const string value = "jack";

            _bucket.Remove(key);
            _bucket.Upsert(key, new {name = "mike"});

            // Add XATTR
            var createResult = _bucket.MutateIn<dynamic>(key)
                .Upsert(field, value, SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(createResult.Success);

            // Replace document body
            var replaceResult = _bucket.Replace(key, new {name = "michael"});

            Assert.IsTrue(replaceResult.Success);

            // Try to get the xattr
            var getResult = _bucket.LookupIn<dynamic>(key)
                .Get(field, SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(value, getResult.Content<string>(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Can_Create_XATTR_with_CreatePath_Flag(bool useMutation)
        {
            Setup(useMutation);

            if (!SupportsXAttributes())
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Get_XATTR_with_CreatePath_Flag";
            const string field = "created_by";
            const string value = "jack";

            _bucket.Upsert(key, new {name = "mike"});

            var mutateResult = _bucket.MutateIn<dynamic>(key)
                .Upsert(field, value, SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var getResult = _bucket.LookupIn<dynamic>(key)
                .Get(field, SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(value, getResult.Content<string>(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Can_Create_Document_With_CreateDocument_Subdoc_Flag(bool useMutation)
        {
            Setup(useMutation);

            if (!SupportsXAttributes())
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Create_Document_With_CreateDocument_Subdoc_Flag";
            const string field = "name";
            const string name = "mike";

            _bucket.Remove(key);
            var existsResult = _bucket.Exists(key);

            Assert.IsFalse(existsResult);

            var mutateResult = _bucket.MutateIn<dynamic>(key)
                .Upsert(field, name, SubdocPathFlags.CreatePath, SubdocDocFlags.InsertDocument)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var getResult = _bucket.LookupIn<dynamic>(key)
                .Get(field)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(name, getResult.Content<string>(0));
        }

        [Ignore("Requires additional logical delete server feature")]
        [TestCase(false)]
        [TestCase(true)]
        public void Can_Get_Deleted_Document_System_XATTR_Using_AccessDeleted_Subdoc_Flag(bool useMutation)
        {
            Setup(useMutation);

            if (!SupportsXAttributes())
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Get_Deleted_Document_XATTR_Using_AccessDeleted_Subdoc_Flag";
            const string field = "_data.username";
            const string value = "jack";

            _bucket.Remove(key);
            _bucket.Upsert(key, new { name = "mike" });

            var mutateResult = _bucket.MutateIn<dynamic>(key)
                .Upsert(field, value, SubdocPathFlags.Xattr | SubdocPathFlags.CreatePath)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var removeResult = _bucket.Remove(key);

            Assert.IsTrue(removeResult.Success);

            var getResult = _bucket.LookupIn<dynamic>(key)
                .Get(field, SubdocPathFlags.Xattr, SubdocDocFlags.AccessDeleted)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(value, getResult.Content<string>(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Can_Use_Server_Macro_To_Populate_XATTR(bool useMutation)
        {
            Setup(useMutation);

            if (!SupportsXAttributes())
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Use_Server_Macro_To_Populate_XATTR";
            const string field = "cas";
            const string value = "${Mutation.CAS}";

            _bucket.Upsert(key, new {name = "mike"});

            var mutateResult = _bucket.MutateIn<dynamic>(key)
                .Upsert(field, value, SubdocPathFlags.Xattr | SubdocPathFlags.ExpandMacroValues)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var getResult = _bucket.LookupIn<dynamic>(key)
                .Get(field, SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.IsFalse(string.IsNullOrEmpty(getResult.Content<string>(0)));
        }

        #endregion

        [TearDown]
        public void OneTimeTearDown()
        {
            if (_cluster != null)
            {
                _cluster.Dispose();
                _cluster = null;
            }
        }
    }
}
