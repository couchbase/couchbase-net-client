using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Couchbase.Tests.Mocks
{
    public class FakeView : IEnumerable<IViewRow>
    {
        private CouchbaseViewHandler _handler;
        private Stream _stream;

        internal FakeView(CouchbaseViewHandler handler, Stream stream)
        {
            _handler = handler;
            _stream = stream;
        }

        public IEnumerator<IViewRow> GetEnumerator()
        {
            return _handler.ReadResponse(_stream, jr => new FakeViewRow(Json.Parse(jr) as IDictionary<string, object>));
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}