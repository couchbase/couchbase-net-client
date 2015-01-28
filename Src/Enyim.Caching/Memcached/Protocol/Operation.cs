using System;
using System.Collections.Generic;
using System.Threading;
using Enyim.Caching.Memcached.Results;

namespace Enyim.Caching.Memcached.Protocol
{
    /// <summary>
    /// Base class for implementing operations.
    /// </summary>
    public abstract class Operation : IOperation
    {
        private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(Operation));

        protected Operation(){}

        protected internal abstract IList<ArraySegment<byte>> GetBuffer();

        protected internal abstract IOperationResult ReadResponse(IPooledSocket socket);

        protected internal abstract bool ReadResponseAsync(IPooledSocket socket, Action<bool> next);

        public int RetryAttempts { get;  set; }

        IList<ArraySegment<byte>> IOperation.GetBuffer()
        {
            return this.GetBuffer();
        }

        IOperationResult IOperation.ReadResponse(IPooledSocket socket)
        {
            return this.ReadResponse(socket);
        }

        bool IOperation.ReadResponseAsync(IPooledSocket socket, Action<bool> next)
        {
            return this.ReadResponseAsync(socket, next);
        }

        int IOperation.StatusCode
        {
            get { return this.StatusCode; }
        }

        public int StatusCode { get; protected set; }

        public string Key { get; protected set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    Copyright (c) 2010 Attila Kisk�, enyim.com
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

#endregion