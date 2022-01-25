using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: ComVisible(false)]

#if !SIGNING
[assembly: InternalsVisibleTo("Couchbase.Extensions.OpenTelemetry.UnitTests")]
#endif
