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
        private readonly ConcurrentDictionary<BucketKey, Lazy<Type>> _proxyTypeCache = new();

        public NamedBucketProxyGenerator(ProxyModuleBuilder proxyModuleBuilder)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (proxyModuleBuilder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(proxyModuleBuilder));
            }

            _proxyModuleBuilder = proxyModuleBuilder;
        }

        [RequiresDynamicCode(ServiceCollectionExtensions.RequiresDynamicCodeWarning)]
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2073",
            Justification = "Proxy type is dynamically generated")]
        public Type GetProxy([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type bucketProviderInterface,
            string? serviceKey, string bucketName) =>
            _proxyTypeCache.GetOrAdd(new BucketKey(bucketProviderInterface, serviceKey, bucketName), args =>
            {
                // This factory method may be called more than once if two callers hit GetOrAdd simultaneously
                // with the same key. So we further wrap in a Lazy<T> to ensure we don't try to create the proxy twice.

                return new Lazy<Type>(() => CreateProxyType(args.InterfaceType, args.ServiceKey, args.Name),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            }).Value;

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(NamedBucketProvider))]
        [RequiresDynamicCode(ServiceCollectionExtensions.RequiresDynamicCodeWarning)]
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        private Type CreateProxyType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type interfaceType,
            string? serviceKey, string bucketName)
        {
            var moduleBuilder = _proxyModuleBuilder.GetModuleBuilder();

            var typeName = serviceKey is null
                ? $"{interfaceType.Name}+{bucketName}"
                : $"{serviceKey}+{interfaceType.Name}+{bucketName}";
            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public,
                typeof(NamedBucketProvider));

            typeBuilder.AddInterfaceImplementation(interfaceType);

            var baseConstructor = typeof(NamedBucketProvider).GetConstructor(
                [typeof(IBucketProvider), typeof(string)]);

            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard | CallingConventions.HasThis,
                [typeof(IBucketProvider)]);

            var parameterBuilder = constructorBuilder.DefineParameter(1, ParameterAttributes.None, "bucketProvider");
            if (serviceKey is not null)
            {
                parameterBuilder.SetCustomAttribute(ProxyHelpers.CreateFromKeyedServicesAttribute(serviceKey));
            }

            var ilGenerator = constructorBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0); // push "this"
            ilGenerator.Emit(OpCodes.Ldarg_1); // push the IBucketProvider
            ilGenerator.Emit(OpCodes.Ldstr, bucketName); // push the bucketName
            ilGenerator.Emit(OpCodes.Call, baseConstructor!);
            ilGenerator.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo()!.AsType();
        }

        private readonly record struct BucketKey(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type InterfaceType,
            string? ServiceKey,
            string Name)
        {
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
            public Type InterfaceType { get; } = InterfaceType;
        }
    }
}
