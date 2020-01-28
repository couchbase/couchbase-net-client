using System;
using System.Collections.Generic;
using Couchbase.Core;
using Couchbase.Core.DI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Core.DI
{
    public class CouchbaseServiceProviderTests
    {
        #region GetService

        [Fact]
        public void GetService_Unregistered_ReturnsNull()
        {
            // Arrange

            var provider = new CouchbaseServiceProvider(new KeyValuePair<Type, IServiceFactory>[] { });

            // Act

            var result = provider.GetService(typeof(object));

            // Assert

            Assert.Null(result);
        }

        [Fact]
        public void GetService_IServiceProvider_ReturnsSelf()
        {
            // Arrange

            var provider = new CouchbaseServiceProvider(new KeyValuePair<Type, IServiceFactory>[] { });

            // Act

            var result = provider.GetService(typeof(IServiceProvider));

            // Assert

            Assert.Equal(provider, result);
        }

        [Fact]
        public void GetService_LambdaRegistered_ReturnsService()
        {
            // Arrange

            var obj = new object();
            var factory = new LambdaServiceFactory(_ => obj);

            var provider = new CouchbaseServiceProvider(new[]
            {
                new KeyValuePair<Type, IServiceFactory>(typeof(object), factory)
            });

            // Act

            var result = provider.GetService(typeof(object));

            // Assert

            Assert.Equal(obj, result);
        }

        [Fact]
        public void GetService_LambdaRegistered_LambdaGetsServiceProvider()
        {
            // Arrange

            IServiceProvider? calledWith = null;
            var factory = new LambdaServiceFactory(serviceProvider => {
                calledWith = serviceProvider;
                return null;
            });

            var provider = new CouchbaseServiceProvider(new[]
            {
                new KeyValuePair<Type, IServiceFactory>(typeof(object), factory)
            });

            // Act

            provider.GetService(typeof(object));

            // Assert

            Assert.Equal(provider, calledWith);
        }

        [Fact]
        public void GetService_SingletonRegistered_ReturnsService()
        {
            // Arrange

            var obj = new object();
            var factory = new SingletonServiceFactory(obj);

            var provider = new CouchbaseServiceProvider(new[]
            {
                new KeyValuePair<Type, IServiceFactory>(typeof(object), factory)
            });

            // Act

            var result = provider.GetService(typeof(object));

            // Assert

            Assert.Equal(obj, result);
        }

        [Fact]
        public void GetService_GenericRegistered_ReturnsService()
        {
            // Arrange

            var loggerFactory = new NullLoggerFactory();

            var provider = new CouchbaseServiceProvider(new[]
            {
                new KeyValuePair<Type, IServiceFactory>(typeof(ILoggerFactory), new SingletonServiceFactory(loggerFactory)),
                new KeyValuePair<Type, IServiceFactory>(typeof(ILogger<>), new SingletonGenericServiceFactory(typeof(Logger<>)))
            });

            // Act

            var result = provider.GetService(typeof(ILogger<CouchbaseServiceProvider>));

            // Assert

            Assert.NotNull(result);
            Assert.IsAssignableFrom<Logger<CouchbaseServiceProvider>>(result);
        }

        [Fact]
        public void GetService_GenericAndSpecificRegistered_ReturnsSpecificService()
        {
            // Arrange

            var loggerFactory = new NullLoggerFactory();

            var logger = new Logger<CouchbaseServiceProvider>(loggerFactory);

            var provider = new CouchbaseServiceProvider(new[]
            {
                new KeyValuePair<Type, IServiceFactory>(typeof(ILoggerFactory), new SingletonServiceFactory(loggerFactory)),
                new KeyValuePair<Type, IServiceFactory>(typeof(ILogger<>), new SingletonGenericServiceFactory(typeof(Logger<>))),
                new KeyValuePair<Type, IServiceFactory>(typeof(ILogger<CouchbaseServiceProvider>), new SingletonServiceFactory(logger))
            });

            // Act

            var result = provider.GetService(typeof(ILogger<CouchbaseServiceProvider>));

            // Assert

            Assert.NotNull(result);
            var resultLogger = Assert.IsAssignableFrom<Logger<CouchbaseServiceProvider>>(result);
            Assert.Equal(logger, resultLogger);
        }

        #endregion

        #region GetRequiredService

        [Fact]
        public void GetRequiredService_Unregistered_InvalidOperationException()
        {
            // Arrange

            var provider = new CouchbaseServiceProvider(new KeyValuePair<Type, IServiceFactory>[] { });

            // Act/Assert

            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService(typeof(IList<object>)));
        }

        [Fact]
        public void GetRequiredService_LambdaRegistered_ReturnsService()
        {
            // Arrange

            var obj = new List<object>();
            var factory = new LambdaServiceFactory(_ => obj);

            var provider = new CouchbaseServiceProvider(new[]
            {
                new KeyValuePair<Type, IServiceFactory>(typeof(IList<object>), factory)
            });

            // Act

            var result = provider.GetRequiredService(typeof(IList<object>));

            // Assert

            Assert.Equal(obj, result);
        }

        #endregion

        #region GetServiceT

        [Fact]
        public void GetServiceT_Unregistered_ReturnsNull()
        {
            // Arrange

            var provider = new CouchbaseServiceProvider(new KeyValuePair<Type, IServiceFactory>[] { });

            // Act

            var result = provider.GetService<IList<object>>();

            // Assert

            Assert.Null(result);
        }

        [Fact]
        public void GetServiceT_LambdaRegistered_ReturnsService()
        {
            // Arrange

            var obj = new List<object>();
            var factory = new LambdaServiceFactory(_ => obj);

            var provider = new CouchbaseServiceProvider(new[]
            {
                new KeyValuePair<Type, IServiceFactory>(typeof(IList<object>), factory)
            });

            // Act

            var result = provider.GetService<IList<object>>();

            // Assert

            Assert.Equal(obj, result);
        }

        #endregion

        #region GetRequiredServiceT

        [Fact]
        public void GetRequiredServiceT_Unregistered_InvalidOperationException()
        {
            // Arrange

            var provider = new CouchbaseServiceProvider(new KeyValuePair<Type, IServiceFactory>[] { });

            // Act/Assert

            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IList<object>>());
        }

        [Fact]
        public void GetRequiredServiceT_LambdaRegistered_ReturnsService()
        {
            // Arrange

            var obj = new List<object>();
            var factory = new LambdaServiceFactory(_ => obj);

            var provider = new CouchbaseServiceProvider(new[]
            {
                new KeyValuePair<Type, IServiceFactory>(typeof(IList<object>), factory)
            });

            // Act

            var result = provider.GetRequiredService<IList<object>>();

            // Assert

            Assert.Equal(obj, result);
        }

        #endregion
    }
}
