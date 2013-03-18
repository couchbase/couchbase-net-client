using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached.Protocol.Binary;
using Enyim.Caching.Memcached;
using Couchbase.Protocol;

namespace Couchbase.Operations
{
	internal class GetWithLockOperation : GetOperation, IGetWithLockOperation, IOperationWithState
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(GetWithLockOperation));

		private uint lockExpiration;
		private OperationState state;
		private VBucketNodeLocator locator;

		public GetWithLockOperation(VBucketNodeLocator locator, string key, uint lockExpiration)
			: base(key)
		{
			this.locator = locator;
			this.lockExpiration = lockExpiration;
		}

		protected override BinaryRequest Build()
		{
			var retval = base.Build();
			retval.Operation = (byte)CouchbaseOpCode.GetL;

			if (this.locator != null)
			{
				retval.Reserved = (ushort)locator.GetIndex(this.Key);

				if (log.IsDebugEnabled) log.DebugFormat("Key {0} was mapped to {1}", this.Key, retval.Reserved);
			}

			var extra = new byte[4];

			BinaryConverter.EncodeUInt32(this.lockExpiration, extra, 0);
			retval.Extra = new ArraySegment<byte>(extra);

			return retval;
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