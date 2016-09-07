using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Couchbase.Views;

namespace Couchbase.Tests.Views
{
    [TestFixture]
    public class ViewResultTests
    {
        [Test]
        public void Test_View_Result_Data_Set()
        {
            var resultData = new ViewResultData<dynamic>
            {
                error = "testError",
                reason = "testReason",
                total_rows = 1,
                rows = new []{new ViewRowData<dynamic> { id = "testViewRowId", key = "testKey", geometry = "testGeometry", value = "testValue"} },
            };

            var result = resultData.ToViewResult();

            Assert.AreEqual(result.Error, resultData.error);
            Assert.AreEqual(result.Message, resultData.reason);
            Assert.AreEqual(result.TotalRows, resultData.total_rows);
            Assert.AreEqual(result.Rows.Count(), resultData.rows.Count());
            Assert.IsTrue(
                result.Rows.All(
                    r =>
                        resultData.rows.Any(
                            rd => r.Id == rd.id && r.Key == rd.key && r.Geometry == rd.geometry && r.Value == rd.value)));
        }
    }
}
