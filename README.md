<div align="center">

<img src="assets/couchbase-logo.svg" alt="Couchbase logo" width="100" height="100">

# Couchbase .NET SDK

The official .NET client for [Couchbase Server](https://www.couchbase.com/products/server/) and [Couchbase Capella](https://www.couchbase.com/products/capella/).

[![NuGet](https://img.shields.io/nuget/v/CouchbaseNetClient?logo=nuget&style=flat-square)](https://www.nuget.org/packages/CouchbaseNetClient)
[![Downloads](https://img.shields.io/nuget/dt/CouchbaseNetClient?style=flat-square)](https://www.nuget.org/packages/CouchbaseNetClient)
[![Build](https://img.shields.io/github/actions/workflow/status/couchbase/couchbase-net-client/build-and-test.yml?branch=master&style=flat-square&label=build)](https://github.com/couchbase/couchbase-net-client/actions/workflows/build-and-test.yml)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue?style=flat-square)](LICENSE)

[Documentation](https://docs.couchbase.com/dotnet-sdk/current/hello-world/overview.html) ·
[API Reference](https://docs.couchbase.com/sdk-api/couchbase-net-client/) ·
[Release Notes](https://docs.couchbase.com/dotnet-sdk/current/project-docs/sdk-release-notes.html) ·
[Forums](https://www.couchbase.com/forums/c/net-sdk/6) ·
[Discord](https://discord.com/invite/sQ5qbPZuTh)

</div>

## Install

```bash
dotnet add package CouchbaseNetClient
```

See the [compatibility page](https://docs.couchbase.com/dotnet-sdk/current/project-docs/compatibility.html) for supported .NET versions and Couchbase Server versions.

## Quick start

```csharp
using Couchbase;

await using var cluster = await Cluster.ConnectAsync(
    "couchbase://localhost", "Administrator", "password");

var bucket = await cluster.BucketAsync("travel-sample");
var collection = await bucket.DefaultCollectionAsync();

// Write and read a document
await collection.UpsertAsync("user::1", new User("Ada", "C#"));
var result = await collection.GetAsync("user::1");
Console.WriteLine(result.ContentAs<User>());

// Run a SQL++ query
var query = await cluster.QueryAsync<User>(
    "SELECT u.* FROM `travel-sample` u USE KEYS 'user::1'");
await foreach (var row in query.Rows)
{
    Console.WriteLine(row);
}

record User(string Name, string Language);
```

To connect to Capella, use the `couchbases://` connection string from the Capella UI.
The [getting started guide](https://docs.couchbase.com/dotnet-sdk/current/hello-world/start-using-sdk.html) shows the full setup for Capella and for a local cluster.

## Transactions

A transaction changes several documents as one unit. Either all the changes commit, or none do.

```csharp
await collection.UpsertAsync("account::alice", new Account("Alice", 100));
await collection.UpsertAsync("account::bob", new Account("Bob", 0));

// Move 20 from Alice to Bob
await cluster.Transactions.RunAsync(async ctx =>
{
    var aliceDoc = await ctx.GetAsync(collection, "account::alice");
    var bobDoc = await ctx.GetAsync(collection, "account::bob");

    var alice = aliceDoc.ContentAs<Account>()!;
    var bob = bobDoc.ContentAs<Account>()!;

    await ctx.ReplaceAsync(aliceDoc, alice with { Balance = alice.Balance - 20 });
    await ctx.ReplaceAsync(bobDoc, bob with { Balance = bob.Balance + 20 });
});

record Account(string Owner, decimal Balance);
```

## Learn more

- [Getting started](https://docs.couchbase.com/dotnet-sdk/current/hello-world/start-using-sdk.html)
- [Key Value operations](https://docs.couchbase.com/dotnet-sdk/current/howtos/kv-operations.html)
- [SQL++ queries](https://docs.couchbase.com/dotnet-sdk/current/howtos/sqlpp-queries-with-sdk.html)
- [Transactions](https://docs.couchbase.com/dotnet-sdk/current/howtos/distributed-acid-transactions-from-the-sdk.html)
- [Managing connections](https://docs.couchbase.com/dotnet-sdk/current/howtos/managing-connections.html)
- [API reference](https://docs.couchbase.com/sdk-api/couchbase-net-client/)
- [Release notes](https://docs.couchbase.com/dotnet-sdk/current/project-docs/sdk-release-notes.html)

## Packages

| Package | Description |
| --- | --- |
| [CouchbaseNetClient](https://www.nuget.org/packages/CouchbaseNetClient) | The core SDK |
| [Couchbase.Extensions.DependencyInjection](https://www.nuget.org/packages/Couchbase.Extensions.DependencyInjection) | Register the cluster and buckets with `Microsoft.Extensions.DependencyInjection` |
| [Couchbase.Extensions.OpenTelemetry](https://www.nuget.org/packages/Couchbase.Extensions.OpenTelemetry) | Export SDK traces and metrics with OpenTelemetry |

## Related projects

| Project | Description | Links |
| --- | --- | --- |
| Operational Insights .NET SDK | Connect to an Operational Insights cluster and run SQL++ queries | [Docs](https://docs.couchbase.com/dotnet-operational-insights-sdk/current/hello-world/overview.html) · [NuGet](https://www.nuget.org/packages/Couchbase.OperationalInsightsClient) |
| EF Core provider | Use Couchbase as an Entity Framework Core database | [Docs](https://docs.couchbase.com/efcore-provider/current/overview.html) · [NuGet](https://www.nuget.org/packages/Couchbase.EntityFrameworkCore) · [GitHub](https://github.com/couchbaselabs/couchbase-efcore-provider) |

## Get help

- Ask questions on the [Couchbase Forums](https://www.couchbase.com/forums/c/net-sdk/6) or on [Discord](https://discord.com/invite/sQ5qbPZuTh).
- Report bugs in the [NCBC issue tracker](https://jira.issues.couchbase.com/browse/NCBC).

## Contributing

We welcome contributions. Read [CONTRIBUTING.md](CONTRIBUTING.md) before you open a pull request.

## License

Apache 2.0. See [LICENSE](LICENSE).
