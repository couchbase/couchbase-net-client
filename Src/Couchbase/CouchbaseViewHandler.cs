using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using Couchbase.Helpers;
using Couchbase.Exceptions;

namespace Couchbase
{
    internal class CouchbaseViewHandler
    {
        private const string ERROR_VIEW_NOT_FOUND = "not_found";

        protected static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbaseViewHandler));

        public ICouchbaseClient Client { get; private set; }

        public IHttpClientLocator ClientLocator { get; private set; }

        public string DesignDocument { get; private set; }

        public string ViewPath { get; private set; }

        public string IndexName { get; private set; }

        public int RetryCount { get; private set; }

        internal CouchbaseViewHandler(ICouchbaseClient client, IHttpClientLocator clientLocator, string designDocument, string indexName, int retryCount, string viewPath = "_view")
        {
            this.Client = client;
            this.ClientLocator = clientLocator;
            this.DesignDocument = designDocument;
            this.IndexName = indexName;
            this.ViewPath = viewPath;

            if (retryCount < 0 || retryCount > 10)
            {
                const string msg = "Must be greater than 0 and less than or equal to 10.";
                throw new ArgumentOutOfRangeException("retryCount", msg);
            }
            RetryCount = retryCount;
        }

        internal virtual bool UrlEncode { get; set; }

        public int TotalRows { get; set; }

        public IDictionary<string, object> DebugInfo { get; set; }

        internal IEnumerator<T> ReadResponse<T>(Stream stream, Func<JsonReader, T> rowTransformer)
        {
            using (var streamReader = new StreamReader(stream, Encoding.UTF8, true))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                while (jsonReader.Read())
                {
                    if (jsonReader.TokenType == JsonToken.PropertyName && jsonReader.Depth == 1)
                    {
                        if (jsonReader.TokenType == JsonToken.PropertyName
                            && jsonReader.Depth == 1
                            && ((string) jsonReader.Value) == "debug_info")
                        {
                            JToken debugInfoJson = (JToken.ReadFrom(jsonReader) as JProperty).Value;
                            DebugInfo = JsonHelper.Deserialize<Dictionary<string, object>>(debugInfoJson.ToString());
                        }
                        else if (jsonReader.Value as string == "total_rows" && jsonReader.Read())
                        {
                            TotalRows = Convert.ToInt32(jsonReader.Value);
                        }
                        else if (jsonReader.Value as string == "error" && jsonReader.Read())
                        {
                            var error = jsonReader.Value as string;
                            string reason = "";
                            while (jsonReader.Read())
                            {
                                if (jsonReader.TokenType == JsonToken.PropertyName &&
                                    jsonReader.Value as string == "reason" && jsonReader.Read())
                                {
                                    reason = jsonReader.Value.ToString();
                                }
                            }

                            //When requesting a bad design document, the response will be a 404 with the error == "not_found"
                            //When requesting a bad view name and a valid design doc, response will be a 500 with a reason containing "not_found"
                            if (error == ERROR_VIEW_NOT_FOUND || reason.Contains(ERROR_VIEW_NOT_FOUND))
                            {
                                throw new ViewNotFoundException(DesignDocument, IndexName, error, reason);
                            }
                            throw new ViewException(DesignDocument, IndexName, error, reason);
                        }
                        else if (jsonReader.Value as string == "rows" && jsonReader.Read())
                        {
                            // position the reader on the first "rows" field which contains the actual resultset
                            // this way we do not have to deserialize the whole response twice
                            // read until the end of the rows array
                            while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
                            {
                                T row = rowTransformer(jsonReader);
                                yield return row;
                            }

                            while (jsonReader.Read())
                            {
                                if (jsonReader.TokenType == JsonToken.PropertyName
                                    && jsonReader.Depth == 1
                                    && ((string) jsonReader.Value) == "errors")
                                {
                                    // we skip the deserialization if the array is null
                                    if (jsonReader.Read() && jsonReader.TokenType == JsonToken.StartArray)
                                    {
                                        object errors = Json.Parse(jsonReader);
                                        string formatted = String.Join("\n", FormatErrors(errors as object[]).ToArray());
                                        if (String.IsNullOrEmpty(formatted)) formatted = "<unknown>";
                                        throw new ViewException("Cannot read view: " + formatted);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public IEnumerator<T> TransformResults<T>(Func<JsonReader, T> rowTransformer, IDictionary<string, string> viewParams)
        {
            IHttpResponse response = null;
            Stream stream = null;

            try
            {
                response = GetResponse(viewParams);
                stream = response.GetResponseStream();

                if (ShouldRetry(response.StatusCode, stream))
                {
                    var attempts = 0;
                    var retry = false;

                    do
                    {
                        var timeout = (int) Math.Pow(2, attempts);
                        Thread.Sleep(timeout);

                        response = GetResponse(viewParams);
                        stream = response.GetResponseStream();
                        retry = ShouldRetry(response.StatusCode, stream);
                    } while (retry && attempts++ < RetryCount);
                }
            }
            catch (Exception e)
            {
                log.Error(e);
                throw;
            }

            return ReadResponse(stream, rowTransformer);
        }

        private bool ShouldRetry(HttpStatusCode statusCode, Stream stream)
        {
            bool retry;
            switch (statusCode)
            {
                case HttpStatusCode.OK:
                    retry = false;
                    break;
                //300's
                case HttpStatusCode.MultipleChoices:
                case HttpStatusCode.MovedPermanently:
                case HttpStatusCode.Found:
                case HttpStatusCode.SeeOther:
                case HttpStatusCode.NotModified:
                case HttpStatusCode.TemporaryRedirect:
                    log.DebugFormat("Document request returned {0}, retrying", statusCode);
                    retry = true;
                    break;
                //400's
                case HttpStatusCode.NotFound:
                    retry = Check404ForRetry(stream);
                    break;

                case HttpStatusCode.RequestTimeout:
                case HttpStatusCode.Conflict:
                case HttpStatusCode.PreconditionFailed:
                case HttpStatusCode.RequestedRangeNotSatisfiable:
                case HttpStatusCode.ExpectationFailed:
                    log.WarnFormat("Document request returned {0}, retrying", statusCode);
                    retry = true;
                    break;
                //500's
                case HttpStatusCode.InternalServerError:
                    retry = Check500ForRetry(stream);
                    if (retry)
                    {
                        log.WarnFormat("Document request returned {0}, retrying", statusCode);
                    }
                    break;

                case HttpStatusCode.NotImplemented:
                case HttpStatusCode.BadGateway:
                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.GatewayTimeout:
                    log.WarnFormat("Document request returned {0}, retrying", statusCode);
                    retry = true;
                    break;

                default:
                    retry = false;
                    break;
            }
            return retry;
        }

        private static Stream CopyStream(Stream stream)
        {
            const int chunkSize = 256;
            var buffer = new byte[chunkSize];
            var ms = new MemoryStream();

            var count = stream.Read(buffer, 0, chunkSize);
            while (count > 0)
            {
                ms.Write(buffer, 0, count);
                count = stream.Read(buffer, 0, chunkSize);
            }
            ms.Position = 0;
            return ms;
        }

        private bool Check404ForRetry(Stream stream)
        {
            var retry = true;

            try
            {
                using (var reader = new StreamReader(CopyStream(stream)))
                {
                    var body = reader.ReadToEnd();
                    if (body.Contains("not_found") &&
                        body.Contains("missing") ||
                        body.Contains("deleted"))
                    {
                        log.WarnFormat("Design document not found, body: {0}", body);
                        retry = false;
                    }
                }
            }
            catch (Exception e)
            {
                retry = false;
                log.Warn(e);
            }
            finally
            {
                stream.Position = 0;
            }
            return retry;
        }

        private bool Check500ForRetry(Stream stream)
        {
            var retry = true;

            try
            {
                using (var reader = new StreamReader(CopyStream(stream)))
                {
                    var body = reader.ReadToEnd();
                    if (body.Contains("error") &&
                        body.Contains("{not_found, missing_named_view}"))
                    {
                        log.WarnFormat("Design document not found, body: {0}", body);
                        retry = false;
                    }
                }
            }
            catch (Exception e)
            {
                retry = false;
                log.Warn(e);
            }
            finally
            {
                stream.Position = 0;
            }
            return retry;
        }

        public bool CheckViewExists()
        {
            var client = ClientLocator.Locate();
            var request = client.CreateRequest(this.DesignDocument + "/");
            var response = request.GetResponse();

            using (var sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8, true))
            using (var jsonReader = new JsonTextReader(sr))
            {
                while (jsonReader.Read())
                {
                    if (jsonReader.TokenType == JsonToken.PropertyName
                                             && jsonReader.Depth == 1
                                             && ((string)jsonReader.Value) == "views")
                    {
                        while (jsonReader.Read())
                        {
                            if (jsonReader.TokenType == JsonToken.PropertyName && (string)jsonReader.Value == IndexName)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        protected static IEnumerable<string> FormatErrors(object[] list)
        {
            if (list == null || list.Length == 0)
                yield break;

            foreach (IDictionary<string, object> error in list)
            {
                object reason;
                object from;

                if (!error.TryGetValue("from", out from)) continue;
                if (!error.TryGetValue("reason", out reason)) continue;

                yield return from + ": " + reason;
            }
        }

        /// <summary>
        /// Builds the request uri based on the parameters set by the user
        /// </summary>
        /// <returns></returns>
        public IHttpRequest CreateRequest(IHttpClient client, Dictionary<string, string> viewParams = null)
        {
            var retval = client.CreateRequest(this.DesignDocument + "/" + this.ViewPath + "/" + this.IndexName);
            return retval;
        }

        public IHttpResponse GetResponse(IDictionary<string, string> viewParams)
        {
            Debug.Assert(this.ClientLocator != null);

            var client = this.ClientLocator.Locate();
            if (client == null)
            {
                var message = String.Format("View {0} was mapped to a dead node, failing.", IndexName);
                if (log.IsErrorEnabled)
                    log.Warn(message);

                throw new InvalidOperationException(message);
            }

            var request = CreateRequest(client);

            if (viewParams != null)
            {
                foreach (var param in viewParams)
                {
                    var key = param.Key;
                    var value = param.Value;
                    if (UrlEncode)
                    {
                        key = HttpUtility.UrlEncode(key);
                        value = HttpUtility.UrlEncode(value);
                    }
                    request.AddParameter(key, value);
                }
            }

            return request.GetResponse();
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