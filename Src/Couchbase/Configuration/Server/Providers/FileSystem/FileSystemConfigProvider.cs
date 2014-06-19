using System;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;

namespace Couchbase.Configuration.Server.Providers.FileSystem
{
    internal class FileSystemConfigProvider : IConfigProvider
    {
        private readonly ClientConfiguration _clientConfig;
        private IConfigInfo _configInfo;
        private IServerConfig _serverConfig;
        private readonly string _bootstrapPath;

        public FileSystemConfigProvider(ClientConfiguration clientConfig)
        {
            _clientConfig = clientConfig;
        }

        public FileSystemConfigProvider(IServerConfig serverConfig, ClientConfiguration clientConfig, string bootstrapPath)
        {
            _serverConfig = serverConfig;
            _clientConfig = clientConfig;
            _bootstrapPath = bootstrapPath;
        }

        public IConfigInfo GetCached(string bucketName)
        {
            return _configInfo;
        }

        public IConfigInfo GetConfig(string bucketName)
        {
            if (_serverConfig == null)
            {
                _serverConfig = new FileSystemConfig(_bootstrapPath);
            }
            _configInfo = new ConfigInfo(_serverConfig, _clientConfig);
            return _configInfo;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void RegisterListener(IConfigObserver observer)
        {
            throw new NotImplementedException();
        }


        public void UnRegisterObserver(IConfigObserver observer)
        {
            throw new NotImplementedException();
        }

        public bool ObserverExists(IConfigObserver observer)
        {
            throw new NotImplementedException();
        }


        bool IConfigProvider.RegisterObserver(IConfigObserver observer)
        {
            throw new NotImplementedException();
        }


        public IConfigInfo GetConfig(string name,  string password)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }


        public IByteConverter Converter
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }
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