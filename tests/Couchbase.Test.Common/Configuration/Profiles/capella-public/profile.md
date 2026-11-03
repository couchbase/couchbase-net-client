# Profile capella-public

The `reduce` command of the config recorder writes this file from profile.json. Do not edit it.

## Source

Server 8.0.3, captured 2026-11-03.

| Run | TLS | Length (s) |
| --- | --- | --- |
| 20261103T102216Z | yes | 880 |

| Kind | Distinct bodies | Versions with a body |
| --- | --- | --- |
| cluster | 81 | 23 |
| bucket | 279 | 149 |

## Fields that vary

perNode means the nodes of one body disagree, or some nodes do not have the field. perVersion means the field changes between bodies, or some bodies do not have it. All other fields are constant and the template holds them.

| Kind | Path | Scope | Distinct | Share | Samples |
| --- | --- | --- | --- | --- | --- |
| cluster | `nodesExt[].hostname` | perNode | 5 | 1 | "node-0.example.com", "node-1.example.com", "node-2.example.... |
| cluster | `nodesExt[].nodeUUID` | perNode | 5 | 1 | "a0000000000000000000000000000000", "a0000000000000000000000... |
| cluster | `nodesExt[].serverGroup` | perNode | 3 | 1 | "group:1", "group:2", "group:3" |
| cluster | `nodesExt[].services.indexAdmin` | perNode | 1 | 1 | 9100 |
| cluster | `nodesExt[].services.indexHttp` | perNode | 1 | 1 | 9102 |
| cluster | `nodesExt[].services.indexHttps` | perNode | 1 | 1 | 19102 |
| cluster | `nodesExt[].services.indexScan` | perNode | 1 | 1 | 9101 |
| cluster | `nodesExt[].services.indexStreamCatchup` | perNode | 1 | 1 | 9104 |
| cluster | `nodesExt[].services.indexStreamInit` | perNode | 1 | 1 | 9103 |
| cluster | `nodesExt[].services.indexStreamMaint` | perNode | 1 | 1 | 9105 |
| cluster | `nodesExt[].services.n1ql` | perNode | 1 | 1 | 8093 |
| cluster | `nodesExt[].services.n1qlSSL` | perNode | 1 | 1 | 18093 |
| cluster | `nodesExt[].thisNode` | perNode | 1 | 0.975 | true |
| cluster | `rev` | perVersion | 23 | 1 | 135, 234, 238, 251, 256 |
| bucket | `nodesExt[].hostname` | perNode | 5 | 1 | "node-0.example.com", "node-1.example.com", "node-2.example.... |
| bucket | `nodesExt[].nodeUUID` | perNode | 5 | 1 | "a0000000000000000000000000000000", "a0000000000000000000000... |
| bucket | `nodesExt[].serverGroup` | perNode | 3 | 1 | "group:1", "group:2", "group:3" |
| bucket | `nodesExt[].services.indexAdmin` | perNode | 1 | 1 | 9100 |
| bucket | `nodesExt[].services.indexHttp` | perNode | 1 | 1 | 9102 |
| bucket | `nodesExt[].services.indexHttps` | perNode | 1 | 1 | 19102 |
| bucket | `nodesExt[].services.indexScan` | perNode | 1 | 1 | 9101 |
| bucket | `nodesExt[].services.indexStreamCatchup` | perNode | 1 | 1 | 9104 |
| bucket | `nodesExt[].services.indexStreamInit` | perNode | 1 | 1 | 9103 |
| bucket | `nodesExt[].services.indexStreamMaint` | perNode | 1 | 1 | 9105 |
| bucket | `nodesExt[].services.n1ql` | perNode | 1 | 1 | 8093 |
| bucket | `nodesExt[].services.n1qlSSL` | perNode | 1 | 1 | 18093 |
| bucket | `nodesExt[].thisNode` | perNode | 1 | 0.993 | true |
| bucket | `nodes[].couchApiBase` | perNode | 5 | 1 | "http://node-0.example.com:8092/recorder%2Bb0000000000000000... |
| bucket | `nodes[].hostname` | perNode | 5 | 1 | "node-0.example.com:8091", "node-1.example.com:8091", "node-... |
| bucket | `rev` | perVersion | 149 | 1 | 211, 234, 238, 251, 256 |
| bucket | `vBucketServerMap.serverList[]` | perNode | 5 | 1 | "node-0.example.com:11210", "node-1.example.com:11210", "nod... |
| bucket | `vBucketServerMap.vBucketMapForward[][]` | perVersion | 5 | 0.659 | 0, 1, 2, 3, 4 |
| bucket | `vBucketServerMap.vBucketMap[][]` | perVersion | 5 | 1 | 0, 1, 2, 3, 4 |

