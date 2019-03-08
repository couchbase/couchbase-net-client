using System;
using System.IO;

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Creates instances of MemoryStreams for use in writing/ reading bytes to/ from the network.
    /// </summary>
    public static class MemoryStreamFactory
    {
        private static Func<MemoryStream> _factoryFunc = () => new MemoryStream();

        /// <summary>
        /// Provides a custom MemoryStream creation function that will override the default implementation.
        /// </summary>
        /// <param name="factoryFunc"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void SetFactoryFunc(Func<MemoryStream> factoryFunc)
        {
            _factoryFunc = factoryFunc ?? throw new ArgumentNullException(nameof(factoryFunc), "You must provide a non-null factory function");
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
