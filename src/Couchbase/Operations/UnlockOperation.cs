using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached.Protocol.Binary;
using Enyim.Caching.Memcached;
using Couchbase.Protocol;
using Enyim.Caching.Memcached.Results;
using Enyim.Caching.Memcached.Results.Helpers;
using Enyim.Caching.Memcached.Results.Extensions;

namespace Couchbase.Operations
{
	internal class UnlockOperation : BinarySingleItemOperation, IUnlockOperation, IOperationWithState
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(UnlockOperation));

		private OperationState state;
		private VBucketNodeLocator locator;

		public UnlockOperation(VBucketNodeLocator locator, string key, ulong cas)
			: base(key)
		{
			this.locator = locator;
			this.Cas = cas;
		}

		protected override BinaryRequest Build()
		{
			var op = (byte)CouchbaseOpCode.Unlock;
			var request = new BinaryRequest(op);

			if (this.locator != null)
			{
				request.Reserved = (ushort)locator.GetIndex(this.Key);

				if (log.IsDebugEnabled) log.DebugFormat("Key {0} was mapped to {1}", this.Key, request.Reserved);
			}

			request.Key = Key;
			request.Cas = Cas;

			return request;
		}

		protected override IOperationResult ProcessResponse(BinaryResponse response)
		{
			var status = response.StatusCode;
			var result = new BinaryOperationResult();

			this.StatusCode = status;

			if (status == 0)
			{
				this.Cas = response.CAS;
				return result.Pass();
			}

			this.Cas = 0;
			var message = ResultHelper.ProcessResponseData(response.Data);
			return result.Fail(message);
		}

		#region [ IOperationWithState          ]

		OperationState IOperationWithState.State
		{
			get { return this.state; }
		}

		#endregion
	}
}

/**
 * Copyright (c) 2013 Couchbase, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */