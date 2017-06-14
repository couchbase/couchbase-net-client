using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.N1QL;
using NUnit.Framework;

namespace Couchbase.UnitTests.N1Ql
{
    [TestFixture]
    public class QueryRequestTests
    {
        #region GetFormValues

        [Test]
        public void GetFormValues_NoPrettyCall_NoPrettyParam()
        {
            // Arrange

            var request = new QueryRequest("SELECT * FROM default");

            // Act

            var fields = request.GetFormValues();

            // Assert

            Assert.False(fields.Keys.Contains("pretty"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void GetFormValues_PrettyCall_IncludesParam(bool pretty)
        {
            // Arrange

            var request = new QueryRequest("SELECT * FROM default")
                .Pretty(pretty);

            // Act

            var fields = request.GetFormValues();

            // Assert

            Assert.True(fields.Keys.Contains("pretty"));
            Assert.AreEqual(pretty, fields["pretty"]);
        }

        #endregion
    }
}
