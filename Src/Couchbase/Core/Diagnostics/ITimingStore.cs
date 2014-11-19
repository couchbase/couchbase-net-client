using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.Diagnostics
{
    public interface ITimingStore
    {
        void Write(string format, params object[] args);

        bool Enabled { get; }
    }
}
