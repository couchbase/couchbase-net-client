using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

            var services = new ServiceCollection();

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

            var services = new ServiceCollection();

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

        #endregion

        #region Helpers

        public interface ITestBucketProvider : INamedBucketProvider
        {
        }

        #endregion
    }
}
