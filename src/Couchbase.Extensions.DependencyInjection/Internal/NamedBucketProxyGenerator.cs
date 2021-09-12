using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    /// <summary>
    /// Generates proxy classes based on interfaces that inherit from <see cref="INamedBucketProvider"/>.
    /// </summary>
    internal class NamedBucketProxyGenerator
    {
        public static NamedBucketProxyGenerator Instance { get; } = new(ProxyModuleBuilder.Instance);

        private readonly ProxyModuleBuilder _proxyModuleBuilder;
        private readonly Dictionary<(Type type, string bucketName), Type> _proxyTypeCache = new();

        public NamedBucketProxyGenerator(ProxyModuleBuilder proxyModuleBuilder)
        {
            _proxyModuleBuilder = proxyModuleBuilder ?? throw new ArgumentNullException(nameof(proxyModuleBuilder));
        }

        public Type GetProxy(Type bucketProviderInterface, string bucketName)
        {
            if (!_proxyTypeCache.TryGetValue((bucketProviderInterface, bucketName), out var proxyType))
            {
                proxyType = CreateProxyType(bucketProviderInterface, bucketName);
                _proxyTypeCache.Add((bucketProviderInterface, bucketName), proxyType);
            }

            return proxyType;
        }

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
