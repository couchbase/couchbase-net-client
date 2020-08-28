using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("a877a9c8-b003-44de-bd70-8bb90b1b9f9e")]

#if !SIGNING
[assembly: InternalsVisibleTo("Couchbase.Test.Common")]
[assembly: InternalsVisibleTo("Couchbase.UnitTests")]
[assembly: InternalsVisibleTo("Couchbase.LoadTests")]
[assembly: InternalsVisibleTo("Couchbase.IntegrationTests")]
[assembly: InternalsVisibleTo("Couchbase.IntegrationTests.Management")]
[assembly: InternalsVisibleTo("Sdkd")]
[assembly: InternalsVisibleTo("SdkdConsole")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#endif
