using System;
using Couchbase.Core.IO.Operations;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class MutateInSpecBuilderTests
    {
        [Fact]
        public void Insert_Spec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder();

            // Act

            builder.Insert("prop", fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("prop", spec.Path);
            Assert.Equal(OpCode.SubDictAdd, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void Upsert_Spec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder();

            // Act

            builder.Upsert("prop", fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("prop", spec.Path);
            Assert.Equal(OpCode.SubDictUpsert, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void Upsert_WithDynamicExpression_AddsSpec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder();

            // Act

            builder.Upsert("dynamic", fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("dynamic", spec.Path);
            Assert.Equal(OpCode.SubDictUpsert, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void Replace_Spec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder();

            // Act

            builder.Replace("prop", fakeValue);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("prop", spec.Path);
            Assert.Equal(OpCode.SubReplace, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
        }

        [Fact]
        public void Remove_Spec()
        {
            // Arrange

            var builder = new MutateInSpecBuilder();

            // Act

            builder.Remove("prop");

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("prop", spec.Path);
            Assert.Equal(OpCode.SubDelete, spec.OpCode);
        }

        [Fact]
        public void ArrayAppend_Spec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder();

            // Act

            builder.ArrayAppend("array", fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("array", spec.Path);
            Assert.Equal(OpCode.SubArrayPushLast, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void ArrayPrepend_Spec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder();

            // Act

            builder.ArrayPrepend("array", fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("array", spec.Path);
            Assert.Equal(OpCode.SubArrayPushFirst, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void ArrayInsert_Spec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder();

            // Act

            builder.ArrayInsert("array[3]", fakeValue);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("array[3]", spec.Path);
            Assert.Equal(OpCode.SubArrayInsert, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
        }

        [Fact]
        public void AddUnique_Spec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder();

            // Act

            builder.ArrayAddUnique("array", fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("array", spec.Path);
            Assert.Equal(OpCode.SubArrayAddUnique, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        [Obsolete()]
        public void Increment_Signed_Spec()
        {
            // Arrange

            var fakeDelta = 123L;

            var builder = new MutateInSpecBuilder();

            // Act

            builder.Increment("prop", fakeDelta, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("prop", spec.Path);
            Assert.Equal(OpCode.SubCounter, spec.OpCode);
            Assert.Equal(fakeDelta, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void Increment_Unsigned_Spec()
        {
            // Arrange

            var fakeDelta = 123UL;

            var builder = new MutateInSpecBuilder();

            // Act

            builder.Increment("prop", fakeDelta, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("prop", spec.Path);
            Assert.Equal(OpCode.SubCounter, spec.OpCode);
            Assert.Equal(fakeDelta, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        [Obsolete()]
        public void Decrement_Signed_Spec()
        {
            // Arrange

            var fakeDelta = 123L;

            var builder = new MutateInSpecBuilder();

            // Act

            builder.Decrement("prop", fakeDelta, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("prop", spec.Path);
            Assert.Equal(OpCode.SubCounter, spec.OpCode);
            Assert.Equal(-fakeDelta, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void Decrement_Unsigned_Spec()
        {
            // Arrange

            var fakeDelta = 123UL;

            var builder = new MutateInSpecBuilder();

            // Act

            builder.Decrement("prop", fakeDelta, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("prop", spec.Path);
            Assert.Equal(OpCode.SubCounter, spec.OpCode);
            Assert.Equal(-(long)fakeDelta, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        [Obsolete()]
        public void Increment_Signed_Negative_Spec()
        {
            // Arrange

            var fakeDelta = -123L;

            var builder = new MutateInSpecBuilder();

            // Act

            builder.Increment("prop", fakeDelta, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("prop", spec.Path);
            Assert.Equal(OpCode.SubCounter, spec.OpCode);
            Assert.Equal(fakeDelta, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        [Obsolete()]
        public void Decrement_Signed_Negative_Spec()
        {
            // Arrange

            var fakeDelta = -123L;

            var builder = new MutateInSpecBuilder();

            // Act

            builder.Decrement("prop", fakeDelta, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("prop", spec.Path);
            Assert.Equal(OpCode.SubCounter, spec.OpCode);
                // Here an obsolete defect on signed long Spec is asserted for inverse of a negative delta not applied
            Assert.Equal(fakeDelta, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }
    }
}
