using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    /// <summary>
    /// Generates proxy classes based on interfaces that inherit from <see cref="INamedBucketProvider"/>.
    /// </summary>
    internal class NamedBucketProxyGenerator
    {
        private static readonly AssemblyName DynamicAssemblyName =
            new AssemblyName("Couchbase.Extensions.DependencyInjection.Dynamic");

        private readonly object _lock = new object();
        private ModuleBuilder _moduleBuilder;

        private readonly ConcurrentDictionary<Type, Type> _proxyTypeCache = new ConcurrentDictionary<Type, Type>();

        public T GetProxy<T>(IBucketProvider bucketProvider, string bucketName)
            where T: class, INamedBucketProvider
        {
            var proxyType = _proxyTypeCache.GetOrAdd(typeof(T), CreateProxyType);

            return (T)Activator.CreateInstance(proxyType, bucketProvider, bucketName);
        }

        private Type CreateProxyType(Type interfaceType)
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

            var typeBuilder = _moduleBuilder.DefineType(interfaceType.Name, TypeAttributes.Class | TypeAttributes.Public,
                typeof(NamedBucketProvider));

            typeBuilder.AddInterfaceImplementation(interfaceType);

            var parameterTypes = new[] {typeof(IBucketProvider), typeof(string)};
            var baseConstructor = typeof(NamedBucketProvider).GetConstructor(parameterTypes);

            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard | CallingConventions.HasThis, parameterTypes);

            var ilGenerator = constructorBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0); // push "this"
            ilGenerator.Emit(OpCodes.Ldarg_1); // push the I
            ilGenerator.Emit(OpCodes.Ldarg_2); // push the param
            ilGenerator.Emit(OpCodes.Call, baseConstructor);
            ilGenerator.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo().AsType();
        }
    }
}
