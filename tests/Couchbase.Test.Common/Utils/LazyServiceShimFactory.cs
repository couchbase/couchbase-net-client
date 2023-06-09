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
                .Returns(mockInstance);

            return new LazyService<T>(serviceProvider.Object);
        }
    }
}
