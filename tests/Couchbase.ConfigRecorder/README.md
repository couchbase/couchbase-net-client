# Config recorder

The recorder connects to a cluster with the SDK and records every cluster map that the server sends,
as raw bytes, plus what the SDK does with them. Run it while the cluster scales out and back in.
Then print a report with `summarize`.

## Steps

Step 1 starts at the repository root. All later commands run from this folder.

1. Build the tool.

   ```
   cd tests/Couchbase.ConfigRecorder
   dotnet build -c Release
   ```

2. Create the settings file and fill in the connection string, the database user, the password and the bucket.
   Git ignores `recorder.json` and the `runs` folder.

   ```
   cp recorder.example.json recorder.json
   ```

3. Start recording. Keep it running for the whole scale out and scale in.

   ```
   dotnet bin/Release/net10.0/Couchbase.ConfigRecorder.dll record
   ```

   Use `--config <path>` for another settings file. A status line prints every 5 s.
   Wait until it shows `probe N/N up`, where N is twice the number of data nodes.

4. Scale the cluster. Wait for each rebalance to finish.

5. Press Ctrl+C once. The tool stops the load, closes its connections, flushes and prints the run folder.
   SIGTERM does the same. A second Ctrl+C quits at once, and the events up to the last half second are on disk.

6. Print the report.

   ```
   dotnet bin/Release/net10.0/Couchbase.ConfigRecorder.dll summarize runs/<UTC timestamp>
   ```

## Settings

| Field | Default | Meaning |
| --- | --- | --- |
| `connectionString` | | `couchbases://` for Capella. |
| `username`, `password` | | A database user with read and write access to the bucket. The password is never written to the run folder. |
| `bucket` | | The bucket to open, load and probe. |
| `outputFolder` | `runs` | Each run writes to `<outputFolder>/<UTC timestamp>/`. |
| `probeIntervalMs` | 500 | How often the probe asks each node for its configs. |
| `sdkSampleIntervalMs` | 1000 | How often the SDK state is sampled. |
| `dnsSrvIntervalMs` | 10000 | How often the DNS SRV records are resolved. |
| `load.workers` | 64 | Closed-loop workers. 0 turns the load off. |
| `load.keyCount` | 10000 | Keys `cr-0` to `cr-<keyCount-1>` in the default collection. |
| `load.valueSize` | 256 | Bytes per value. |
| `acceptAnyServerCertificate` | false | Accepts any server certificate. Use it for local test clusters only. |

## What a run folder holds

- `events.jsonl` has one JSON object per line. Each line has `ts` (UTC wall clock), `t` (ms since start),
  `stream` and `kind`. Lines are written in arrival order, so sort by `t` when you read them.
- `configs/<sha256>.json` holds each distinct config body once. Events refer to it by `hash`.
- `sdk.log` holds the SDK logs at Debug level without redaction. The four Debug lines that the SDK writes for every
  KV operation are dropped, because under load they reach gigabytes per hour.
- `probe-sdk.log` holds the logs of the probe connections at Information level.

Streams.

- `tap` is the raw traffic on the SDK connections. Kinds are `connect`, `hello` (features granted),
  `select-bucket`, `config` (a GetClusterConfig reply with the known version that was sent), `push`
  (a clustermap change notification), `nmvb` (a not-my-vbucket reply with a config body) and `close`.
- `probe-tap` is the same for the probe connections. Replies with an unchanged body are left out.
- `probe` holds what each node served. One connection per node has no bucket selected and one has the bucket
  selected. The probe asks every `probeIntervalMs`, and at once when one of its connections receives a
  clustermap push. Kinds are `version` (a node served a new version), `latest`, `up`, `down`, `heartbeat` (every 10 s),
  `node-added` and `node-removed`.
- `sdk` holds each config the SDK config handler dispatched and the SDK state when it changed.
- `dns` holds the DNS SRV answers when they change.
- `load` holds one stats line per second.
- `recorder` holds start, stop and errors of the tool itself.

## Notes

- A failure in one part of the tool is recorded as a `recorder` `error` event and the other parts continue.
  If the tap cannot follow the frames of a connection, it stops tapping that connection only.
- The probe uses its own SDK context and connections. It does not touch the nodes or the state of the recorded
  SDK instance. It shares the static metric counters of the SDK with it.
- The applied times in the report come from the SDK state samples, so they are as precise as `sdkSampleIntervalMs`.
