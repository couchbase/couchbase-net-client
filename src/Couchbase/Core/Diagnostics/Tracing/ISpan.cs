using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.Diagnostics.Tracing
{
    public interface ISpan
    {
        void Finish();
    }
}
