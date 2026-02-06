# Couchbase .NET Event Counters

The Couchbase .NET SDK offers [metrics](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/metrics) to support
instrumenting your application. These metrics may be [collected](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/metrics-collection)
in a variety of ways such as the [dotnet-counters tool](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters),
the new [dotnet-monitor tool](https://devblogs.microsoft.com/dotnet/announcing-dotnet-monitor-in-net-6/), or instrumented
directly in code using the [MeterListener class](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meterlistener?view=net-6.0).

## Meter Types

### Gauge

Gauges show a snapshot of the value at a given point in time.

### Counter

Counters return a total that continues to increase over time. These are useful for calculating the rate of events over some period of time.

### Histogram

Typically used for measuring durations, this meter tracks distributions such as averages and 95th percentile.

## Meters

The following meters are exposed under the `CouchbaseNetClient` meter name.

| Instrument Name                   | Type      | Description |
| --------------------------------- | --------- | ----------- |
| db.couchbase.connections          | Gauge     | Total number of active connections to data nodes |
| db.couchbase.operations           | Histogram | Distribution of operation durations, in microseconds (legacy); the modern `db.client.operation.duration` uses seconds |
| db.couchbase.retries              | Counter   | Number of operation retries, excluding first attempts |
| db.couchbase.orphaned             | Counter   | Number of operations which were sent but for which a response was never received |
| db.couchbase.sendqueue.fullerrors | Counter   | Number of times a connection pool rejected an operation because the send queue was full |
| db.couchbase.sendqueue.length     | Gauge     | Total number of items waiting to be sent |
| db.couchbase.timeouts             | Counter   | Number of operations that failed due to a client-side timeout |

## Tags

Output measurements to these meters may be tagged with additional data.

| Key                    | Description |
| ---------------------- | ----------- |
| db.couchbase.service   | Service involved, such as "kv", "query", "search", "analytics" |
| db.couchbase.operation | Type of data operation, such as "get" |
