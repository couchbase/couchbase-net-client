using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.Diagnostics
{
    public interface IOperationTimer : IDisposable
    {
        ITimingStore Store { get; set; }
    }
}
