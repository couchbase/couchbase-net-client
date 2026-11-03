# Profile docker

The `reduce` command of the config recorder writes this file from profile.json. Do not edit it.

## Source

Server 8.6.0, captured 2026-11-03.

| Run | TLS | Length (s) |
| --- | --- | --- |
| 20261103T174755Z | no | 232.8 |
| 20261103T175148Z | yes | 243.5 |
| 20261103T175953Z | no | 167.3 |
| 20261103T180241Z | no | 125.4 |
| 20261103T180450Z | no | 236.6 |
| 20261103T180852Z | no | 167.8 |
| 20261103T181141Z | no | 219 |

| Kind | Distinct bodies | Versions with a body |
| --- | --- | --- |
| cluster | 210 | 104 |
| bucket | 13948 | 4870 |

## Fields that vary

perNode means the nodes of one body disagree, or some nodes do not have the field. perVersion means the field changes between bodies, or some bodies do not have it. All other fields are constant and the template holds them.

| Kind | Path | Scope | Distinct | Share | Samples |
| --- | --- | --- | --- | --- | --- |
| cluster | `nodesExt[].hostname` | perNode | 3 | 1 | "node-0.example.com", "node-1.example.com", "node-2.example.... |
| cluster | `nodesExt[].nodeUUID` | perNode | 6 | 1 | "a0000000000000000000000000000000", "a0000000000000000000000... |
| cluster | `nodesExt[].thisNode` | perNode | 1 | 0.957 | true |
| cluster | `rev` | perVersion | 98 | 1 | 75, 77, 93, 106, 109 |
| cluster | `revEpoch` | perVersion | 2 | 1 | 1, 2 |
| bucket | `nodesExt[].hostname` | perNode | 3 | 1 | "node-0.example.com", "node-1.example.com", "node-2.example.... |
| bucket | `nodesExt[].nodeUUID` | perNode | 6 | 1 | "a0000000000000000000000000000000", "a0000000000000000000000... |
| bucket | `nodesExt[].thisNode` | perNode | 1 | 1 | true |
| bucket | `nodes[].couchApiBase` | perNode | 3 | 1 | "http://node-0.example.com:8092/default%2Bb00000000000000000... |
| bucket | `nodes[].hostname` | perNode | 3 | 1 | "node-0.example.com:8091", "node-1.example.com:8091", "node-... |
| bucket | `rev` | perVersion | 4864 | 1 | 83, 93, 107, 109, 114 |
| bucket | `revEpoch` | perVersion | 2 | 1 | 1, 2 |
| bucket | `vBucketServerMap.serverList[]` | perNode | 3 | 1 | "node-0.example.com:11210", "node-1.example.com:11210", "nod... |
| bucket | `vBucketServerMap.vBucketMapForward[][]` | perVersion | 4 | 0.969 | -1, 0, 1, 2 |
| bucket | `vBucketServerMap.vBucketMap[][]` | perVersion | 4 | 1 | -1, 0, 1, 2 |

## Fields the SDK does not model

The SDK config classes have no property for these paths, so the SDK drops them.

| Kind | Path | Scope | Samples |
| --- | --- | --- | --- |
| cluster | `prodName` | constant | "Couchbase Server" |
| bucket | `bucketType` | constant | "membase" |
| bucket | `collectionsManifestUid` | constant | "1" |
| bucket | `externalCollectionsManifestUid` | constant | "0" |
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
- `nodesExt[].services.indexAdmin`
- `nodesExt[].services.indexHttp`
- `nodesExt[].services.indexHttps`
- `nodesExt[].services.indexScan`
- `nodesExt[].services.indexStreamCatchup`
- `nodesExt[].services.indexStreamInit`
- `nodesExt[].services.indexStreamMaint`
- `nodesExt[].services.moxi`
- `nodesExt[].services.n1ql`
- `nodesExt[].services.n1qlSSL`
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
| kv | 11210 |
| kvSSL | 11207 |
| mgmt | 8091 |
| mgmtSSL | 18091 |
| projector | 9999 |

| Service set | Services | Keys |
| --- | --- | --- |
| 0 | capi, kv, mgmt, projector | 7 |

Service sets over the life of each member. A member is one node with one nodeUUID.

| Service sets in order | Joined during a run | Left during a run | Members |
| --- | --- | --- | --- |
| 0 | no | no | 0 |
| 0 | no | yes | 1, 4 |
| 0 | yes | yes | 2, 3, 5 |

Server groups follow the pattern `Group {n}` with the values Group 1.
In join order the members are in Group 1, Group 1, Group 1, Group 1, Group 1, Group 1.

No node has alternate addresses.

## Transitions

Each step compares the first body of a version with the first body of the version before it, without thisNode.

### cluster

97 steps between 104 versions with a body. Any source delivered 104 versions.
Gap between versions with a body median 538, p95 63163, max 91790 ms. Gap between all versions median 538, p95 63163, max 91790 ms.

| Label | Steps |
| --- | --- |
| nodeAddedToNodesExt | 6 |
| nodeRemovedFromNodesExt | 7 |
| revEpochChanged | 1 |
| revOnly | 83 |

