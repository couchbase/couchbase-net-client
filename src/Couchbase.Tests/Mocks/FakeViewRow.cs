using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Tests.Mocks
{
    public class FakeViewRow : IViewRow
    {
        private readonly object[] _key;
        private readonly string _id;
        private readonly IDictionary<string, object> _info;

        public FakeViewRow(IDictionary<string, object> info)
        {
            if (!info.TryGetValue("id", out _id))
            {
                _id = null;
            }

            object tempKey;
            if (!info.TryGetValue("key", out tempKey))
            {
                throw new InvalidOperationException("The value 'key' was not found in the info definition.");
            }

            _key = (tempKey as object[]) ?? (new[] { tempKey });
            _info = info.AsReadOnly();
        }

        public object[] ViewKey
        {
            get { return _key; }
        }

        public string ItemId
        {
            get { return _id; }
        }

        public IDictionary<string, object> Info
        {
            get { return _info; }
        }

        public object GetItem()
        {
            throw new NotImplementedException();
        }
    }
}
