using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Eventing;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests.Management.Management
{
    [Collection("NonParallel")]
    public class EventingFunctionManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public EventingFunctionManagerTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Test_GetAllFunctionsAsync()
        {
           var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
           var results = await eventingFunctionManager.GetAllFunctionsAsync();

           var eventingFunction = results.First();
           Assert.Equal("function OnUpdate(doc, meta) {\n  log('document', doc);\n  doc[\"ip_num_start\"] = get_numip_first_3_octets(doc[\"ip_start\"]);\n  doc[\"ip_num_end\"]   = get_numip_first_3_octets(doc[\"ip_end\"]);\n  tgt[meta.id]=doc;\n}\nfunction get_numip_first_3_octets(ip) {\n  var return_val = 0;\n  if (ip) {\n    var parts = ip.split('.');\n    //IP Number = A x (256*256*256) + B x (256*256) + C x 256 + D\n    return_val = (parts[0]*(256*256*256)) + (parts[1]*(256*256)) + (parts[2]*256) + parseInt(parts[3]);\n    return return_val;\n  }\n}", eventingFunction.Code);
           Assert.Equal("external", eventingFunction.Version);
           Assert.False(eventingFunction.EnforceSchema);
        }

        [Fact]
        public async Task Test_GetFunctionAsync()
        {
            var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            var result = await eventingFunctionManager.GetFunctionAsync("case_1_enrich_ips");

            Assert.Equal("function OnUpdate(doc, meta) {\n  log('document', doc);\n  doc[\"ip_num_start\"] = get_numip_first_3_octets(doc[\"ip_start\"]);\n  doc[\"ip_num_end\"]   = get_numip_first_3_octets(doc[\"ip_end\"]);\n  tgt[meta.id]=doc;\n}\nfunction get_numip_first_3_octets(ip) {\n  var return_val = 0;\n  if (ip) {\n    var parts = ip.split('.');\n    //IP Number = A x (256*256*256) + B x (256*256) + C x 256 + D\n    return_val = (parts[0]*(256*256*256)) + (parts[1]*(256*256)) + (parts[2]*256) + parseInt(parts[3]);\n    return return_val;\n  }\n}", result.Code);
            Assert.Equal("external", result.Version);
            Assert.False(result.EnforceSchema);
        }

        [Fact]
        public async Task Test_UpsertFunctionAsync_Ok()
        {
            var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            await eventingFunctionManager.UpsertFunctionAsync(new EventingFunction
            {
                MetaDataKeySpace = new EventingFunctionKeyspace("bulk", "data", "source"),
                SourceKeySpace = new EventingFunctionKeyspace("rr100", "eventing", "metadata"),
                Name = "case_1_enrich_ips",
                Code = "function OnUpdate(doc, meta) {\n  log('document', doc);\n  doc[\"ip_num_start\"] = get_numip_first_3_octets(doc[\"ip_start\"]);\n  doc[\"ip_num_end\"]   = get_numip_first_3_octets(doc[\"ip_end\"]);\n  tgt[meta.id]=doc;\n}\nfunction get_numip_first_3_octets(ip) {\n  var return_val = 0;\n  if (ip) {\n    var parts = ip.split('.');\n    //IP Number = A x (256*256*256) + B x (256*256) + C x 256 + D\n    return_val = (parts[0]*(256*256*256)) + (parts[1]*(256*256)) + (parts[2]*256) + parseInt(parts[3]);\n    return return_val;\n  }\n}",
            });
        }

        [Fact]
        public async Task Test_UpsertFunctionAsync_EventingCompilationFailureException()
        {
            var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            await Assert.ThrowsAsync<EventingFunctionCompilationFailureException>(async () =>
                await eventingFunctionManager.UpsertFunctionAsync(new EventingFunction
                {
                    MetaDataKeySpace = new EventingFunctionKeyspace("bulk", "data", "source"),
                    SourceKeySpace = new EventingFunctionKeyspace("rr100", "eventing", "metadata"),
                    Name = "case_1_enrich_ips",
                    Code =
                        "function OnUpdate(doc, meta) {\n  log('document', doc);\n  doc\"ip_num_start\"] = get_numip_first_3_octets(doc[\"ip_start\"]);\n  doc[\"ip_num_end\"]   = get_numip_first_3_octets(doc[\"ip_end\"]);\n  tgt[meta.id]=doc;\n}\nfunction get_numip_first_3_octets(ip) {\n  var return_val = 0;\n  if (ip) {\n    var parts = ip.split('.');\n    //IP Number = A x (256*256*256) + B x (256*256) + C x 256 + D\n    return_val = (parts[0]*(256*256*256)) + (parts[1]*(256*256)) + (parts[2]*256) + parseInt(parts[3]);\n    return return_val;\n  }\n}"
                }));
        }

        [Fact]
        public async Task Test_UpsertFunctionAsync_CollectionNotFoundException()
        {
            var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            await Assert.ThrowsAsync<Couchbase.Management.Collections.CollectionNotFoundException>(async () =>
                await eventingFunctionManager.UpsertFunctionAsync(new EventingFunction
                {
                    MetaDataKeySpace = new EventingFunctionKeyspace("bulk", "doesnotexist", "source"),
                    SourceKeySpace = new EventingFunctionKeyspace("rr100", "eventing", "metadata"),
                    Name = "case_1_enrich_ips",
                    Code =
                        "function OnUpdate(doc, meta) {\n  log('document', doc);\n  doc[\"ip_num_start\"] = get_numip_first_3_octets(doc[\"ip_start\"]);\n  doc[\"ip_num_end\"]   = get_numip_first_3_octets(doc[\"ip_end\"]);\n  tgt[meta.id]=doc;\n}\nfunction get_numip_first_3_octets(ip) {\n  var return_val = 0;\n  if (ip) {\n    var parts = ip.split('.');\n    //IP Number = A x (256*256*256) + B x (256*256) + C x 256 + D\n    return_val = (parts[0]*(256*256*256)) + (parts[1]*(256*256)) + (parts[2]*256) + parseInt(parts[3]);\n    return return_val;\n  }\n}"
                }));
        }

        [Fact]
        public async Task Test_UpsertFunctionAsync_BucketNotFoundException()
        {
            var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            await Assert.ThrowsAsync<BucketNotFoundException>(async () =>
                await eventingFunctionManager.UpsertFunctionAsync(new EventingFunction
                {
                    MetaDataKeySpace = new EventingFunctionKeyspace("doesnotexist", "data", "source"),
                    SourceKeySpace = new EventingFunctionKeyspace("rr100", "eventing", "metadata"),
                    Name = "case_1_enrich_ips",
                    Code =
                        "function OnUpdate(doc, meta) {\n  log('document', doc);\n  doc[\"ip_num_start\"] = get_numip_first_3_octets(doc[\"ip_start\"]);\n  doc[\"ip_num_end\"]   = get_numip_first_3_octets(doc[\"ip_end\"]);\n  tgt[meta.id]=doc;\n}\nfunction get_numip_first_3_octets(ip) {\n  var return_val = 0;\n  if (ip) {\n    var parts = ip.split('.');\n    //IP Number = A x (256*256*256) + B x (256*256) + C x 256 + D\n    return_val = (parts[0]*(256*256*256)) + (parts[1]*(256*256)) + (parts[2]*256) + parseInt(parts[3]);\n    return return_val;\n  }\n}"
                }));
        }

        [Fact]
        public async Task Test_DropFunctionAsync()
        {
            var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            await eventingFunctionManager.DropFunctionAsync("case_1_enrich_ips");
        }

        [Fact]
        public async Task Test_DeployFunctionAsync()
        {
            var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            await eventingFunctionManager.DeployFunctionAsync("case_1_enrich_ips");
        }

        [Fact]
        public async Task Test_PauseFunctionAsync()
        {
            var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            await eventingFunctionManager.PauseFunctionAsync("case_1_enrich_ips");
        }

        [Fact]
        public async Task Test_ResumeFunctionAsync()
        {
            var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            await eventingFunctionManager.ResumeFunctionAsync("case_1_enrich_ips");
        }

        [Fact]
        public async Task Test_UndeployFunctionAsync()
        {
            var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            await eventingFunctionManager.UndeployFunctionAsync("case_1_enrich_ips");
        }

        [Fact]
        public async Task Test_FunctionStatus()
        {
            var eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            var results = await eventingFunctionManager.FunctionsStatus();

            Assert.NotNull(results);
        }
    }
}
