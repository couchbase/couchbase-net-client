using System;
using System.Reflection;
using Couchbase.Configuration.Client;
using Couchbase.IO.Services;

#if NET45
using Couchbase.Configuration.Client.Providers;
#endif

namespace Couchbase.IO
{
    /// <summary>
    /// Contains Factory methods for creating <see cref="IIOService"/> implementations.
    /// </summary>
    public static class IOServiceFactory
    {
        /// <summary>
        /// Gets a <see cref="Func{IConnectionPool, IIOService}"/> that will create a <see cref="PooledIOService"/> instance
        /// if <paramref name="ClientConfiguration.UseConnectionPooling"/> is <c>true</c>, otherwise it will create a <see cref="MultiplexingIOService"/>.
        /// </summary>
        /// <returns></returns>
        public static Func<IConnectionPool, IIOService> GetFactory(ClientConfiguration config)
        {
            //The current SslConnection class does not support multiplexing, so default back to pooling if
            //UseSsl is enabled. This will likely change in the future when a muxio-ssl implementation exists
            return pool =>
            {
                if (config.UseSsl)
                {
                    return new PooledIOService(pool);
                }
                return new SharedPooledIOService(pool);
            };
        }

#if NET45

        /// <exception cref="TypeLoadException">Condition.</exception>
        /// <exception cref="TargetInvocationException">A class initializer is invoked and throws an exception. </exception>
        /// <exception cref="BadImageFormatException">The assembly or one of its dependencies is not valid. -or-Version 2.0 or later of the common language runtime is currently loaded, and the assembly was compiled with a later version.</exception>
        public static Func<IConnectionPool, IIOService> GetFactory(IOServiceElement element)
        {
            return GetFactory(element.Type);
        }

#endif

        /// <exception cref="TypeLoadException">Condition.</exception>
        /// <exception cref="TargetInvocationException">A class initializer is invoked and throws an exception. </exception>
        /// <exception cref="BadImageFormatException">The assembly or one of its dependencies is not valid. -or-Version 2.0 or later of the common language runtime is currently loaded, and the assembly was compiled with a later version.</exception>
        public static Func<IConnectionPool, IIOService> GetFactory(string typeName)
        {
            return (p) =>
            {
                var type = Type.GetType(typeName);
                if (type == null)
                {
                    throw new TypeLoadException(string.Format("Could not find: {0}", typeName));
                }
                return (IIOService)Activator.CreateInstance(type, p);
            };
        }


        /// <exception cref="TypeLoadException">Condition.</exception>
        /// <exception cref="TargetInvocationException">A class initializer is invoked and throws an exception. </exception>
        /// <exception cref="BadImageFormatException">The assembly or one of its dependencies is not valid. -or-Version 2.0 or later of the common language runtime is currently loaded, and the assembly was compiled with a later version.</exception>
        public static Func<IConnectionPool, IIOService> GetFactory<T>()
        {
            return (p) =>
            {
                var type = typeof (T);
                return (IIOService)Activator.CreateInstance(type, p);
            };
        }
    }
}
