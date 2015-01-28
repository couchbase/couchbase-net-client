﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached.Protocol.Binary;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Results;
using Enyim.Caching.Memcached.Results.Extensions;

namespace Couchbase
{
    internal class TouchOperation : BinarySingleItemOperation, IOperationWithState, ITouchOperation
    {
        private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(TouchOperation));

        private uint expires;
        private OperationState state;
        private VBucketNodeLocator locator;

        public TouchOperation(VBucketNodeLocator locator, string key, uint expires)
            : base(key)
        {
            this.locator = locator;
            this.expires = expires;
        }

        protected override BinaryRequest Build()
        {
            var retval = new BinaryRequest(0x1c);

            retval.Key = this.Key;

            if (this.locator != null)
            {
                retval.Reserved = (ushort)locator.GetIndex(this.Key);
                if (log.IsDebugEnabled) log.DebugFormat("Key {0} was mapped to {1}", this.Key, retval.Reserved);
            }

            var extra = new byte[4];

            BinaryConverter.EncodeUInt32(this.expires, extra, 0);
            retval.Extra = new ArraySegment<byte>(extra);

            return retval;
        }

        protected override IOperationResult ProcessResponse(BinaryResponse response)
        {
            var r = response.StatusCode == 0;
            var result = new BinaryOperationResult();

            if (this.locator != null &&
                !VBucketAwareOperationFactory.GuessResponseState(response, out this.state))
            {
                return result.Fail("Process response failed");
            }

            return result.PassOrFail(r, "Processing response failed");
        }

        #region [ IOperationWithState          ]

        OperationState IOperationWithState.State
        {
            get { return this.state; }
        }

        #endregion
    }
}