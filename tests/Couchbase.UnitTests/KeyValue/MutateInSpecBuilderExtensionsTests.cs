using System;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class MutateInSpecBuilderExtensionsTests
    {
        [Fact]
        public void Insert_WithExpression_AddsSpec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.Insert(p => p.Prop, fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`prop`", spec.Path);
            Assert.Equal(OpCode.SubDictAdd, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void Upsert_WithExpression_AddsSpec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.Upsert(p => p.Prop, fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`prop`", spec.Path);
            Assert.Equal(OpCode.SubDictUpsert, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void Upsert_WithDynamicExpression_AddsSpec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.Upsert(p => p.Dynamic, fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`dynamic`", spec.Path);
            Assert.Equal(OpCode.SubDictUpsert, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void Replace_WithExpression_AddsSpec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.Replace(p => p.Prop, fakeValue);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`prop`", spec.Path);
            Assert.Equal(OpCode.SubReplace, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
        }

        [Fact]
        public void Remove_WithExpression_AddsSpec()
        {
            // Arrange

            var builder = new MutateInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.Remove(p => p.Prop);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`prop`", spec.Path);
            Assert.Equal(OpCode.SubDelete, spec.OpCode);
        }

        [Fact]
        public void ArrayAppend_WithExpression_AddsSpec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.ArrayAppend(p => p.Array, fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`array`", spec.Path);
            Assert.Equal(OpCode.SubArrayPushLast, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void ArrayPrepend_WithExpression_AddsSpec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.ArrayPrepend(p => p.Array, fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`array`", spec.Path);
            Assert.Equal(OpCode.SubArrayPushFirst, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void ArrayInsert_WithExpression_AddsSpec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.ArrayInsert(p => p.Array[3], fakeValue);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`array`[3]", spec.Path);
            Assert.Equal(OpCode.SubArrayInsert, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
        }

        [Fact]
        public void AddUnique_WithExpression_AddsSpec()
        {
            // Arrange

            var fakeValue = "abc";

            var builder = new MutateInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.ArrayAddUnique(p => p.Array, fakeValue, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`array`", spec.Path);
            Assert.Equal(OpCode.SubArrayAddUnique, spec.OpCode);
            Assert.Equal(fakeValue, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void Increment_WithExpression_AddsSpec()
        {
            // Arrange

            var fakeDelta = 123L;

            var builder = new MutateInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.Increment(p => p.Prop, fakeDelta, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`prop`", spec.Path);
            Assert.Equal(OpCode.SubCounter, spec.OpCode);
            Assert.Equal(fakeDelta, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        [Fact]
        public void Decrement_WithExpression_AddsSpec()
        {
            // Arrange

            var fakeDelta = 123L;

            var builder = new MutateInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.Decrement(p => p.Prop, fakeDelta, true);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`prop`", spec.Path);
            Assert.Equal(OpCode.SubCounter, spec.OpCode);
            Assert.Equal(-fakeDelta, spec.Value);
            Assert.Equal(SubdocPathFlags.CreatePath, spec.PathFlags);
        }

        #region Helpers

        public class MyDoc
        {
            public string Prop { get; set; }
            public string[] Array { get; set; }
            public dynamic Dynamic { get; set; }
        }

        #endregion
    }
}
