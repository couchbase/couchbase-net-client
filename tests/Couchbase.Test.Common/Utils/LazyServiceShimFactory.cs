using System;
using Couchbase.Core.DI;
using Moq;

#nullable enable

namespace Couchbase.Test.Common.Utils
{
    public static class LazyServiceShimFactory
    {
        internal static LazyService<T> Create<T>(T? mockInstance)
            where T : notnull
        {
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(p => p.GetService(typeof(T)))
                // Null is a legitimate argument here: the shim exists to simulate a service
                // that is not registered, and Moq stores the null without dereferencing it.
                .Returns(mockInstance!);

            return new LazyService<T>(serviceProvider.Object);
        }
    }
}
