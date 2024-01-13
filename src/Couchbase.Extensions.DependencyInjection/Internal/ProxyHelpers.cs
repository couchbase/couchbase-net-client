using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    internal static class ProxyHelpers
    {
        private static ConstructorInfo? _fromKeyedServicesConstructor;

        private static ConstructorInfo FromKeyedServicesConstructor
        {
            [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(FromKeyedServicesAttribute))]
            get => _fromKeyedServicesConstructor ??=
                typeof(FromKeyedServicesAttribute).GetConstructor([typeof(object)])!;
        }

        public static CustomAttributeBuilder CreateFromKeyedServicesAttribute(string serviceKey) =>
            new(FromKeyedServicesConstructor, [serviceKey]);
    }
}
