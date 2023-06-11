using System.Text.Json;
using Couchbase.Management.Eventing;
using Couchbase.Management.Eventing.Internal;
using Couchbase.UnitTests.Utils;
using Xunit;

namespace Couchbase.UnitTests.Management.Eventing
{
    public class EventingFunctionTests
    {
        [Fact]
        public void Test_Serialize()
        {
            var eventingFunction = new EventingFunction
            {
                Code = "",
                Settings = new()
                {
                },
                DeploymentConfig = new DeploymentConfig
                {
                    BucketBindings = new ()
                    {
                        new()
                        {
                            Access = EventingFunctionBucketAccess.ReadOnly,
                            Alias = "thealias",
                            BucketName = "thebucketname",
                            CollectionName = "thecollectionname",
                            ScopeName = "thescopename"
                        }
                    },
                    ConstantBindings = new ()
                    {
                        new()
                        {
                            Alias = "thealias",
                            Literal = "theliterial"
                        }
                    },
                    UrlBindings = new()
                    {
                        new ()
                        {
                            Alias = "thealias",
                            Auth = new EventingFunctionUrlAuthBasic
                            {
                                Password = "thepassword",
                                Username = "theusername"
                            },
                            AllowCookies = true,
                            Hostname = "thehostname",
                            ValidateSslCertificate = true
                        }
                    },
                    MetadataBucket = "themetdatabucket",
                    MetadataCollection = "themetadatacollection",
                    MetadataScope = "themetadatascope",
                    SourceBucket = "thesourcebucket",
                    SourceScope = "thesourcescope",
                    SourceCollection = "thesourcecollection"
                }
            };

            var json = eventingFunction.ToJson();
        }

        [Fact]
        public void Test_Deserialize()
        {
            var json =
                ResourceHelper.ReadResource(@"Documents\Eventing\eventing-function.json");

            var eventingFunction = JsonSerializer.Deserialize(json, EventingSerializerContext.Primary.EventingFunction);

        }
    }
}
