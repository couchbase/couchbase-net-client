using System;
using System.IO;

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Creates instances of MemoryStreams for use in writing/ reading bytes to/ from the network.
    /// </summary>
    internal static class MemoryStreamFactory
    {
        private const int DefaultStreamCapacity = 16384;

        private static Func<MemoryStream> _factoryFunc = () => new MemoryStream(DefaultStreamCapacity);

        /// <summary>
        /// Provides a custom MemoryStream creation function that will override the default implementation.
        /// </summary>
        /// <param name="factoryFunc"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void SetFactoryFunc(Func<MemoryStream> factoryFunc)
        {
            _factoryFunc = factoryFunc ?? throw new ArgumentNullException(nameof(factoryFunc));
        }

        /// <summary>
        /// Fetches a MemoryStream. The default implementation retuns a new MemoryStream instance.
        /// </summary>
        /// <returns></returns>
        public static MemoryStream GetMemoryStream()
        {
            return _factoryFunc();
        }
    }
}
