using System;
using Microsoft.IO;

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Creates instances of MemoryStreams for use in writing/ reading bytes to/ from the network.
    /// </summary>
    internal static class MemoryStreamFactory
    {
        private static readonly RecyclableMemoryStreamManager MemoryStreamManager =
            new RecyclableMemoryStreamManager();

        private static Func<RecyclableMemoryStream> _factoryFunc = () => new RecyclableMemoryStream(MemoryStreamManager);

        /// <summary>
        /// Provides a custom MemoryStream creation function that will override the default implementation.
        /// </summary>
        /// <param name="factoryFunc"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void SetFactoryFunc(Func<RecyclableMemoryStream> factoryFunc)
        {
            _factoryFunc = factoryFunc ?? throw new ArgumentNullException(nameof(factoryFunc), "You must provide a non-null factory function");
        }

        /// <summary>
        /// Fetches a MemoryStream. The default implementation retuns a new MemoryStream instance.
        /// </summary>
        /// <returns></returns>
        public static RecyclableMemoryStream GetMemoryStream()
        {
            return _factoryFunc();
        }
    }
}