## Fields the SDK does not model

The SDK config classes have no property for these paths, so the SDK drops them.

| Kind | Path | Scope | Samples |
| --- | --- | --- | --- |
| cluster | `prodName` | constant | "Couchbase Server" |
| bucket | `bucketType` | constant | "membase" |
| bucket | `collectionsManifestUid` | constant | "1" |
| bucket | `numVBuckets` | constant | 1024 |
| bucket | `prodName` | constant | "Couchbase Server" |
| bucket | `storageBackend` | constant | "couchstore" |

## SDK fields the server never sends

No body of this profile has these paths. The SDK keeps their default values.

- `networkResolution`
- `nodesExt[].alternateAddresses.*.hostname`
- `nodesExt[].alternateAddresses.*.ports.capi`
- `nodesExt[].alternateAddresses.*.ports.capiSSL`
- `nodesExt[].alternateAddresses.*.ports.cbas`
- `nodesExt[].alternateAddresses.*.ports.cbasSSL`
- `nodesExt[].alternateAddresses.*.ports.eventingAdminPort`
- `nodesExt[].alternateAddresses.*.ports.eventingSSL`
- `nodesExt[].alternateAddresses.*.ports.fts`
- `nodesExt[].alternateAddresses.*.ports.ftsSSL`
- `nodesExt[].alternateAddresses.*.ports.indexAdmin`
- `nodesExt[].alternateAddresses.*.ports.indexHttp`
- `nodesExt[].alternateAddresses.*.ports.indexHttps`
- `nodesExt[].alternateAddresses.*.ports.indexScan`
- `nodesExt[].alternateAddresses.*.ports.indexStreamCatchup`
- `nodesExt[].alternateAddresses.*.ports.indexStreamInit`
- `nodesExt[].alternateAddresses.*.ports.indexStreamMaint`
- `nodesExt[].alternateAddresses.*.ports.kv`
- `nodesExt[].alternateAddresses.*.ports.kvSSL`
- `nodesExt[].alternateAddresses.*.ports.mgmt`
- `nodesExt[].alternateAddresses.*.ports.mgmtSSL`
- `nodesExt[].alternateAddresses.*.ports.moxi`
- `nodesExt[].alternateAddresses.*.ports.n1ql`
- `nodesExt[].alternateAddresses.*.ports.n1qlSSL`
- `nodesExt[].alternateAddresses.*.ports.projector`
- `nodesExt[].appTelemetryPath`
- `nodesExt[].services.cbas`
- `nodesExt[].services.cbasSSL`
- `nodesExt[].services.eventingAdminPort`
- `nodesExt[].services.eventingSSL`
- `nodesExt[].services.fts`
- `nodesExt[].services.ftsSSL`
- `nodesExt[].services.moxi`
- `nodes[].couchApiBaseHttps`
- `nodes[].ports.httpsCAPI`
- `nodes[].ports.httpsMgmt`
- `nodes[].ports.proxy`
- `nodes[].ports.sslDirect`
- `nodes[].services[]`
- `nodes[].version`

## Nodes

Ports in nodesExt.services, the same on every node unless a list is shown.

| Key | Port |
| --- | --- |
| capi | 8092 |
| capiSSL | 18092 |
| indexAdmin | 9100 |
| indexHttp | 9102 |
| indexHttps | 19102 |
| indexScan | 9101 |
| indexStreamCatchup | 9104 |
| indexStreamInit | 9103 |
| indexStreamMaint | 9105 |
| kv | 11210 |
| kvSSL | 11207 |
| mgmt | 8091 |
| mgmtSSL | 18091 |
| n1ql | 8093 |
| n1qlSSL | 18093 |
| projector | 9999 |

| Service set | Services | Keys |
| --- | --- | --- |
| 0 | capi, index, kv, mgmt, n1ql, projector | 16 |
| 1 | capi, kv, mgmt, projector | 7 |
| 2 | capi, kv, mgmt, n1ql, projector | 9 |
| 3 | capi, index, kv, mgmt, projector | 14 |

