using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    /// <summary>
    /// Generates proxy classes based on interfaces that inherit from <see cref="INamedCollectionProvider"/>.
    /// </summary>
    internal class NamedCollectionProxyGenerator
    {
        public static NamedCollectionProxyGenerator Instance { get; } = new(ProxyModuleBuilder.Instance);

        private readonly ProxyModuleBuilder _proxyModuleBuilder;

        private readonly
            ConcurrentDictionary<(Type collectionProviderType, Type bucketProviderType, string scopeName, string collectionName),
                Lazy<Type>> _proxyTypeCache = new();

        public NamedCollectionProxyGenerator(ProxyModuleBuilder proxyModuleBuilder)
        {
            _proxyModuleBuilder = proxyModuleBuilder ?? throw new ArgumentNullException(nameof(proxyModuleBuilder));
        }

        public Type GetProxy(Type collectionProviderType, Type bucketProviderType, string scopeName, string collectionName) =>
            _proxyTypeCache.GetOrAdd((collectionProviderType, bucketProviderType, scopeName, collectionName),
                args =>
                {
                    // This factory method may be called more than once if two callers hit GetOrAdd simultaneously
                    // with the same key. So we further wrap in a Lazy<T> to ensure we don't try to create the proxy twice.

                    return new Lazy<Type>(
                        () => CreateProxyType(args.collectionProviderType, args.bucketProviderType, args.scopeName,
                            args.collectionName),
                        LazyThreadSafetyMode.ExecutionAndPublication);
                }).Value;

#if NET5_0_OR_GREATER
        // Make our use of reflection safe for trimming
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(NamedCollectionProvider))]
#endif
        private Type CreateProxyType(Type collectionProviderType, Type bucketProviderType, string scopeName, string collectionName)
        {
            var moduleBuilder = _proxyModuleBuilder.GetModuleBuilder();

            var typeBuilder = moduleBuilder.DefineType($"{collectionProviderType.Name}+{scopeName}+{collectionName}", TypeAttributes.Class | TypeAttributes.Public,
                typeof(NamedCollectionProvider));

            typeBuilder.AddInterfaceImplementation(collectionProviderType);

            var baseConstructor = typeof(NamedCollectionProvider).GetConstructor(
                new[] { typeof(INamedBucketProvider), typeof(string), typeof(string) });

            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard | CallingConventions.HasThis,
                new[] { bucketProviderType });

            var ilGenerator = constructorBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0); // push "this"
            ilGenerator.Emit(OpCodes.Ldarg_1); // push the bucketProvider param
            ilGenerator.Emit(OpCodes.Ldstr, scopeName); // push the scopeName param
            ilGenerator.Emit(OpCodes.Ldstr, collectionName); // push the collectionName param
            ilGenerator.Emit(OpCodes.Call, baseConstructor!);
            ilGenerator.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo()!.AsType();
        }
    }
}
