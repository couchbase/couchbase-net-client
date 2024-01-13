using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Extensions.DependencyInjection.Internal;
using Couchbase.KeyValue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Couchbase.Extensions.DependencyInjection.UnitTests
{
    public class ServiceCollectionExtensionsTests
    {
        #region AddCouchbase IConfiguration

        [Fact]
        public void AddCouchbase_WithConfiguration_BindsConfiguration()
        {
            // Arrange

            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("MaxHttpConnections", "1005")
            });

            var configuration = configurationBuilder.Build();

            var services = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(new NullLoggerFactory());

            //  Act

            services.AddCouchbase(configuration);

            // Assert

            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<ClusterOptions>>();

            Assert.Equal(1005, options.Value.MaxHttpConnections);
        }

        [Fact]
        public void AddCouchbase_WithNullConfiguration_Exception()
        {
            // Arrange

            var services = new ServiceCollection();

            //  Act/Assert

            var ex = Assert.Throws<ArgumentNullException>(() => services.AddCouchbase((IConfiguration)null));

            Assert.Equal("configuration", ex.ParamName);
        }

        #endregion

        #region AddCouchbase Action

        [Fact]
        public void AddCouchbase_NoOverride_RegistersICouchbaseBucketProviderAsSingleton()
        {
            // Arrange

            var services = new ServiceCollection();

            //  Act

            services.AddCouchbase((Action<ClusterOptions>)null);

            // Assert

            var description = services.FirstOrDefault(p => p.ServiceType == typeof(IBucketProvider));

            Assert.NotNull(description);
            Assert.Equal(ServiceLifetime.Singleton, description.Lifetime);
        }

        [Fact]
        public void AddCouchbase_WithOverride_KeepsPreviousICouchbaseBucketProvider()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);

            //  Act

            services.AddCouchbase((Action<ClusterOptions>)null);

            // Assert

            var description = services.FirstOrDefault(p => p.ServiceType == typeof(IBucketProvider));

            Assert.NotNull(description);
            Assert.Equal(bucketProvider.Object, description.ImplementationInstance);
        }

        [Fact]
        public void AddCouchbase_NoOverride_RegistersICouchbaseClusterProviderAsSingleton()
        {
            // Arrange

            var services = new ServiceCollection();

            //  Act

            services.AddCouchbase((Action<ClusterOptions>)null);

            // Assert

            var description = services.FirstOrDefault(p => p.ServiceType == typeof(IClusterProvider));

            Assert.NotNull(description);
            Assert.Equal(ServiceLifetime.Singleton, description.Lifetime);
        }

        [Fact]
        public void AddCouchbase_WithOverride_KeepsPreviousICouchbaseClusterProvider()
        {
            // Arrange

            var clusterProvider = new Mock<IClusterProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(clusterProvider.Object);

            //  Act

            services.AddCouchbase((Action<ClusterOptions>)null);

            // Assert

            var description = services.FirstOrDefault(p => p.ServiceType == typeof(IClusterProvider));

            Assert.NotNull(description);
            Assert.Equal(clusterProvider.Object, description.ImplementationInstance);
        }

        [Fact]
        public void AddCouchbase_WithOptions_RegistersOptions()
        {
            // Arrange

            var services = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(new NullLoggerFactory());

            Action<ClusterOptions> optionsAction = clientDefinition =>
            {
                clientDefinition.MaxHttpConnections = 1005;
            };

            //  Act

            services.AddCouchbase(optionsAction);

            // Assert

            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<ClusterOptions>>();

            Assert.Equal(1005, options.Value.MaxHttpConnections);
        }

        [Fact]
        public void AddCouchbase_WithOptionsNoLogger_UseTheDILogger()
        {
            // Arrange

            var logger = new NullLoggerFactory();

            var services = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(logger);

            Action<ClusterOptions> optionsAction = clientDefinition =>
            {
            };

            //  Act

            services.AddCouchbase(optionsAction);

            // Assert

            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<ClusterOptions>>();

            Assert.Equal(logger, options.Value.Logging);
        }

        [Fact]
        public void AddCouchbase_WithOptionsWithLogger_OverridesTheDILogger()
        {
            // Arrange

            var services = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(new NullLoggerFactory());

            var overrideLogger = Mock.Of<ILoggerFactory>();

            Action<ClusterOptions> optionsAction = clientDefinition =>
            {
                clientDefinition.WithLogging(overrideLogger);
            };

            //  Act

            services.AddCouchbase(optionsAction);

            // Assert

            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<ClusterOptions>>();

            Assert.Equal(overrideLogger, options.Value.Logging);
        }

        #endregion

        #region AddKeyedCouchbase

        [Fact]
        public void AddCouchbase_UnkeyedAndKeyed_SeparateProviders()
        {
            // Arrange

            var services = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(new NullLoggerFactory());

            //  Act

            services.AddCouchbase(_ => { });
            services.AddKeyedCouchbase("foo", _ => { });

            // Assert

            var provider = (IKeyedServiceProvider)services.BuildServiceProvider();
            var clusterProvider1 = provider.GetRequiredService<IClusterProvider>();
            var clusterProvider2 = provider.GetRequiredKeyedService<IClusterProvider>("foo");
            var bucketProvider1 = provider.GetRequiredService<IBucketProvider>();
            var bucketProvider2 = provider.GetRequiredKeyedService<IBucketProvider>("foo");

            Assert.NotSame(clusterProvider1, clusterProvider2);
            Assert.NotSame(bucketProvider1, bucketProvider2);
        }

        [Fact]
        public void AddCouchbase_TwoKeyed_SeparateProviders()
        {
            // Arrange

            var services = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(new NullLoggerFactory());

            //  Act

            services.AddKeyedCouchbase("foo", _ => { });
            services.AddKeyedCouchbase("bar", _ => { });

            // Assert

            var provider = (IKeyedServiceProvider)services.BuildServiceProvider();
            var clusterProvider1 = provider.GetRequiredKeyedService<IClusterProvider>("foo");
            var clusterProvider2 = provider.GetRequiredKeyedService<IClusterProvider>("bar");
            var bucketProvider1 = provider.GetRequiredKeyedService<IBucketProvider>("foo");
            var bucketProvider2 = provider.GetRequiredKeyedService<IBucketProvider>("bar");

            Assert.NotSame(clusterProvider1, clusterProvider2);
            Assert.NotSame(bucketProvider1, bucketProvider2);
        }

        [Fact]
        public void AddCouchbase_UnkeyedAndKeyed_IndepedentOptions()
        {
            // Arrange

            var services = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(new NullLoggerFactory());

            Action<ClusterOptions> optionsAction1 = clientDefinition =>
            {
                clientDefinition.MaxHttpConnections = 1005;
            };

            Action<ClusterOptions> optionsAction2 = clientDefinition =>
            {
                clientDefinition.MaxKvConnections = 2006;
            };

            //  Act

            services.AddCouchbase(optionsAction1);
            services.AddKeyedCouchbase("foo", optionsAction2);

            // Assert

            var provider = services.BuildServiceProvider();
            var options1 = provider.GetRequiredService<IOptions<ClusterOptions>>().Value;
            var options2 = provider.GetRequiredService<IOptionsMonitor<ClusterOptions>>().Get("foo");

            Assert.Equal(1005, options1.MaxHttpConnections);
            Assert.NotEqual(2006, options1.MaxKvConnections);

            Assert.NotEqual(1005, options2.MaxHttpConnections);
            Assert.Equal(2006, options2.MaxKvConnections);
        }

        [Fact]
        public void AddCouchbase_TwoKeys_IndepedentOptions()
        {
            // Arrange

            var services = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(new NullLoggerFactory());

            Action<ClusterOptions> optionsAction1 = clientDefinition =>
            {
                clientDefinition.MaxHttpConnections = 1005;
            };

            Action<ClusterOptions> optionsAction2 = clientDefinition =>
            {
                clientDefinition.MaxKvConnections = 2006;
            };

            //  Act

            services.AddKeyedCouchbase("foo", optionsAction1);
            services.AddKeyedCouchbase("bar", optionsAction2);

            // Assert

            var provider = services.BuildServiceProvider();
            var options1 = provider.GetRequiredService<IOptionsMonitor<ClusterOptions>>().Get("foo");
            var options2 = provider.GetRequiredService<IOptionsMonitor<ClusterOptions>>().Get("bar");

            Assert.Equal(1005, options1.MaxHttpConnections);
            Assert.NotEqual(2006, options1.MaxKvConnections);

            Assert.NotEqual(1005, options2.MaxHttpConnections);
            Assert.Equal(2006, options2.MaxKvConnections);
        }

        #endregion

        #region AddCouchbaseBucket

        [Fact]
        public void AddCouchbaseBucket_NullName_Exception()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);

            // Act/Assert

            var ex =
                Assert.Throws<ArgumentNullException>(() => services.AddCouchbaseBucket<ITestBucketProvider>(null));

            Assert.Equal("bucketName", ex.ParamName);
        }

        [Fact]
        public void AddCouchbaseBucket_Name_ReturnsServiceCollection()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);

            // Act

            var result = services.AddCouchbaseBucket<ITestBucketProvider>("bucketName");

            // Assert

            Assert.Equal(services, result);
        }

        [Fact]
        public void AddCouchbaseBucket_Name_ProvidesNamedBucketProvider()
        {
            // Arrange
            const string bucketName = "bucketName";

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);
            services.AddCouchbase(options => { });

            // Act

            services.AddCouchbaseBucket<ITestBucketProvider>(bucketName);

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedBucketProvider = serviceProvider.GetRequiredService<ITestBucketProvider>();

            Assert.NotNull(namedBucketProvider);
            Assert.Equal(bucketName, namedBucketProvider.BucketName);
        }

        [Fact]
        public void AddCouchbaseBucket_WithBuilder_ProvidesNamedCollectionProvider()
        {
            // Arrange
            const string bucketName = "bucketName";
            const string scopeName = "scopeName";
            const string collectionName = "collectionName";

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);
            services.AddCouchbase(options => { });

            // Act

                services.AddCouchbaseBucket<ITestBucketProvider>(bucketName, builder =>
            {
                builder.AddScope(scopeName)
                    .AddCollection<ITestCollectionProvider>(collectionName);
            });

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedCollectionProvider = serviceProvider.GetRequiredService<ITestCollectionProvider>();

            Assert.NotNull(namedCollectionProvider);
            Assert.Equal(scopeName, namedCollectionProvider.ScopeName);
            Assert.Equal(collectionName, namedCollectionProvider.CollectionName);
        }

        #endregion

        #region AddCouchbaseBucket

        [Fact]
        public void AddKeyedCouchbaseBucket_NullName_Exception()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddKeyedSingleton("foo", bucketProvider.Object);

            // Act/Assert

            var ex =
                Assert.Throws<ArgumentNullException>(() => services.AddKeyedCouchbaseBucket<ITestBucketProvider>("foo", null!));

            Assert.Equal("bucketName", ex.ParamName);
        }

        [Fact]
        public void AddKeyedCouchbaseBucket_Name_ReturnsServiceCollection()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddKeyedSingleton("foo", bucketProvider.Object);

            // Act

            var result = services.AddKeyedCouchbaseBucket<ITestBucketProvider>("foo", "bucketName");

            // Assert

            Assert.Equal(services, result);
        }

        [Fact]
        public async Task AddKeyedCouchbaseBucket_Name_ProvidesNamedBucketProvider()
        {
            // Arrange
            const string bucketName = "bucketName";

            var bucket = Mock.Of<IBucket>();

            var bucketProvider = new Mock<IBucketProvider>();
            bucketProvider
                .Setup(m => m.GetBucketAsync(bucketName))
                .ReturnsAsync(bucket);

            var services = new ServiceCollection();
            services.AddKeyedSingleton("foo", bucketProvider.Object);
            services.AddKeyedCouchbase("foo", options => { });

            // Act

            services.AddKeyedCouchbaseBucket<ITestBucketProvider>("foo", bucketName);

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedBucketProvider = serviceProvider.GetRequiredKeyedService<ITestBucketProvider>("foo");

            Assert.NotNull(namedBucketProvider);
            Assert.Equal(bucketName, namedBucketProvider.BucketName);

            var result = await namedBucketProvider.GetBucketAsync();
            Assert.Same(bucket, result);
        }

        [Fact]
        public async Task AddKeyedCouchbaseBucket_WithBuilder_ProvidesNamedCollectionProvider()
        {
            // Arrange
            const string bucketName = "bucketName";
            const string scopeName = "scopeName";
            const string collectionName = "collectionName";

            var collection = Mock.Of<ICouchbaseCollection>();

            var scope = new Mock<IScope>();
            scope
                .Setup(m => m.Collection(collectionName))
                .Returns(collection);

            var bucket = new Mock<IBucket>();
            bucket
                .Setup(m => m.Scope(scopeName))
                .Returns(scope.Object);

            var bucketProvider = new Mock<IBucketProvider>();
            bucketProvider
                .Setup(m => m.GetBucketAsync(bucketName))
                .ReturnsAsync(bucket.Object);

            var services = new ServiceCollection();
            services.AddKeyedSingleton("foo", bucketProvider.Object);
            services.AddKeyedCouchbase("foo", options => { });

            // Act

            services.AddKeyedCouchbaseBucket<ITestBucketProvider>("foo", bucketName, builder =>
            {
                builder.AddScope(scopeName)
                    .AddCollection<ITestCollectionProvider>(collectionName);
            });

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedCollectionProvider = serviceProvider.GetRequiredKeyedService<ITestCollectionProvider>("foo");

            Assert.NotNull(namedCollectionProvider);
            Assert.Equal(scopeName, namedCollectionProvider.ScopeName);
            Assert.Equal(collectionName, namedCollectionProvider.CollectionName);

            var result = await namedCollectionProvider.GetCollectionAsync();
            Assert.Same(collection, result);
        }

        [Fact]
        public async Task AddKeyedCouchbaseBucket_TwoKeysSameBucketName_ProvidesNamedBucketProviders()
        {
            // Arrange
            const string bucketName = "bucketName";

            var bucket1 = Mock.Of<IBucket>();
            var bucket2 = Mock.Of<IBucket>();

            var bucketProvider1 = new Mock<IBucketProvider>();
            bucketProvider1
                .Setup(m => m.GetBucketAsync(bucketName))
                .ReturnsAsync(bucket1);

            var bucketProvider2 = new Mock<IBucketProvider>();
            bucketProvider2
                .Setup(m => m.GetBucketAsync(bucketName))
                .ReturnsAsync(bucket2);

            var services = new ServiceCollection();
            services.AddKeyedSingleton("foo", bucketProvider1.Object);
            services.AddKeyedCouchbase("foo", options => { });
            services.AddKeyedSingleton("bar", bucketProvider2.Object);
            services.AddKeyedCouchbase("bar", options => { });

            // Act

            services.AddKeyedCouchbaseBucket<ITestBucketProvider>("foo", bucketName);
            services.AddKeyedCouchbaseBucket<ITestBucketProvider2>("bar", bucketName);

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedBucketProvider1 = serviceProvider.GetRequiredKeyedService<ITestBucketProvider>("foo");
            var namedBucketProvider2 = serviceProvider.GetRequiredKeyedService<ITestBucketProvider2>("bar");

            Assert.NotNull(namedBucketProvider1);
            Assert.Equal(bucketName, namedBucketProvider1.BucketName);

            Assert.NotNull(namedBucketProvider2);
            Assert.Equal(bucketName, namedBucketProvider2.BucketName);

            var result1 = await namedBucketProvider1.GetBucketAsync();
            Assert.Same(bucket1, result1);

            var result2 = await namedBucketProvider2.GetBucketAsync();
            Assert.Same(bucket2, result2);
        }

        #endregion

        #region AddCouchbaseBucketConcrete

        [Fact]
        public void AddCouchbaseBucketConcrete_Name_ReturnsServiceCollection()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);

            // Act

            var result = services.AddCouchbaseBucket<ITestBucketProvider, TestBucketProvider>();

            // Assert

            Assert.Equal(services, result);
        }

        [Fact]
        public void AddCouchbaseBucketConcrete_Name_ProvidesNamedBucketProvider()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);
            services.AddCouchbase(options => { });

            // Act

            services.AddCouchbaseBucket<ITestBucketProvider, TestBucketProvider>();

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedBucketProvider = serviceProvider.GetRequiredService<ITestBucketProvider>();

            Assert.NotNull(namedBucketProvider);
            Assert.Equal("bucketName", namedBucketProvider.BucketName);
        }

        [Fact]
        public void AddCouchbaseBucketConcrete_WithBuilder_ProvidesNamedCollectionProvider()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);
            services.AddCouchbase(options => { });

            // Act

            services.AddCouchbaseBucket<ITestBucketProvider, TestBucketProvider>(builder =>
            {
                builder.AddCollection<ITestCollectionProvider, TestCollectionProvider>();
            });

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedCollectionProvider = serviceProvider.GetRequiredService<ITestCollectionProvider>();

            Assert.NotNull(namedCollectionProvider);
            Assert.Equal("_default", namedCollectionProvider.ScopeName);
            Assert.Equal("_default", namedCollectionProvider.CollectionName);
        }

        #endregion

        #region TryAddCouchbaseBucket

        [Fact]
        public void TryAddCouchbaseBucket_NullName_Exception()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);

            // Act/Assert

            var ex =
                Assert.Throws<ArgumentNullException>(() => services.TryAddCouchbaseBucket<ITestBucketProvider>(null));

            Assert.Equal("bucketName", ex.ParamName);
        }

        [Fact]
        public void TryAddCouchbaseBucket_Name_ReturnsServiceCollection()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);

            // Act

            var result = services.TryAddCouchbaseBucket<ITestBucketProvider>("bucketName");

            // Assert

            Assert.Equal(services, result);
        }

        [Fact]
        public void TryAddCouchbaseBucket_Name_ProvidesNamedBucketProvider()
        {
            // Arrange
            const string bucketName = "bucketName";

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);
            services.AddCouchbase(options => { });

            // Act

            services.TryAddCouchbaseBucket<ITestBucketProvider>(bucketName);

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedBucketProvider = serviceProvider.GetRequiredService<ITestBucketProvider>();

            Assert.NotNull(namedBucketProvider);
            Assert.Equal(bucketName, namedBucketProvider.BucketName);
        }

        [Fact]
        public void TryAddCouchbaseBucket_CalledTwice_UsesFirstRegistration()
        {
            // Arrange
            const string bucketName = "bucketName";

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);
            services.AddCouchbase(options => { });

            // Act

            services.TryAddCouchbaseBucket<ITestBucketProvider>(bucketName);
            services.TryAddCouchbaseBucket<ITestBucketProvider>("bucketName2");

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedBucketProvider = serviceProvider.GetRequiredService<ITestBucketProvider>();

            Assert.NotNull(namedBucketProvider);
            Assert.Equal(bucketName, namedBucketProvider.BucketName);
        }

        [Fact]
        public void TryAddCouchbaseBucket_WithBuilder_ProvidesNamedCollectionProvider()
        {
            // Arrange
            const string bucketName = "bucketName";
            const string scopeName = "scopeName";
            const string collectionName = "collectionName";

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);
            services.AddCouchbase(options => { });

            // Act

            services.TryAddCouchbaseBucket<ITestBucketProvider>(bucketName, builder =>
            {
                builder.AddScope(scopeName)
                    .AddCollection<ITestCollectionProvider>(collectionName);
            });

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedCollectionProvider = serviceProvider.GetRequiredService<ITestCollectionProvider>();

            Assert.NotNull(namedCollectionProvider);
            Assert.Equal(scopeName, namedCollectionProvider.ScopeName);
            Assert.Equal(collectionName, namedCollectionProvider.CollectionName);
        }

        #endregion

        #region TryAddCouchbaseBucketConcrete

        [Fact]
        public void TryAddCouchbaseBucketConcrete_Name_ReturnsServiceCollection()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);

            // Act

            var result = services.TryAddCouchbaseBucket<ITestBucketProvider,TestBucketProvider>();

            // Assert

            Assert.Equal(services, result);
        }

        [Fact]
        public void TryAddCouchbaseBucketConcrete_Name_ProvidesNamedBucketProvider()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);
            services.AddCouchbase(options => { });

            // Act

            services.TryAddCouchbaseBucket<ITestBucketProvider, TestBucketProvider>();

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedBucketProvider = serviceProvider.GetRequiredService<ITestBucketProvider>();

            Assert.NotNull(namedBucketProvider);
            Assert.Equal("bucketName", namedBucketProvider.BucketName);
        }

        [Fact]
        public void TryAddCouchbaseBucketConcrete_WithBuilder_ProvidesNamedCollectionProvider()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);
            services.AddCouchbase(options => { });

            // Act

            services.TryAddCouchbaseBucket<ITestBucketProvider, TestBucketProvider>(builder =>
            {
                builder.AddCollection<ITestCollectionProvider, TestCollectionProvider>();
            });

            // Assert

            var serviceProvider = services.BuildServiceProvider();
            var namedCollectionProvider = serviceProvider.GetRequiredService<ITestCollectionProvider>();

            Assert.NotNull(namedCollectionProvider);
            Assert.Equal("_default", namedCollectionProvider.ScopeName);
            Assert.Equal("_default", namedCollectionProvider.CollectionName);
        }

        #endregion

        #region Helpers

        public interface ITestBucketProvider : INamedBucketProvider
        {
        }

        public interface ITestBucketProvider2 : INamedBucketProvider
        {
        }

        public interface ITestCollectionProvider : INamedCollectionProvider
        {
        }

        public class TestBucketProvider : NamedBucketProvider, ITestBucketProvider
        {
            public TestBucketProvider(IBucketProvider bucketProvider) : base(bucketProvider, "bucketName")
            {
            }
        }

        public class TestCollectionProvider : DefaultCollectionProvider, ITestCollectionProvider
        {
            public TestCollectionProvider(ITestBucketProvider bucketProvider) : base(bucketProvider)
            {
            }
        }

        #endregion
    }
}
