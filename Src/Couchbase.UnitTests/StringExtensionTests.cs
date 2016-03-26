using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class StringExtensionTests
    {
        [Test]
        public void N1QlEscape_PadRight_Success()
        {
            var expected = "`thestring`";

            Assert.AreEqual(expected, "thestring`".N1QlEscape());
        }

        [Test]
        public void N1QlEscape_PadLeft_Success()
        {
            var expected = "`thestring`";

            Assert.AreEqual(expected, "`thestring".N1QlEscape());
        }

        [Test]
        public void N1QlEscape_PadBoth_Success()
        {
            var expected = "`thestring`";

            Assert.AreEqual(expected, "thestring".N1QlEscape());
        }

        [Test]
        public void N1QlEscape_PadNone_Success()
        {
            var expected = "`thestring`";

            Assert.AreEqual(expected, "`thestring`".N1QlEscape());
        }
    }
}
