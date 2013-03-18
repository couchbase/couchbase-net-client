using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached.Protocol.Binary;
using Enyim.Caching.Memcached.Results;
using Enyim.Caching.Memcached.Results.Extensions;
using Enyim.Caching.Memcached;
using Couchbase.Protocol;
using Enyim.Caching.Memcached.Protocol;
using Enyim.Caching.Memcached.Results.Helpers;
using Couchbase.Results;
using Enyim.Caching.Memcached.Results.StatusCodes;

namespace Couchbase.Operations
{
	public class ObserveOperation : Operation, IObserveOperation
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(TouchOperation));

		private readonly string _key;
		private readonly int _vbucket;
		private readonly ulong _cas;

		public ObserveOperation(string key, int vbucket, ulong cas)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException("key");
			_key = key;
			_vbucket = vbucket;
			_cas = cas;
		}

		protected override IOperationResult ReadResponse(PooledSocket socket)
		{
			var response = new ObserveResponse();
			var result = new ObserveOperationResult();
			var retval = false;

			if (response.Read(socket))
			{
				retval = true;
				result.Cas = response.Cas;
				result.StatusCode = StatusCode;
				result.Key = response.Key;
				result.KeyState = response.KeyState;
				result.PersistenceStats = response.PersistenceStats;
				result.ReplicationStats = response.ReplicationStats;
			}

            this.StatusCode = response.StatusCode.ToStatusCode();

			result.PassOrFail(retval, ResultHelper.ProcessResponseData(response.Data, "Failed: "));
			return result;
		}

		protected override bool ReadResponseAsync(PooledSocket socket, Action<bool> next)
		{
			throw new NotImplementedException();
		}

		protected override IList<ArraySegment<byte>> GetBuffer()
		{
			var request = new ObserveRequest();
			request.Key = _key;
			request.VBucket = _vbucket;
			request.Cas = _cas;

			return request.CreateBuffer();
		}

	}
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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