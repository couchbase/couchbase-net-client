using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using Couchbase.Helpers;

namespace Couchbase {
    internal class CouchbaseView<T> : CouchbaseViewBase<T> {

		private bool _shouldLookupDocById = false;

        internal CouchbaseView(ICouchbaseClient client, IHttpClientLocator clientLocator,
								string designDocument, string indexName, int retryCount, bool shouldLookupDocById = false,
								string pagedViewIdProperty = null, string pagedViewKeyProperty = null)
            : base(client, clientLocator, designDocument, indexName, retryCount)
		{
			_shouldLookupDocById = shouldLookupDocById;
		}

        protected CouchbaseView(CouchbaseView<T> original)
            : base(original) { }


        #region IEnumerable<T> Members

        public override IEnumerator<T> GetEnumerator() 
        {
			return TransformResults<T>((jr) =>
			{
				return GenericViewRowTransformer<T>.TransformRow(jr, ViewHandler.Client, _shouldLookupDocById);
			});
        }

        #endregion

		private string addIdToJson(string json, string id)
		{
			if (!json.Contains("\"_id\""))
			{
				return json.Insert(1, string.Concat("\"_id\":", "\"", id, "\","));
			}
			return json;
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