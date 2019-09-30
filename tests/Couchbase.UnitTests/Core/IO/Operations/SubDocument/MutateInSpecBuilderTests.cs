using System.Linq;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations.SubDocument
{
    public class MutateInSpecBuilderTests
    {
        [Fact]
        public void MutationMacro_Sets_Correct_Flags()
        {
            var builder = new MutateInSpecBuilder();
            builder.Upsert("", MutationMacro.Cas);

            Assert.True(builder.Specs.First().PathFlags.HasFlag(SubdocPathFlags.ExpandMacroValues));
            Assert.True(builder.Specs.First().PathFlags.HasFlag(SubdocPathFlags.Xattr));
        }
    }
}
