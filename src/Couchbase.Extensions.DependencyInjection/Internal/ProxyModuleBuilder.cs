using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Resources;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    internal class ProxyModuleBuilder
    {
        public static ProxyModuleBuilder Instance { get; } =
            new ProxyModuleBuilder();

        private readonly object _lock = new object();
        private ModuleBuilder? _moduleBuilder;

        // Starting with .NET 6 we don't even have the ability to sign our dynamic assembly anymore, it will cause exceptions if we try.
        // However, it hasn't really be required for longer. So we just disable starting with .NET 5 for simplicity.
        // We also don't bother signing the dynamic assembly for local development where none of the projects are signed.

        private static readonly AssemblyName DynamicAssemblyName =
#if SIGNING && !NET5_0_OR_GREATER
            new AssemblyName("Couchbase.Extensions.DependencyInjection.Dynamic, PublicKeyToken=9112ac8688e923b2")
            {
                KeyPair = new StrongNameKeyPair(GetKeyPair())
            };
#else
            new AssemblyName("Couchbase.Extensions.DependencyInjection.Dynamic");
#endif

        [RequiresDynamicCode(ServiceCollectionExtensions.RequiresDynamicCodeWarning)]
        public ModuleBuilder GetModuleBuilder()
        {
            if (_moduleBuilder == null)
            {
                lock (_lock)
                {
                    if (_moduleBuilder == null)
                    {
                        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(DynamicAssemblyName,
                            AssemblyBuilderAccess.Run);
                        _moduleBuilder = assemblyBuilder.DefineDynamicModule("Module");
                    }
                }
            }

            return _moduleBuilder;
        }

#if SIGNING && !NET5_0_OR_GREATER
        private static byte[] GetKeyPair()
        {
            using var stream =
                typeof(NamedBucketProxyGenerator).Assembly.GetManifestResourceStream(
                    "Couchbase.Extensions.DependencyInjection.Dynamic.snk");

            if (stream == null)
            {
                throw new MissingManifestResourceException("Resource 'Couchbase.Extensions.DependencyInjection.Dynamic.snk' not found.");
            }

            var keyLength = (int)stream.Length;
            var result = new byte[keyLength];
            stream.Read(result, 0, keyLength);
            return result;
        }
#endif
    }
}
