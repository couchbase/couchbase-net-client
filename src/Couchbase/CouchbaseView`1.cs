using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace Couchbase {
    internal class CouchbaseView<T> : CouchbaseViewBase<T> {

        internal CouchbaseView(IMemcachedClient client, IHttpClientLocator clientLocator, string designDocument, string indexName)
            : base(client, clientLocator, designDocument, indexName) { }

        protected CouchbaseView(CouchbaseView<T> original)
            : base(original) { }


        #region IEnumerable<T> Members

        public override IEnumerator<T> GetEnumerator() 
        {
            return TransformResults<T>((jr) => {
                    var jObject = Json.ParseValue(jr, "value");                    
                    return JsonConvert.DeserializeObject<T>(jObject);
                });
        }

        #endregion
    }
}
