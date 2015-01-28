using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Collections;

namespace Couchbase
{
    internal abstract class CouchbaseSpatialViewBase<T> : ISpatialView<T>
    {
        protected readonly CouchbaseViewHandler ViewHandler;

        private float[] _boundingBox;
        private StaleMode? _stale;
        private int _limit;
        private int _skip;

        internal CouchbaseSpatialViewBase(ICouchbaseClient client, IHttpClientLocator clientLocator, string designDocument, string indexName, int retryCount)
        {
            ViewHandler = new CouchbaseViewHandler(client, clientLocator, designDocument, indexName, retryCount, "_spatial");
        }

        public ISpatialView<T> BoundingBox(float lowerLeftLong, float lowerLeftLat, float upperRightLong, float upperRightLat)
        {
            _boundingBox = new float[] { lowerLeftLong, lowerLeftLat, upperRightLong, upperRightLat };
            return this;
        }

        public ISpatialView<T> Limit(int limit)
        {
            _limit = limit;
            return this;
        }

        public ISpatialView<T> Skip(int skip)
        {
            _skip = skip;
            return this;
        }

        public ISpatialView<T> Stale(StaleMode stale)
        {
            _stale = stale;
            return this;
        }

        protected IDictionary<string, string> BuildParams()
        {
            var viewParamsBuilder = new ViewParamsBuilder();
            if (_boundingBox != null)
            {
                if (_boundingBox.Length != 4)
                {
                    throw new ArgumentException("4 coordinates must be supplied for bounding box");
                }

#if NET35
                    viewParamsBuilder.AddParam("bbox", string.Join(",", _boundingBox.Select(b => b.ToString()).ToArray()));
#else
                    viewParamsBuilder.AddParam("bbox", string.Join(",", _boundingBox));
#endif
                }

            viewParamsBuilder.AddGreaterThanOneParam("limit", _limit);
            viewParamsBuilder.AddOptionalParam("skip", _skip);
            viewParamsBuilder.AddStaleParam(_stale);

            return viewParamsBuilder.Build();
        }

        #region IEnumerable<T> Members

        public abstract IEnumerator<T> GetEnumerator();

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        #endregion
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