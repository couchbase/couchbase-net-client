using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Couchbase SDK 2.0")]
[assembly: AssemblyDescription("The official .NET SDK for Couchbase Server")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Couchbase Inc.")]
[assembly: AssemblyProduct("Couchbase")]
[assembly: AssemblyCopyright("Copyright ©  2014")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("11f64121-1774-42f2-ba1a-c79e1f2d8896")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("0.0.0.0")]
[assembly: AssemblyFileVersion("0.0.0.0")]

[assembly: InternalsVisibleTo("Couchbase.Tests")]
[assembly: InternalsVisibleTo("Couchbase.IntegrationTests")]
[assembly: InternalsVisibleTo("Couchbase.IntegrationTests.NetStandard")]
[assembly: InternalsVisibleTo("Couchbase.UnitTests")]
[assembly: InternalsVisibleTo("Couchbase.UnitTests.NetStandard")]
[assembly: InternalsVisibleTo("Couchbase.Linq.Tests")]
[assembly: InternalsVisibleTo("Couchbase.Linq")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

#if NET45
[assembly: InternalsVisibleTo("Sdkd")]
[assembly: InternalsVisibleTo("SdkdConsole")]
#else
[assembly: InternalsVisibleTo("Sdkd.NetStandard")]
[assembly: InternalsVisibleTo("SdkdConsole.NetStandard")]
#endif
