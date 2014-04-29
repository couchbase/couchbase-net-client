using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    /// <summary>
    /// Adds support for querying Couchbase Views.
    /// </summary>
    /// <see cref="http://docs.couchbase.com/couchbase-manual-2.5/cb-admin/#views-and-indexes"/>
    public interface IViewSupportable
    {
        IViewResult<T> Get<T>(IViewQuery query);

        IViewQuery CreateQuery(bool development);

        IViewQuery CreateQuery(string designdoc, bool development);

        IViewQuery CreateQuery(string designdoc, string view, bool development);
    }
}
