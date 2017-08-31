using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.N1QL;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Couchbase.UnitTests.N1Ql
{
    [TestFixture]
    public class QueryRequestExtensionTests
    {
        [TestCase(4040, QueryStatus.Timeout)]
        [TestCase(4050, QueryStatus.Timeout)]
        [TestCase(4070, QueryStatus.Timeout)]
        [TestCase(5000, QueryStatus.Timeout)]
        [TestCase(4040, QueryStatus.Fatal)]
        [TestCase(4050, QueryStatus.Fatal)]
        [TestCase(4070, QueryStatus.Fatal)]
        [TestCase(5000, QueryStatus.Fatal)]
        public void When_Status_Is_Fatal_And_Code_Is_Retriable_Return_True(int code, QueryStatus status)
        {
            var response = new QueryResult<dynamic>
            {
                Status = status,
                Errors = new List<Error>
                {
                    new Error
                    {
                        Code = code
                    }
                }
            };

            Assert.IsTrue(response.IsQueryPlanStale());
        }

        [TestCase(2000, QueryStatus.Success)]
        [TestCase(2000, QueryStatus.Completed)]
        [TestCase(4070, QueryStatus.Errors)]
        [TestCase(5000, QueryStatus.Running)]
        [TestCase(4040, QueryStatus.Stopped)]
        public void When_Status_Is_Fatal_And_Code_Is_Not_Retriable_Return_False(int code, QueryStatus status)
        {
            var response = new QueryResult<dynamic>
            {
                Status = status,
                Errors = new List<Error>
                {
                    new Error
                    {
                        Code = code
                    }
                }
            };

            Assert.IsFalse(response.IsQueryPlanStale());
        }
    }
}
