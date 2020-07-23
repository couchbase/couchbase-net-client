using System.Runtime.CompilerServices;

#if SIGNING
// These internals must be exposed in production builds to support dynamic proxy generation
[assembly: InternalsVisibleTo("Couchbase.Extensions.DependencyInjection.Dynamic, PublicKey=05e9c6b5a9ec94c2")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=05e9c6b5a9ec94c2")]
#else
[assembly: InternalsVisibleTo("Couchbase.Extensions.DependencyInjection.Dynamic")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#endif

#if !SIGNING
[assembly: InternalsVisibleTo("Couchbase.Extensions.DependencyInjection.UnitTests")]
#endif
