using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    /// <summary>
    /// Generates proxy classes based on interfaces that inherit from <see cref="INamedBucketProvider"/>.
    /// </summary>
    internal class NamedBucketProxyGenerator
    {
        public static NamedBucketProxyGenerator Instance { get; } = new(ProxyModuleBuilder.Instance);

        private readonly ProxyModuleBuilder _proxyModuleBuilder;
        private readonly ConcurrentDictionary<(Type type, string bucketName), Lazy<Type>> _proxyTypeCache = new();

        public NamedBucketProxyGenerator(ProxyModuleBuilder proxyModuleBuilder)
        {
            _proxyModuleBuilder = proxyModuleBuilder ?? throw new ArgumentNullException(nameof(proxyModuleBuilder));
        }

        public Type GetProxy(Type bucketProviderInterface, string bucketName) =>
            _proxyTypeCache.GetOrAdd((bucketProviderInterface, bucketName), args =>
            {
                // This factory method may be called more than once if two callers hit GetOrAdd simultaneously
                // with the same key. So we further wrap in a Lazy<T> to ensure we don't try to create the proxy twice.

                return new Lazy<Type>(() => CreateProxyType(args.type, args.bucketName),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            }).Value;

#if NET5_0_OR_GREATER
        // Make our use of reflection safe for trimming
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(NamedBucketProvider))]
#endif
        private Type CreateProxyType(Type interfaceType, string bucketName)
        {
            var moduleBuilder = _proxyModuleBuilder.GetModuleBuilder();

            var typeBuilder = moduleBuilder.DefineType($"{interfaceType.Name}+{bucketName}", TypeAttributes.Class | TypeAttributes.Public,
                typeof(NamedBucketProvider));

            typeBuilder.AddInterfaceImplementation(interfaceType);

            var baseConstructor = typeof(NamedBucketProvider).GetConstructor(
                new[] { typeof(IBucketProvider), typeof(string) });

            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard | CallingConventions.HasThis,
                new[] { typeof(IBucketProvider) });

            var ilGenerator = constructorBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0); // push "this"
            ilGenerator.Emit(OpCodes.Ldarg_1); // push the IBucketProvider
            ilGenerator.Emit(OpCodes.Ldstr, bucketName); // push the bucketName
            ilGenerator.Emit(OpCodes.Call, baseConstructor!);
            ilGenerator.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo()!.AsType();
        }
    }
}