Bodies without thisNode.

| Run | Rev | Sent by | Sender in nodesExt | Source | First body of the version |
| --- | --- | --- | --- | --- | --- |
| 0 | 1296 | node-2.example.com | no | probe-tap config | no |
| 1 | 2541 | node-2.example.com | no | probe-tap config | yes |
| 2 | 3514 | node-1.example.com | no | probe-tap config | no |
| 2 | 3518 | node-1.example.com | no | probe-tap config | yes |
| 2 | 3519 | node-1.example.com | no | probe-tap config | yes |
| 3 | 3855 | node-1.example.com | no | probe-tap config | no |
| 3 | 3859 | node-1.example.com | no | probe-tap config | no |
| 3 | 3860 | node-1.example.com | no | probe-tap config | no |
| 4 | 4385 | node-1.example.com | no | probe-tap config | no |

### bucket

4863 steps between 4870 versions with a body. Any source delivered 4870 versions.
Gap between versions with a body median 67, p95 288, max 91794 ms. Gap between all versions median 67, p95 288, max 91794 ms.
Steps that move vbuckets move median 1, p95 2, max 1024 vbuckets, 11259 in total.

| Label | Steps |
| --- | --- |
| nodeAddedToNodesExt | 6 |
| nodeRemovedFromNodesExt | 7 |
| nodeAddedToNodes | 6 |
| nodeRemovedFromNodes | 7 |
| nodesExtOrderChanged | 6 |
| serverListChanged | 13 |
| vbucketMapChanged | 4672 |
| forwardMapSet | 15 |
| forwardMapCleared | 15 |
| revEpochChanged | 1 |
| revOnly | 143 |

Rebalances. One rebalance is the versions with a forward map, the node additions before them and the node removals after them.

| Run | From rev | To rev | Versions with a body | Versions seen | With forward map | Seconds | vbuckets moved | Active moved |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 114 | 681 | 557 | 557 | 553 | 53.4 | 682 | 341 |
| 0 | 700 | 701 | 2 | 2 | 1 | 0 | 0 | 0 |
| 0 | 722 | 1296 | 564 | 564 | 559 | 73.3 | 682 | 341 |
| 0 | 1316 | 1317 | 2 | 2 | 1 | 0 | 0 | 0 |
| 1 | 1351 | 1939 | 576 | 576 | 572 | 63 | 682 | 341 |
| 1 | 1958 | 1959 | 2 | 2 | 1 | 0.1 | 0 | 0 |
| 1 | 1980 | 2541 | 552 | 552 | 547 | 65.7 | 682 | 341 |
| 1 | 2563 | 2564 | 2 | 2 | 1 | 0 | 0 | 0 |
| 2 | 3205 | 3514 | 304 | 304 | 299 | 41.4 | 1025 | 342 |
| 2 | 3530 | 3839 | 297 | 297 | 292 | 36.4 | 1025 | 342 |
| 3 | 3873 | 4369 | 487 | 487 | 483 | 37.6 | 683 | 341 |
| 4 | 4414 | 4908 | 492 | 492 | 491 | 29.7 | 682 | 0 |
| 4 | 4939 | 5520 | 569 | 569 | 565 | 68.9 | 682 | 341 |
| 5 | 5558 | 5876 | 307 | 307 | 302 | 39.5 | 1023 | 341 |
| 6 | 5933 | 5946 | 14 | 14 | 13 | 0.7 | 340 | 340 |

Bodies without thisNode.

| Run | Rev | Sent by | Sender in nodesExt | Source | First body of the version |
| --- | --- | --- | --- | --- | --- |
| 0 | 1296 | unknown | no | probe-tap config | no |
| 2 | 3519 | unknown | no | probe-tap config | no |

## Examples

| File | Rev | nodesExt | nodes | Forward map | thisNode | Alternate addresses | Same services |
| --- | --- | --- | --- | --- | --- | --- | --- |
| examples/cluster-2nodes-stable.json | 75 | 2 | 0 | no | yes | no | yes |
| examples/bucket-2nodes-stable.json | 83 | 2 | 2 | no | yes | no | yes |
| examples/bucket-3nodes-forward-map.json | 122 | 3 | 3 | yes | yes | no | yes |
| examples/bucket-2nodes-no-this-node.json | 1296 | 2 | 2 | no | no | no | yes |
| examples/cluster-2nodes-no-this-node.json | 1296 | 2 | 0 | no | no | no | yes |
| examples/bucket-3nodes-node-outside-bucket.json | 114 | 3 | 2 | no | yes | no | yes |
| examples/bucket-3nodes-stable.json | 119 | 3 | 3 | no | yes | no | yes |
| examples/bucket-1nodes-stable.json | 5901 | 1 | 1 | no | yes | no | yes |
| examples/cluster-3nodes-stable.json | 114 | 3 | 0 | no | yes | no | yes |
| examples/cluster-1nodes-stable.json | 5901 | 1 | 0 | no | yes | no | yes |
