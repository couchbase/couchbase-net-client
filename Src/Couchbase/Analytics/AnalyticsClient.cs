using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Authentication;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Logging;
using Couchbase.N1QL;
using Couchbase.Utils;
using Couchbase.Views;

namespace Couchbase.Analytics
{
    internal class AnalyticsClient : IAnalyticsClient
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AnalyticsClient));

        private readonly HttpClient _client;
        private readonly IDataMapper _dataMapper;
        private readonly ClientConfiguration _configuration;

        public AnalyticsClient(HttpClient client, IDataMapper dataMapper, ClientConfiguration configuration)
        {
            _client = client;
            _dataMapper = dataMapper;
            _configuration = configuration;
        }

        /// <summary>
        /// Queries the specified request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        /// <returns></returns>
        public IAnalyticsResult<T> Query<T>(IAnalyticsRequest request)
        {
            using (new SynchronizationContextExclusion())
            {
                return QueryAsync<T>(request, CancellationToken.None).Result;
            }
        }

        /// <summary>
        /// Queries the asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queryRequest">The query request.</param>
        /// <param name="token">The token.</param>
        /// <returns></returns>
        public async Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest queryRequest, CancellationToken token)
        {
            var result = new AnalyticsResult<T>();

            FailureCountingUri baseUri;
            if (!TryGetUri(result, out baseUri))
            {
                return result;
            }

            ApplyCredentials(queryRequest, _configuration);

            using (var content = new StringContent(queryRequest.GetFormValuesAsJson(), System.Text.Encoding.UTF8, MediaType.Json))
            {
                try
                {
                    Log.Trace("Sending analytics query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                    var request = await _client.PostAsync(baseUri, content, token).ContinueOnAnyContext();
                    using (var response = await request.Content.ReadAsStreamAsync().ContinueOnAnyContext())
                    {
                        result = _dataMapper.Map<AnalyticsResultData<T>>(response).ToQueryResult();
                        result.Success = result.Status == QueryStatus.Success;
                        result.HttpStatusCode = request.StatusCode;
                        Log.Trace("Received analytics query cid{0}: {1}", result.ClientContextId, result.ToString());
                    }
                    baseUri.ClearFailed();
                }
                catch (HttpRequestException e)
                {
                    Log.Info("Failed analytics query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                    baseUri.IncrementFailed();
                    ProcessError(e, result);
                    Log.Error(e);
                }
                catch (AggregateException ae)
                {
                    ae.Flatten().Handle(e =>
                    {
                        Log.Info("Failed analytics query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                        ProcessError(e, result);
                        return true;
                    });
                }
                catch (Exception e)
                {
                    Log.Info("Failed analytics query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                    Log.Info(e);
                    ProcessError(e, result);
                }
            }

            return result;
        }

        private static void ProcessError<T>(Exception exception, AnalyticsResult<T> queryResult)
        {
            const string message = "Check Exception and Error fields for details.";
            queryResult.Status = QueryStatus.Fatal;
            queryResult.HttpStatusCode = HttpStatusCode.BadRequest;
            queryResult.Success = false;
            queryResult.Message = message;
            queryResult.Exception = exception;
        }

        private static bool TryGetUri<T>(AnalyticsResult<T> result, out FailureCountingUri uri)
        {
            uri = ConfigContextBase.GetAnalyticsUri();
            if (uri != null && !string.IsNullOrEmpty(uri.AbsoluteUri))
            {
                return true;
            }

            Log.Error(ExceptionUtil.EmptyUriTryingSubmitN1qlQuery);
            ProcessError(new InvalidOperationException(ExceptionUtil.EmptyUriTryingSubmitN1QlQuery), result);
            return false;
        }

        private static void ApplyCredentials(IAnalyticsRequest request, ClientConfiguration config)
        {
            if (config.HasCredentials)
            {
                var creds = config.GetCredentials(AuthContext.ClusterAnalytics);
                foreach (var cred in creds)
                {
                    request.Credentials(cred.Key, cred.Value, false);
                }
            }
        }
    }
}
