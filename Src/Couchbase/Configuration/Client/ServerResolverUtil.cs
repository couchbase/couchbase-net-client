using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Logging;
using Couchbase.Utils;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// This utility class will try to retrive a list of Server URIs to bootstrap the client with
    /// using an implementation of <see cref="IServerResolver"/>.
    /// </summary>
    public static class ServerResolverUtil
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ServerResolverUtil));

        public static List<Uri> GetServers(string serverResolverType)
        {
            if (string.IsNullOrEmpty(serverResolverType))
            {
                Log.Debug(ExceptionUtil.MissingOrEmptyServerResolverType);
                return null;
            }

            var resolver = GetServerResolver(serverResolverType);

            List<Uri> servers;
            try
            {
                servers = resolver.GetServers();
            }
            catch (Exception exception)
            {
                Log.Error(ExceptionUtil.ErrorRetrievingServersUsingServerResolver.WithParams(serverResolverType));
                Log.Error("Error getting list of servers", exception);
                throw;
            }

            if (servers == null || !servers.Any())
            {
                var message = ExceptionUtil.ServerResolverReturnedNoservers.WithParams(serverResolverType);
                Log.Error(message);
                throw new Exception(message);
            }

            return servers;
        }

        private static IServerResolver GetServerResolver(string serverResolverType)
        {
            var type = Type.GetType(serverResolverType, false);
            if (type == null)
            {
                var message = ExceptionUtil.UnrecognisedServerResolverType.WithParams(serverResolverType);
                Log.Error(message);
                throw new Exception(message);
            }

            var resolver = Activator.CreateInstance(type) as IServerResolver;
            if (resolver == null)
            {
                var message = ExceptionUtil.ServerResolverTypeDoesntImplementInterface.WithParams(serverResolverType, typeof(IServerResolver).Name);
                Log.Error(message);
                throw new Exception(message);
            }

            return resolver;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
