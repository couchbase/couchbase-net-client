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

            // NCBC-2601: specifying isXattr: true in combination with MutationMacro resulted in isXattr flipping to false.
            //            MutationMacro is *only* valid with isXattr: true
            builder.Upsert("foo", MutationMacro.Cas, isXattr: true);

            // Making this throw an ArgumentException would require changing the method signature from bool to bool?, which
            // would be a breaking change. We leave it as silently ignoring the false value.
            builder.Upsert("bar", MutationMacro.Cas, isXattr: false);

            Assert.True(builder.Specs.TrueForAll(spec => spec.PathFlags.HasFlag(SubdocPathFlags.ExpandMacroValues)));
            Assert.True(builder.Specs.TrueForAll(spec => spec.PathFlags.HasFlag(SubdocPathFlags.Xattr)));
        }
    }
}
