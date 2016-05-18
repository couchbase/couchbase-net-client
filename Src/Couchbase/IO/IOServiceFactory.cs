using System;
using System.Reflection;
using Couchbase.IO.Services;

#if NET45
using Couchbase.Configuration.Client.Providers;
#endif

namespace Couchbase.IO
{
    /// <summary>
    /// Contains Factory methods for creating <see cref="IIOService"/> implementations.
    /// </summary>
    internal static class IOServiceFactory
    {
        /// <summary>
        /// Gets a <see cref="Func{IConnectionPool, IIOService}"/> that will create a <see cref="PooledIOService"/> instance.
        /// </summary>
        /// <returns></returns>
        public static Func<IConnectionPool, IIOService> GetFactory()
        {
            return (p) => new PooledIOService(p);
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
                if (type == null)
                {
                    throw new TypeLoadException(string.Format("Could not create IIOService from factory."));
                }
                return (IIOService)Activator.CreateInstance(type, p);
            };
        }
    }
}
