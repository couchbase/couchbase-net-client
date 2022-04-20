using System.Runtime.CompilerServices;

// We are not relying on InternalsVisibleToAttribute in recent frameworks to allow our dynamic assembly to access this one.
// Instead, we're relying on the IgnoresAccessChecksToAttribute which is added to the dynamic assembly.
#if !NET5_0_OR_GREATER

#if SIGNING
// These internals must be exposed in production builds to support dynamic proxy generation
// Public key for Couchbase.Extensions.DependencyInjection.Dynamic is from Dynamic.snk
[assembly: InternalsVisibleTo("Couchbase.Extensions.DependencyInjection.Dynamic, PublicKey=0024000004800000940000000602000000240000525341310004000001000100a5211350f9b5b7c875987bbe043da4bdc64c1dc8e1b5b053cbef0823ff1fedbdfe109873da2f07b1a3dee854b1f308a8daabe5b4e2007d708c994df291a0aa7e0604a9cb957d47d88b3702dcb71318544645fc73a9c2cbf9c173e17cb105303903ee8601de8433063094fc5d6bed63e3b4cd6b7663fde7366bf1916cfb9a6bc0")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]
#else
[assembly: InternalsVisibleTo("Couchbase.Extensions.DependencyInjection.Dynamic")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#endif

#endif

#if !SIGNING
[assembly: InternalsVisibleTo("Couchbase.Extensions.DependencyInjection.UnitTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#endif
