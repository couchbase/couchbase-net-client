using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    internal static class CurrentAssembly
    {
        public static readonly Assembly Current = typeof (CurrentAssembly).Assembly;
        public static readonly Version  Version = Current.GetName().Version;
    }
}
