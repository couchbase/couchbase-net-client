using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;

namespace Couchbase.Tests.Fakes
{
    public class FauxBucket : IDisposable
    {
        public IResult<T> Upsert<T>(IDocument<T> document)
        {
            return new Mock<IResult<T>>().Object;
        }

        public IResult<T> GetDocument<T>(string id)
        {
            return new Mock<IResult<T>>().Object;
        }

        public IResult<T> Get<T>(string id) where T : class
        {
            return new Mock<IResult<T>>().Object;
        }

        public void Dispose()
        {
        }
    }
}
