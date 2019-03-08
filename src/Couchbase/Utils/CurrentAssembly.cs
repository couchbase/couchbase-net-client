using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Couchbase.Utils
{
    internal static class CurrentAssembly
    {
        public static readonly Assembly Current = typeof (CurrentAssembly).GetTypeInfo().Assembly;
        public static readonly Version  Version = Current.GetName().Version;
    }
}
