using System;
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

        private static readonly AssemblyName DynamicAssemblyName =
#if SIGNING
            new AssemblyName("Couchbase.Extensions.DependencyInjection.Dynamic, PublicKeyToken=9112ac8688e923b2")
            {
                KeyPair = new StrongNameKeyPair(GetKeyPair())
            };
#else
            new AssemblyName("Couchbase.Extensions.DependencyInjection.Dynamic");
#endif

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

                        IgnoresAccessChecksToAttributeGenerator.Generate(assemblyBuilder, _moduleBuilder);
                    }
                }
            }

            return _moduleBuilder;
        }

#if SIGNING
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
