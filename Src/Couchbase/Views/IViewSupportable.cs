using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    public interface IViewSupportable
    {
        IViewResult<T> Get<T>(IViewQuery query);
    }
}
