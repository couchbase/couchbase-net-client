using System.Runtime.CompilerServices;

// These internals must be exposed in production builds to support dynamic proxy generation
[assembly: InternalsVisibleTo("Couchbase.Extensions.DependencyInjection.Dynamic")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

#if !SIGNING
[assembly: InternalsVisibleTo("Couchbase.Extensions.DependencyInjection.UnitTests")]
#endif
