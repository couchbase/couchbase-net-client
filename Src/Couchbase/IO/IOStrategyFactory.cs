using System;
using System.Reflection;
using Couchbase.Configuration.Client.Providers;
using Couchbase.IO.Strategies;

namespace Couchbase.IO
{
    /// <summary>
    /// Contains Factory methods for creating <see cref="IOStrategy"/> implementations.
    /// </summary>
    internal static class IOStrategyFactory
    {
        /// <summary>
        /// Gets a <see cref="Func{IConnectionPool, IIOService}"/> that will create a <see cref="DefaultIOStrategy"/> instance.
        /// </summary>
        /// <returns></returns>
        public static Func<IConnectionPool, IOStrategy> GetFactory()
        {
            return (p) => new DefaultIOStrategy(p);
        }

        /// <exception cref="TypeLoadException">Condition.</exception>
        /// <exception cref="TargetInvocationException">A class initializer is invoked and throws an exception. </exception>
        /// <exception cref="BadImageFormatException">The assembly or one of its dependencies is not valid. -or-Version 2.0 or later of the common language runtime is currently loaded, and the assembly was compiled with a later version.</exception>
        public static Func<IConnectionPool, IOStrategy> GetFactory(IOServiceElement element)
        {
            return (p) =>
            {
                var type = Type.GetType(element.Type);
                if (type == null)
                {
                    throw new TypeLoadException(string.Format("Could not find: {0}", element.Type));
                }
                return (IOStrategy) Activator.CreateInstance(type, p);
            };
        }


        /// <exception cref="TypeLoadException">Condition.</exception>
        /// <exception cref="TargetInvocationException">A class initializer is invoked and throws an exception. </exception>
        /// <exception cref="BadImageFormatException">The assembly or one of its dependencies is not valid. -or-Version 2.0 or later of the common language runtime is currently loaded, and the assembly was compiled with a later version.</exception>
        public static Func<IConnectionPool, IOStrategy> GetFactory<T>()
        {
            return (p) =>
            {
                var type = typeof (T);
                if (type == null)
                {
                    throw new TypeLoadException(string.Format("Could not create IIOService from factory."));
                }
                return (IOStrategy)Activator.CreateInstance(type, p);
            };
        }
    }
}
