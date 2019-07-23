using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Buckets;
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
        private ICluster _clusterWithMutationsTokens;

        [OneTimeSetUp]
        public void Setup()
        {
            var config = TestConfiguration.GetCurrentConfiguration();

            // create cluster with without mutation tokens enabled
            config.BucketConfigs.First().Value.UseEnhancedDurability = false;
            _cluster = new Cluster(config);
            _cluster.SetupEnhancedAuth();
            _cluster.OpenBucket();

            // create cluster with with mutation tokens enabled
            config.BucketConfigs.First().Value.UseEnhancedDurability = true;
            _clusterWithMutationsTokens = new Cluster(config);
            _clusterWithMutationsTokens.SetupEnhancedAuth();
            _clusterWithMutationsTokens.OpenBucket();
        }

        private IBucket GetBucket(bool useMutation)
        {
            return useMutation ? _clusterWithMutationsTokens.OpenBucket() : _cluster.OpenBucket();
        }

        #region Retrieval Commands

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Last_Command_Returns_Status(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupIn_Last_Command_Returns_Status";

            bucket.Upsert(key, new
            {
                field1 = "value1",
                field2 = "value2"
            });

            var lookupInBuilder = bucket.LookupIn<dynamic>(key);
            lookupInBuilder.Get("field1");
            lookupInBuilder.Get("field2");
            lookupInBuilder.Exists("does_not_exist");
            var result = lookupInBuilder.Execute();

            Assert.AreEqual(result.OpStatus(0), ResponseStatus.Success);
            Assert.AreEqual(result.OpStatus(1), ResponseStatus.Success);
            Assert.AreEqual(result.OpStatus(2), ResponseStatus.SubDocPathNotFound);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_MultiCommands_ReturnsCorrectCount(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            var key = "LookupIn_MultiCommands_ReturnsCorrectCount";
            bucket.Upsert(key, new {foo = "bar", bar="foo"});

            var builder = bucket.LookupIn<dynamic>(key).Get("foo").Get("bar");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(2, result.Value.Count);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_PathExists_ReturnsValue(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            var key = "LookupIn_Get_PathExists_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual("bar", result.Content<string>("foo"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_PocoPathExists_ReturnsValue(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            var key = "LookupIn_Get_PathExists_ReturnsSuccess";
            bucket.Upsert(key, new SimpleDoc { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<SimpleDoc>(key).Get("foo");
            var result = (DocumentFragment<SimpleDoc>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual("bar", result.Content<string>("foo"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_PocoPathExists_ValueIsCalled_ReturnsCount(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            var key = "LookupIn_Get_PocoPathExists_ValueIsCalled_ReturnsValue";
            bucket.Upsert(key, new SimpleDoc { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<SimpleDoc>(key).Get("foo");
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
            var bucket = GetBucket(useMutation);

            var key = "LookupIn_Get_PocoPathExists_DocumentFragment_Value_ReturnsICollectionOfOperationSpecs";
            bucket.Upsert(key, new SimpleDoc { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<SimpleDoc>(key).Get("foo");
            var result = (DocumentFragment<SimpleDoc>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.IsInstanceOf(typeof(ICollection<OperationSpec>), result.Value);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_PocoPathExists_DocumentFragment_Value_Returns_Null_IfNotCast(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            var key = "LookupIn_Get_PocoPathExists_DocumentFragment_Value_Returns_Null_IfNotCast";
            bucket.Upsert(key, new SimpleDoc { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<SimpleDoc>(key).Get("foo");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.IsNull(result.Value);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_MultiExists_PathExists_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupIn_MultiExists_PathExists_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Exists("foo").Exists("bar");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_MultiExists_PathExists_ReturnsSubDocMultiPathFailure(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupIn_MultiExists_PathExists_ReturnsSubDocMultiPathFailure";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Exists("foo").Exists("car");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_MissingPath_ReturnsSubDocPathNotFound(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupIn_MultiCommands_ReturnsSubDocPathNotFound";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("boo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus("boo"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_MultiGet_MissingPath_ReturnsSubDocPathNotFound(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupIn_MultiGet_MissingPath_ReturnsSubDocPathNotFound";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("boo").Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus("boo"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Exists_PathExists_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupIn_Get_PathExists_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Exists("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_SingleExists_PathDoesNotExist_ReturnsSubDocPathNotFound(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupIn_Get_PathExists_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Exists("baz");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_SingleExists_MissingPath_ReturnsSubDocPathNotFound(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupIn_MultiCommands_ReturnsSubDocPathNotFound";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("baz");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_SinglePath_Exists_FailsWhenPathDoesNotExist(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
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
            bucket.Upsert(doc);

            var subDoc2 = bucket.LookupIn<dynamic>("Foo::123").Exists("profile.address.province").Execute();
            Assert.IsFalse(subDoc2.Exists("profile.address.province"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_SinglePath_Exists_SucceedsWhenPathExists(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
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
            bucket.Upsert(doc);

            var subDoc = bucket.LookupIn<dynamic>("Foo::123").Exists("profile.address.state").Execute();
            Assert.IsTrue(subDoc.Exists("profile.address.state"));
        }

        #endregion

        #region Dictionary Insertion Commands

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_ValidPath_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_InsertDictionary_ValidPath_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>()});

            var builder = bucket.MutateIn<dynamic>(key).Insert("bar.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = bucket.MutateIn<dynamic>(key).Insert("par.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSubDocPathExists(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsSubDocPathExists";
            bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string> {{ "baz", "foo"}}});

            var builder = bucket.MutateIn<dynamic>(key).Insert("bar.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathExists, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_InsertDictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess";
            bucket.Insert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = bucket.MutateIn<dynamic>(key).Insert("par.baz", "faz", false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_DuplicatePath_ReturnsSubDocPathExists(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_InsertDictionary_DuplicatePath_ReturnsSubDocPathExists";
            bucket.Insert(key, new { foo = "bar", bar = new Dictionary<string, string> { {"baz", "faz"} } });

            var builder = bucket.MutateIn<dynamic>(key).Insert("bar.baz", "baz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathExists, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_InsertDictionary_InvalidPath_ReturnsSubDocInvalidPath(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_InsertDictionary_InvalidPath_ReturnsSubDocInvalidPath";
            bucket.Insert(key, new { foo = "bar", bar = new Dictionary<string, string> { { "baz", "faz" } } });

            var builder = bucket.MutateIn<dynamic>(key).Insert("bar[0]", "baz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathMismatch, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Upsert_Dictionary_ValidPath_ReturnsMuchSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Upsert_Dictionary_ReturnsMuchSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = bucket.MutateIn<dynamic>(key).Upsert("bar.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = bucket.MutateIn<dynamic>(key).Upsert("par.baz", "faz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Upsert_Dictionary_MissingParentAndCreateParentsIsTrue_ReturnsNotSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string>() });

            var builder = bucket.MutateIn<dynamic>(key).Upsert("par.baz", "faz", false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Upsert_Dictionary_DuplicatePath_ReturnsSucesss(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Upsert_Dictionary_DuplicatePath_ReturnsSucesss";
            bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string> { { "baz", "faz" } } });

            var builder = bucket.MutateIn<dynamic>(key).Upsert("bar.baz", "baz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Upsert_Dictionary_InvalidPath_ReturnsSubDocInvalidPath(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Upsert_Dictionary_InvalidPath_ReturnsSubDocInvalidPath";
            bucket.Upsert(key, new { foo = "bar", bar = new Dictionary<string, string> { { "baz", "faz" } } });

            var builder = bucket.MutateIn<dynamic>(key).Upsert("bar[0]", "baz", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathMismatch, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupIn_Get_SucceedsWhenPathIsHiearchial(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            const string id = "puppy";
            bucket.Upsert(new Document<dynamic>
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

            var builder = bucket.LookupIn<dynamic>(id).
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
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Replace_WithInvalidPath_ReturnsSubPathMultiFailure";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.MutateIn<dynamic>(key).Replace("foo", "cas").Insert("bah", "bab", false).Replace("meh", "frack").Replace("hoo", "foo");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(2));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Replace_WithValidPath_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Replace_WithValidPath_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.MutateIn<dynamic>(key).Replace("foo", "foo").Replace("bar", "bar");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Replace_SinglePocoWithValidPath_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Replace_WithValidPath_ReturnsSuccess";
            bucket.Upsert(key, new SimpleDoc() { foo = "bar", bar = "foo" });

            var builder = bucket.MutateIn<SimpleDoc>(key).Replace("foo", "foo");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Replace_SinglePocoWithValidPath_ValueChanges(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Replace_WithValidPath_ReturnsSuccess";
            bucket.Upsert(key, new SimpleDoc() { foo = "bar", bar = "foo" });

            var builder = bucket.MutateIn<SimpleDoc>(key).Replace("foo", "foo");
            builder.Execute();

            var result = bucket.Get<SimpleDoc>(key);

            Assert.True(result.Success);
            Assert.AreEqual("foo", result.Value.foo);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Remove_MultiWithValidPath_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Delete_WithValidPath_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.MutateIn<dynamic>(key).Remove("foo").Replace("bar", "bar");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Remove_MultiWithInValidPath_ReturnsSubDocPathNotFound(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Delete_WithValidPath_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.MutateIn<dynamic>(key).Remove("baz").Replace("bar", "bar");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Remove_SingleWithValidPath_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Delete_WithValidPath_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.MutateIn<dynamic>(key).Remove("foo");
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Remove_SingleWithInValidPath_ReturnsSubDocPathNotFound(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Delete_WithValidPath_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.MutateIn<dynamic>(key).Remove("baz");
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
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Arrayprepend_WithValidPathAndMultipleValues_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = new List<int> { 1, 2, 3 } });

            var builder = bucket.MutateIn<dynamic>(key).ArrayPrepend("bar", false, 1, 2, 3, 4);
            var result = builder.Execute();

            var expected = new[] { 1, 2, 3, 4, 1, 2, 3};
            var fragment = bucket.LookupIn<dynamic>(key).Get("bar").Execute();
            var actual = fragment.Content<int[]>(0);

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayPrepend_WithInValidPath_ReturnsSubDocPathDoesNotExist(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_ArrayPrepend_WithInValidPath_ReturnsSubDocPathDoesNotExist";
            bucket.Upsert(key, new { foo = "bar", bar = new List<int> { 1, 2, 3 } });

            var builder = bucket.MutateIn<dynamic>(key).ArrayPrepend("baz", false, 1, 3, 4);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayAppend_WithValidPathAndMultipleValues_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_ArrayAppend_WithValidPathAndMultipleValues_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = new List<int> { 1, 2, 3 } });

            var builder = bucket.MutateIn<dynamic>(key).ArrayAppend("bar", false, 1,2,3,4);
            var result = builder.Execute();

            var expected = new [] {1, 2, 3, 1, 2, 3, 4};
            var fragment = bucket.LookupIn<dynamic>(key).Get("bar").Execute();
            var actual = fragment.Content<int[]>(0);

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayAppend_WithValidPath_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_ArrayAppend_WithValidPath_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = new List<int> {1,2,3} });

            var builder = bucket.MutateIn<dynamic>(key).ArrayAppend("bar", 4, false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayAppend_WithInValidPath_ReturnsSubDocPathDoesNotExist(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_ArrayAppend_WithInValidPath_ReturnsSubDocPathDoesNotExist";
            bucket.Upsert(key, new { foo = "bar", bar = new List<int> { 1, 2, 3 } });

            var builder = bucket.MutateIn<dynamic>(key).ArrayAppend("baz", 4, false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathNotFound, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Insert_WithValidPath_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Insert_WithValidPathAndCreate_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo", count = 0 });

            var builder = bucket.MutateIn<dynamic>(key).Insert("baz", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayInsert_WithValidPath_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Insert_WithValidPathAndCreate_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = new List<int> {} });

            var builder = bucket.MutateIn<dynamic>(key).ArrayInsert("bar[0]", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayInsert_WithMultipleValues_ReturnsSuccess(bool useMutation)
        {
            //arrange
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_ArrayInsert_WithMultipleValues_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = new List<int> { } });

            //act
            var builder = bucket.MutateIn<dynamic>(key).ArrayInsert("bar[0]", 1,2,3);
            var result = builder.Execute();

            var fragment = bucket.LookupIn<dynamic>(key).Get("bar").Execute();
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
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Insert_WithValidPathAndCreate_SubDocPathInvalid";
            bucket.Upsert(key, new { foo = "bar", bar = new List<int> {0} });

            var builder = bucket.MutateIn<dynamic>(key).ArrayInsert("bar", 1);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.SubDocMultiPathFailure, result.Status);
            Assert.AreEqual(ResponseStatus.SubDocPathInvalid, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_AddUnique_WithValidPathAndCreateParentsTrue_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_AddUnique_WithValidPathAndCreateParentsTrue_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo", baz = new List<int> { 1, 2 } });

            var builder = bucket.MutateIn<dynamic>(key).ArrayAddUnique("bazs", "dd", true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_AddUnique_WithValidPathAndCreateAndNumeric_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_AddUnique_WithValidPathAndCreateAndNumeric_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo", baz = new List<int> { 1, 2 } });

            var builder = bucket.MutateIn<dynamic>(key).ArrayAddUnique("anumericarray", 1, true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_AddUnique_WithValidPathAndCreateAndExpires_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_AddUnique_WithValidPathAndCreateAndExpires_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo", baz = new List<int> { 1, 2 } });

            var builder = bucket.MutateIn<dynamic>(key).ArrayAddUnique("anumericarray", 1, true).WithExpiry(new TimeSpan(0, 0, 10, 0));
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_ArrayInsert_WithValidPathAndCreateAndNumeric_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_AddUnique_WithValidPathAndCreateAndNumeric_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo", baz = new List<int> { 1, 2 } });

            var builder = bucket.MutateIn<dynamic>(key).ArrayInsert("baz[2]", 1);
            var result = builder.Execute();

            var fragment = bucket.LookupIn<dynamic>(key).Get("baz").Execute();
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
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Counter_WithInValidPathAndCreateParentsFalse_ReturnsSucess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo", count=0 });

            var builder = bucket.MutateIn<dynamic>(key).Counter("baz", 1, false);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Counter_WithValidPathAndCreateParentsTrue_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_Counter_WithValidPathAndCreateParentsTrue_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo", count = 0 });

            var builder = bucket.MutateIn<dynamic>(key).Counter("baz", 1, true);
            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test(Description = "https://issues.couchbase.com/browse/NCBC-1348")]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_SingleCounterSmallValue_ReturnsValue(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_SingleCounterSmallValue_ReturnsValue";
            bucket.Upsert(key, new { count = 0 });

            var builder = bucket.MutateIn<dynamic>(key).Counter("count", 1);
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
            var bucket = GetBucket(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_OpStatusReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.OpStatus(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ReturnsCountOfOne(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsCountOfOne";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(1, result.Value.Count);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ContentWithIndexReturnsBar(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsCountOfOne";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual("bar", result.Content<string>(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ContentWithPathReturnsBar(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsCountOfOne";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual("bar", result.Content("foo"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ContentWithPathReturnsArray(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupInBuilder_SingleGet_ContentWithPathReturnsArray";
            bucket.Upsert(key, new { foo = "bar", bar = new List <int>{1, 2, 3} });

            var expected = new List<int> { 1, 2, 3 };
            var builder = bucket.LookupIn<dynamic>(key).Get("bar");
            var result = (DocumentFragment<dynamic>)builder.Execute();

            Assert.AreEqual(expected, result.Content<List<int>>("bar"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ContentWithPathReturnsObject(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupInBuilder_SingleGet_ContentWithPathReturnsObject";
            dynamic poo = new {baz = "faz"};
            bucket.Upsert(key, new { foo = "bar", bar = poo });

            var expected = new {baz = "faz"};
            var builder = bucket.LookupIn<dynamic>(key).Get("bar");
            var result = (DocumentFragment<dynamic>)builder.Execute();
            var actual = result.Content<dynamic>("bar");
            Assert.AreEqual(expected.baz, actual.baz.Value);
        }

        [Test(Description = "https://issues.couchbase.com/browse/NCBC-1348")]
        [TestCase(true)]
        [TestCase(false)]
        public void LookupInBuilder_SingleGet_ReturnsShortValue(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "LookupInBuilder_SingleGet_ReturnsShortValue";

            const int value = 3;
            bucket.Upsert(key, new { foo = value });

            var builder = bucket.LookupIn<dynamic>(key).Get("foo");
            var result = (DocumentFragment<dynamic>)builder.Execute();
            var actual = result.Content<int>("foo");
            Assert.AreEqual(value, actual);
        }

        [Test(Description = "https://issues.couchbase.com/browse/NCBC-1349")]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_SingleReplace_ReturnsMutationTokenWithEnhancedDurability(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateIn_SingleReplace_ReturnsMutationTokenWithEnhancedDurability";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.MutateIn<dynamic>(key).Replace("foo", "foo");
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
            var bucket = GetBucket(useMutation);

            var key = "LookupIn_ExecuteAsync_NoDeadlock";
            await bucket.UpsertAsync(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("foo");

            var result = await builder.ExecuteAsync();

            Assert.AreEqual("bar", result.Content<string>(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task LookupInMulti_ExecuteAsync_GetsResult(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            var key = "LookupIn_ExecuteAsync_NoDeadlock";
            await bucket.UpsertAsync(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("foo").Get("bar");

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

            var bucket = GetBucket(useMutation);

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                var key = "LookupIn_ExecuteAsync_NoDeadlock";
                bucket.Upsert(key, new { foo = "bar", bar = "foo" });

                var builder = bucket.LookupIn<dynamic>(key).Get("foo");

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
            var bucket = GetBucket(useMutation);

            var key = "MutateIn_ExecuteAsync_ModifiesDocument";
            await bucket.UpsertAsync(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.MutateIn<dynamic>(key).Replace("foo", "baz");

            var result = await builder.ExecuteAsync();

            Assert.IsTrue(result.Success);

            var document = await bucket.GetDocumentAsync<dynamic>(key);

            Assert.AreEqual("baz", document.Content.foo.ToString());
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task MutateInMulti_ExecuteAsync_ModifiesDocument(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            var key = "MutateIn_ExecuteAsync_ModifiesDocument_" + useMutation;
            var upsert = await bucket.UpsertAsync(key, new { foo = "bar", bar = "foo" });
            Assert.AreEqual(ResponseStatus.Success, upsert.Status);

            var builder = bucket.MutateIn<dynamic>(key).Replace("foo", "baz").Replace("bar", "fot");

            var result = await builder.ExecuteAsync();
            Assert.AreEqual(ResponseStatus.Success, result.Status);

            var document = await bucket.GetDocumentAsync<dynamic>(key);
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

            var bucket = GetBucket(useMutation);

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                var key = "MutateIn_ExecuteAsync_NoDeadlock";
                bucket.Upsert(key, new {foo = "bar", bar = "foo"});

                var builder = bucket.MutateIn<dynamic>(key).Replace("foo", "baz");

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
            var bucket = GetBucket(useMutation);

            var key = "LookupIn_MultiCommands_ReturnsCorrectCount";
            bucket.Upsert(key, new { foo = "bar", bar = "foo" });

            var builder = bucket.LookupIn<dynamic>(key).Get("fo4").Get("bar");
            var result = builder.Execute();

            Assert.AreEqual(2, result.Count());
        }

        #region XATTRs

        private const string XAttrsNotSupported = "XATTRs not supported.";

        private bool SupportsXAttributes(IBucket bucket)
        {
            if (bucket is CouchbaseBucket couchbaseBucket)
            {
                return couchbaseBucket.SupportsSubdocXAttributes;
            }

            return false;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Can_Create_Get_And_Check_Single_Xattr_Exists(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            if (!SupportsXAttributes(bucket))
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Create_Get_And_Check_Single_Xattr_Exists";
            const string username = "jack";
            bucket.Upsert(key, new {first = "foo", last = "bar"});

            var mutateResult = bucket.MutateIn<dynamic>(key)
                .Upsert("_data.created_by", username, SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var getResult = bucket.LookupIn<dynamic>(key)
                .Get("_data.created_by", SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(username, getResult.Content<string>(0));

            var existsResult = bucket.LookupIn<dynamic>(key)
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
            var bucket = GetBucket(useMutation);

            if (!SupportsXAttributes(bucket))
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Create_Get_And_Check_Multiple_Xattrs_Exist";
            bucket.Upsert(key, new {foo = "bar"});

            const string createdBy = "jack";
            const string modifiedBy = "jill";

            var mutateResult = bucket.MutateIn<dynamic>(key)
                .Upsert("_data.created_by", createdBy, SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr)
                .Upsert("_data.modified_by", modifiedBy, SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var getResult = bucket.LookupIn<dynamic>(key)
                .Get("_data.created_by", SubdocPathFlags.Xattr)
                .Get("_data.modified_by", SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(createdBy, getResult.Content<string>(0));
            Assert.AreEqual(modifiedBy, getResult.Content<string>(1));

            var existsResult = bucket.LookupIn<dynamic>(key)
                .Exists("_data.created_by", SubdocPathFlags.Xattr)
                .Exists("_data.modified_by", SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(existsResult.Success);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void XATTRS_Persist_After_Upsert_Or_Replace(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            if (!SupportsXAttributes(bucket))
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "XATTRS_Persist_After_Replace";
            const string field = "_data.created_by";
            const string value = "jack";

            bucket.Remove(key);
            bucket.Upsert(key, new {name = "mike"});

            // Add XATTR
            var createResult = bucket.MutateIn<dynamic>(key)
                .Upsert(field, value, SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(createResult.Success);

            // Replace document body
            var replaceResult = bucket.Replace(key, new {name = "michael"});

            Assert.IsTrue(replaceResult.Success);

            // Try to get the xattr
            var getResult = bucket.LookupIn<dynamic>(key)
                .Get(field, SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(value, getResult.Content<string>(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Can_Create_XATTR_with_CreatePath_Flag(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            if (!SupportsXAttributes(bucket))
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Get_XATTR_with_CreatePath_Flag";
            const string field = "created_by";
            const string value = "jack";

            bucket.Upsert(key, new {name = "mike"});

            var mutateResult = bucket.MutateIn<dynamic>(key)
                .Upsert(field, value, SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var getResult = bucket.LookupIn<dynamic>(key)
                .Get(field, SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(value, getResult.Content<string>(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Can_Create_Document_With_CreateDocument_Subdoc_Flag(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            if (!SupportsXAttributes(bucket))
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Create_Document_With_CreateDocument_Subdoc_Flag";
            const string field = "name";
            const string name = "mike";

            bucket.Remove(key);
            var existsResult = bucket.Exists(key);

            Assert.IsFalse(existsResult);

            var mutateResult = bucket.MutateIn<dynamic>(key)
                .Upsert(field, name, SubdocPathFlags.CreatePath, SubdocDocFlags.InsertDocument)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var getResult = bucket.LookupIn<dynamic>(key)
                .Get(field)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(name, getResult.Content<string>(0));
        }

        [Test]
        public async Task Can_create_multiple_xattrs_in_single_call()
        {
            var bucket = GetBucket(false);

            if (!SupportsXAttributes(bucket))
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_create_multiple_xattrs_in_single_call";

            await bucket.RemoveAsync(key);
            Assert.IsFalse(bucket.Exists(key));

            var mutateResult = await bucket.MutateIn<dynamic>(key)
                .Upsert("txn.id", "123", SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr, SubdocDocFlags.InsertDocument)
                .Upsert("txn.ver", "v1", SubdocPathFlags.Xattr)
                .ExecuteAsync();

            Assert.IsTrue(mutateResult.Success);

            var getResult = await bucket.LookupIn<dynamic>(key)
                .Get("txn.id", SubdocPathFlags.Xattr)
                .Get("txn.ver", SubdocPathFlags.Xattr)
                .ExecuteAsync();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual("123", getResult.Content<string>(0));
            Assert.AreEqual("v1", getResult.Content<string>(1));
        }

        [Ignore("Requires additional logical delete server feature")]
        [TestCase(false)]
        [TestCase(true)]
        public void Can_Get_Deleted_Document_System_XATTR_Using_AccessDeleted_Subdoc_Flag(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            if (!SupportsXAttributes(bucket))
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Get_Deleted_Document_XATTR_Using_AccessDeleted_Subdoc_Flag";
            const string field = "_data.username";
            const string value = "jack";

            bucket.Remove(key);
            bucket.Upsert(key, new { name = "mike" });

            var mutateResult = bucket.MutateIn<dynamic>(key)
                .Upsert(field, value, SubdocPathFlags.Xattr | SubdocPathFlags.CreatePath)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var removeResult = bucket.Remove(key);

            Assert.IsTrue(removeResult.Success);

            var getResult = bucket.LookupIn<dynamic>(key)
                .Get(field, SubdocPathFlags.Xattr, SubdocDocFlags.AccessDeleted)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(value, getResult.Content<string>(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Can_Use_Server_Macro_To_Populate_XATTR(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            if (!SupportsXAttributes(bucket))
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            const string key = "Can_Use_Server_Macro_To_Populate_XATTR";
            const string field = "cas";
            const string value = "${Mutation.CAS}";

            bucket.Upsert(key, new {name = "mike"});

            var mutateResult = bucket.MutateIn<dynamic>(key)
                .Upsert(field, value, SubdocPathFlags.Xattr | SubdocPathFlags.ExpandMacroValues)
                .Execute();

            Assert.IsTrue(mutateResult.Success);

            var getResult = bucket.LookupIn<dynamic>(key)
                .Get(field, SubdocPathFlags.Xattr)
                .Execute();

            Assert.IsTrue(getResult.Success);
            Assert.IsFalse(string.IsNullOrEmpty(getResult.Content<string>(0)));
        }

        #endregion

        [Test]
        public async Task Can_upsert_full_doc_body()
        {
            var bucket = GetBucket(false);
            if (!SupportsXAttributes(bucket))
            {
                Assert.Ignore(XAttrsNotSupported);
            }

            var key = Guid.NewGuid().ToString();
            try
            {
                var result = await bucket.MutateIn<dynamic>(key)
                    .Upsert("key", "value", SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr)
                    .Upsert(new {name = "mike"}, SubdocDocFlags.UpsertDocument)
                    .ExecuteAsync();
                Assert.IsTrue(result.Success);
            }
            finally
            {
                //await bucket.RemoveAsync(key);
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_SingleMutate_Execute(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "Test_Multiple_SingleMutate_Execute";
            var document = new Document<dynamic>
            {
                Id = key,
                Content = new
                {
                    name = "Couchbase"
                }
            };

            bucket.Upsert(document);
            var mutator = bucket.MutateIn<dynamic>(key).Upsert("name", "Matt");

            mutator.Execute();
            var result = bucket.Get<dynamic>(key);
            Assert.AreEqual("Matt", result.Value.name.ToString());

            mutator.Execute();
            result = bucket.Get<dynamic>(key);
            Assert.AreEqual("Matt", result.Value.name.ToString());
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_MultipleMutate_Execute(bool useMutation)
        {
            var bucket = GetBucket(useMutation);

            var key = "Test_Multiple_MultiMutate_Execute";
            var document = new Document<dynamic>
            {
                Id = key,
                Content = new
                {
                    name = "Couchbase"
                }
            };

            bucket.Upsert(document);
            var mutator = bucket.MutateIn<dynamic>(key).Upsert("name", "Matt").Upsert("name", "John");

            mutator.Execute();
            var result = bucket.Get<dynamic>(key);
            Assert.AreEqual("John", result.Value.name.ToString());

            mutator.Execute();
            result = bucket.Get<dynamic>(key);
            Assert.AreEqual("John", result.Value.name.ToString());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_Counter_WithUpsert_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateInAsync_Counter_WithValidPathAndCreateParentsFalse_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo", count=0 });

            var builder = bucket.MutateIn<dynamic>(key)
                .Counter("baz", 1, false)
                .Upsert("foo", "bar2", false);

            var result = builder.Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(useMutation, result.Token.IsSet);
            Assert.AreEqual(1, result.Content("baz"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task MutateInAsync_Counter_WithUpsert_ReturnsSuccess(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            var key = "MutateInAsync_Counter_WithValidPathAndCreateParentsFalse_ReturnsSuccess";
            bucket.Upsert(key, new { foo = "bar", bar = "foo", count=0 });

            var builder = bucket.MutateIn<dynamic>(key)
                .Counter("baz", 1, false)
                .Upsert("foo", "bar2", false);

            var result = await builder.ExecuteAsync();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(useMutation, result.Token.IsSet);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MutateIn_with_multimutate_can_set_property_value_to_null(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            const string key = "MutateIn_with_multimutate_can_set_property_value_to_null";

            bucket.Upsert(key, new { });

            bucket.MutateIn<dynamic>(key)
                .Upsert("nullProperty", null)
                .Execute();

            var result = bucket.MutateIn<dynamic>(key)
                .Upsert("nullProperty", null)
                .Upsert("name", "MutatedName")
                .Execute();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(useMutation, result.Token.IsSet);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task MutateInAsync_with_multimutate_can_set_property_value_to_null(bool useMutation)
        {
            var bucket = GetBucket(useMutation);
            const string key = "MutateInAsync_with_multimutate_can_set_property_value_to_null";

            await bucket.UpsertAsync(key, new { });

            var result = await bucket.MutateIn<dynamic>(key)
                .Upsert("nullProperty", null)
                .Upsert("name", "MutatedName")
                .ExecuteAsync();

            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(useMutation, result.Token.IsSet);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster?.Dispose();
            _clusterWithMutationsTokens?.Dispose();
        }
    }
}
