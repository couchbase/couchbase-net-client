using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.WrappedClient;

public class ComboDemo
{
    private readonly ITestOutputHelper _outputHelper;

    public ComboDemo(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }
    internal record SampleDocument(string Id, DateTimeOffset UpdatedDate, string Stage);

    [Theory]
    [InlineData("protostellar://localhost")]
    [InlineData("couchbase://localhost")]
    public async Task ComboTest(string connectionString)
    {
        var clusterOptions = new ClusterOptions()
        {
            UserName = "Administrator",
            Password = "password"
        };

        var cluster = await StellarClient.ConnectAsync(connectionString, clusterOptions);
        var bucket = await cluster.BucketAsync("default");
        var scope = bucket.Scope("_default");
        var collection = scope.Collection("_default");
        _outputHelper.WriteLine($"Connected to cluster with connection string: {connectionString}");


        // Prep a document
        var sampleDoc = new SampleDocument(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, Stage: "Upsert");
        _outputHelper.WriteLine("Sample Document:");
        _outputHelper.WriteLine(sampleDoc.ToString());


        // upsert the document
        var upsertResult = await collection.UpsertAsync(sampleDoc.Id, sampleDoc);

        // get it and check it
        var getResult1 = await collection.GetAsync(sampleDoc.Id);
        _outputHelper.WriteLine($"Retrieved document  '{sampleDoc.Id}' with cas = {getResult1.Cas}");
        var deserialized1 = getResult1.ContentAs<SampleDocument>();
        Assert.NotNull(deserialized1);
        _outputHelper.WriteLine(deserialized1!.ToString());

        // query all the docs
        _outputHelper.WriteLine("== QUERY ALL THE DOCS ==");
        var queryResult = await cluster.QueryAsync<object>("SELECT * FROM `default`");
        await foreach (var result in queryResult)
        {
            _outputHelper.WriteLine(result.ToString());
        }
    }
}