Service sets over the life of each member. A member is one node with one nodeUUID.

| Service sets in order | Joined during a run | Left during a run | Members |
| --- | --- | --- | --- |
| 0 then 3 then 1 | no | yes | 0 |
| 0 | no | no | 1, 2 |
| 1 then 2 then 0 | yes | no | 3 |
| 1 then 2 then 0 then 3 then 1 | yes | yes | 4 |

Server groups follow the pattern `group:{n}` with the values group:1, group:2, group:3.
In join order the members are in group:1, group:2, group:3, group:1, group:2.

No node has alternate addresses.

## Transitions

Each step compares the first body of a version with the first body of the version before it, without thisNode.

### cluster

22 steps between 23 versions with a body. Any source delivered 26 versions.
Gap between versions with a body median 2776, p95 258936, max 273936 ms. Gap between all versions median 641, p95 258936, max 273936 ms.

| Label | Steps |
| --- | --- |
| nodeAddedToNodesExt | 1 |
| nodeRemovedFromNodesExt | 2 |
| servicesChanged | 5 |
| revOnly | 14 |

Bodies without thisNode.

| Run | Rev | Sent by | Sender in nodesExt | Source | First body of the version |
| --- | --- | --- | --- | --- | --- |
| 0 | 595 | node-0.example.com | no | probe-tap config | yes |
| 0 | 740 | node-4.example.com | no | probe-tap config | yes |

### bucket

148 steps between 149 versions with a body. Any source delivered 391 versions.
Gap between versions with a body median 145, p95 2841, max 271324 ms. Gap between all versions median 40, p95 248, max 271324 ms.
Steps that move vbuckets move median 11, p95 35, max 50 vbuckets, 1533 in total.

| Label | Steps |
| --- | --- |
| nodeAddedToNodesExt | 1 |
| nodeRemovedFromNodesExt | 2 |
| nodeAddedToNodes | 1 |
| nodeRemovedFromNodes | 2 |
| nodesExtOrderChanged | 1 |
| servicesChanged | 6 |
| serverListChanged | 3 |
| vbucketMapChanged | 116 |
| forwardMapSet | 3 |
| forwardMapCleared | 3 |
| revOnly | 17 |

Rebalances. One rebalance is the versions with a forward map, the node additions before them and the node removals after them.

| Run | From rev | To rev | Versions with a body | Versions seen | With forward map | Seconds | vbuckets moved | Active moved |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 277 | 447 | 54 | 162 | 51 | 8.7 | 650 | 410 |
| 0 | 491 | 595 | 29 | 93 | 25 | 34.9 | 417 | 275 |
| 0 | 617 | 740 | 46 | 112 | 42 | 35.4 | 466 | 256 |

Bodies without thisNode.

| Run | Rev | Sent by | Sender in nodesExt | Source | First body of the version |
| --- | --- | --- | --- | --- | --- |
| 0 | 595 | node-0.example.com | no | probe-tap config | yes |
| 0 | 740 | node-4.example.com | no | probe-tap config | yes |

## Examples

| File | Rev | nodesExt | nodes | Forward map | thisNode | Alternate addresses | Same services |
| --- | --- | --- | --- | --- | --- | --- | --- |
| examples/cluster-3nodes-stable.json | 135 | 3 | 0 | no | yes | no | yes |
| examples/bucket-3nodes-stable.json | 211 | 3 | 3 | no | yes | no | yes |
| examples/bucket-5nodes-forward-map.json | 288 | 5 | 5 | yes | yes | no | no |
| examples/bucket-4nodes-no-this-node.json | 595 | 4 | 4 | no | no | no | yes |
| examples/cluster-4nodes-no-this-node.json | 595 | 4 | 0 | no | no | no | yes |
| examples/bucket-5nodes-mixed-services.json | 283 | 5 | 5 | no | yes | no | no |
| examples/bucket-5nodes-node-outside-bucket.json | 582 | 5 | 4 | no | yes | no | yes |
| examples/cluster-5nodes-mixed-services.json | 277 | 5 | 0 | no | yes | no | no |
| examples/bucket-5nodes-stable.json | 450 | 5 | 5 | no | yes | no | yes |
| examples/bucket-4nodes-stable.json | 596 | 4 | 4 | no | yes | no | yes |
