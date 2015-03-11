using System;
using System.Net;
using System.Threading.Tasks;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Views;

namespace Couchbase.Core
{
    internal interface IServer : IDisposable
    {
        string HostName { get; set; }

        uint QueryPort { get; set; }

        uint ViewPort { get; set; }

        uint DirectPort { get; }

        uint ProxyPort { get; }

        uint Replication { get; }

        bool Active { get; }

        bool Healthy { get; }

        IConnectionPool ConnectionPool { get; }

        IViewClient ViewClient { get; }

        IQueryClient QueryClient { get; }

        IPEndPoint EndPoint { get; }

        Task<IOperationResult<T>> SendAsync<T>(IOperation<T> operation);

        IOperationResult<T> Send<T>(IOperation<T> operation);

        IViewResult<T> Send<T>(IViewQuery query);

        Task<IViewResult<T>> SendAsync<T>(IViewQuery query);

        IQueryResult<T> Send<T>(IQueryRequest queryRequest);

        Task<IQueryResult<T>> SendAsync<T>(IQueryRequest queryRequest);

        IQueryResult<T> Send<T>(string query);

        Task<IQueryResult<T>> SendAsync<T>(string query);

        IQueryResult<IQueryPlan> Prepare(IQueryRequest toPrepare);

        IQueryResult<IQueryPlan> Prepare(string statementToPrepare);

        string GetBaseViewUri(string name);

        string GetBaseQueryUri();

        bool IsSecure { get; }

        bool IsDead { get; }

        void MarkDead();
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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