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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
