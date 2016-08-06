using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [SetUpFixture]
    public class GlobalSetup
    {
        public static readonly List<Action> TearDownSteps = new List<Action>();

        [OneTimeSetUp]
        public void BuildPrimaryIndexes()
        {
            using (var cluster = new Cluster(new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            }))
            {
                //assumes 4.0 server with index service installed.
                using (var bucket = cluster.OpenBucket())
                {
                    Console.WriteLine("Checking indexes...");
                    var indexes = bucket.Query<dynamic>("SELECT i.* FROM system:indexes as i WHERE i.is_primary;");
                    if (!indexes.Rows.Exists(x => x.keyspace_id == "travel-sample"))
                    {
                        Console.WriteLine("Creating primary index for travel-sample.");
                        bucket.Query<dynamic>("CREATE PRIMARY INDEX on `travel-sample` USING GSI;");
                    }
                    if (!indexes.Rows.Exists(x => x.keyspace_id == "beer-sample"))
                    {
                        Console.WriteLine("Creating primary index for beer-sample.");
                        bucket.Query<dynamic>("CREATE PRIMARY INDEX on `beer-sample` USING GSI;");
                    }
                    if (!indexes.Rows.Exists(x => x.keyspace_id == "default"))
                    {
                        Console.WriteLine("Creating primary index for default.");
                        bucket.Query<dynamic>("CREATE PRIMARY INDEX on `default` USING GSI;");
                    }
                    if (!indexes.Rows.Exists(x => x.keyspace_id == "authenticated"))
                    {
                        Console.WriteLine("Creating primary index for authenticated.");
                        bucket.Query<dynamic>("CREATE PRIMARY INDEX on `authenticated` USING GSI;");
                    }
                }
            }
        }

        [OneTimeTearDown]
        public void RunTearDownSteps()
        {
            foreach (var step in TearDownSteps)
            {
                try
                {
                    step.Invoke();
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                }
            }

            TearDownSteps.Clear();
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
