using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class LookupInSpecBuilderExtensionsTests
    {
        [Fact]
        public void Get_WithExpression_AddsSpec()
        {
            // Arrange

            var builder = new LookupInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.Get(p => p.Prop);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`prop`", spec.Path);
            Assert.Equal(OpCode.SubGet, spec.OpCode);
        }

        [Fact]
        public void Get_WithDynamicExpression_AddsSpec()
        {
            // Arrange

            var builder = new LookupInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.Get(p => p.Dynamic);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`dynamic`", spec.Path);
            Assert.Equal(OpCode.SubGet, spec.OpCode);
        }

        [Fact]
        public void Exists_WithExpression_AddsSpec()
        {
            // Arrange

            var builder = new LookupInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.Exists(p => p.Prop);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`prop`", spec.Path);
            Assert.Equal(OpCode.SubExist, spec.OpCode);
        }

        [Fact]
        public void Count_WithExpression_AddsSpec()
        {
            // Arrange

            var builder = new LookupInSpecBuilder<MyDoc>(new DefaultSerializer());

            // Act

            builder.Count(p => p.Array);

            // Assert

            var spec = Assert.Single(builder.Specs);
            Assert.NotNull(spec);
            Assert.Equal("`array`", spec.Path);
            Assert.Equal(OpCode.SubGetCount, spec.OpCode);
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
