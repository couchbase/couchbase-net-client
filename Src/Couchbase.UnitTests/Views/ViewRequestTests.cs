using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.UnitTests.Views
{
    [TestFixture]
    public class ViewRequestTests
    {
        [Test]
        public void Can_Set_IsStreaming()
        {
            var query = new ViewQuery();
            Assert.IsFalse(query.IsStreaming);
            query.UseStreaming(true);
            Assert.IsTrue(query.IsStreaming);
        }
    }
}
