
using System.Collections;
using System.Collections.Generic;

namespace Couchbase.Management.Indexes
{
    public class IndexResult : DefaultResult<List<IndexInfo>>, IEnumerable<IndexInfo>
    {
        public IndexResult()
        {
            Value = new List<IndexInfo>();
        }

        public IEnumerator<IndexInfo> GetEnumerator()
        {
            return ((IEnumerable<IndexInfo>) Value).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
