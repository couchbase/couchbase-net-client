using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Couchbase.UnitTests.Helpers;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class MutateInResultTests
    {
        [Fact]
        public async Task When_Result_Contains_Values_AllowRead()
        {
            var bytes = new byte[]
            {
                129, 209, 0, 0, 0, 0, 0, 0, 0, 0, 0, 18, 0, 0, 0, 16, 21, 240,
                165, 234, 46, 107, 0, 0, 3, 0, 0, 0, 0, 0, 2, 49, 48, 4, 0, 0,
                0, 0, 0, 2, 45, 53
            };

            var specs = new MutateInSpecBuilder().
                Upsert("name", "mike").
                Replace("bar", "bar").
                Insert("bah", 0).
                Increment("zzz", 10, true).
                Decrement("xxx", 5, true).Specs;

            var op = new MultiMutation<byte[]>
            {
                Builder = new MutateInBuilder<byte[]>(null, null, "thekey", specs)
            };
            await op.SendAsync(new Mock<IConnection>().Object).ConfigureAwait(false);
            op.Read(new FakeMemoryOwner<byte>(bytes), null);

            var result = new MutateInResult(op.GetCommandValues(), 0, MutationToken.Empty, new DefaultSerializer());

            Assert.Equal(0, result.ContentAs<int>(0));
            Assert.Equal(0, result.ContentAs<int>(1));
            Assert.Equal(0, result.ContentAs<int>(2));
            Assert.Equal(10, result.ContentAs<int>(3));
            Assert.Equal(-5, result.ContentAs<int>(4));
        }
    }
}
