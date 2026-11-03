# Profile docker

The `reduce` command of the config recorder writes this file from profile.json. Do not edit it.

## Source

Server 8.0.4, captured 2026-11-02.

| Run | TLS | Length (s) |
| --- | --- | --- |
| 20261102T203406Z | yes | 1145 |
| 20261102T205334Z | no | 1197.4 |

| Kind | Distinct bodies | Versions with a body |
| --- | --- | --- |
| cluster | 96 | 43 |
| bucket | 8261 | 2770 |

## Fields that vary

perNode means the nodes of one body disagree, or some nodes do not have the field. perVersion means the field changes between bodies, or some bodies do not have it. All other fields are constant and the template holds them.

| Kind | Path | Scope | Distinct | Share | Samples |
| --- | --- | --- | --- | --- | --- |
| cluster | `nodesExt[].hostname` | perNode | 3 | 1 | "node-0.example.com", "node-1.example.com", "node-2.example.... |
| cluster | `nodesExt[].nodeUUID` | perNode | 4 | 1 | "a0000000000000000000000000000000", "a0000000000000000000000... |
| cluster | `nodesExt[].thisNode` | perNode | 1 | 0.979 | true |
| cluster | `rev` | perVersion | 42 | 1 | 1616, 1625, 1637, 1641, 1646 |
| bucket | `nodesExt[].hostname` | perNode | 3 | 1 | "node-0.example.com", "node-1.example.com", "node-2.example.... |
| bucket | `nodesExt[].nodeUUID` | perNode | 4 | 1 | "a0000000000000000000000000000000", "a0000000000000000000000... |
| bucket | `nodesExt[].thisNode` | perNode | 1 | 1 | true |
| bucket | `nodes[].couchApiBase` | perNode | 3 | 1 | "http://node-0.example.com:8092/default%2Bb00000000000000000... |
| bucket | `nodes[].hostname` | perNode | 3 | 1 | "node-0.example.com:8091", "node-1.example.com:8091", "node-... |
| bucket | `rev` | perVersion | 2769 | 1 | 1616, 1625, 1637, 1641, 1646 |
| bucket | `vBucketServerMap.serverList[]` | perNode | 3 | 1 | "node-0.example.com:11210", "node-1.example.com:11210", "nod... |
| bucket | `vBucketServerMap.vBucketMapForward[][]` | perVersion | 3 | 0.976 | 0, 1, 2 |
| bucket | `vBucketServerMap.vBucketMap[][]` | perVersion | 3 | 1 | 0, 1, 2 |

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
| 0 | no | no | 0, 1 |
| 0 | yes | yes | 2, 3 |

Server groups follow the pattern `Group {n}` with the values Group 1.
In join order the members are in Group 1, Group 1, Group 1, Group 1.

No node has alternate addresses.

## Transitions

Each step compares the first body of a version with the first body of the version before it, without thisNode.

### cluster

41 steps between 43 versions with a body. Any source delivered 43 versions.
Gap between versions with a body median 2038, p95 212214, max 669075 ms. Gap between all versions median 2038, p95 212214, max 669075 ms.

| Label | Steps |
| --- | --- |
| nodeAddedToNodesExt | 2 |
| nodeRemovedFromNodesExt | 2 |
| revOnly | 37 |

Bodies without thisNode.

| Run | Rev | Sent by | Sender in nodesExt | Source | First body of the version |
| --- | --- | --- | --- | --- | --- |
| 0 | 3147 | node-2.example.com | no | probe-tap config | no |
| 1 | 4707 | node-2.example.com | no | probe-tap config | yes |

### bucket

2768 steps between 2770 versions with a body. Any source delivered 2770 versions.
Gap between versions with a body median 308, p95 3156, max 56147 ms. Gap between all versions median 308, p95 3156, max 56147 ms.
Steps that move vbuckets move median 1, p95 1, max 3 vbuckets, 2728 in total.

| Label | Steps |
| --- | --- |
| nodeAddedToNodesExt | 2 |
| nodeRemovedFromNodesExt | 2 |
| nodeAddedToNodes | 2 |
| nodeRemovedFromNodes | 2 |
| serverListChanged | 4 |
| vbucketMapChanged | 2676 |
| forwardMapSet | 8 |
| forwardMapCleared | 8 |
| revOnly | 68 |

Rebalances. One rebalance is the versions with a forward map, the node additions before them and the node removals after them.

| Run | From rev | To rev | Versions with a body | Versions seen | With forward map | Seconds | vbuckets moved | Active moved |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 1646 | 2358 | 668 | 668 | 664 | 382.2 | 682 | 341 |
| 0 | 2377 | 2378 | 2 | 2 | 1 | 0.1 | 0 | 0 |
| 0 | 2401 | 3147 | 684 | 684 | 679 | 619.1 | 682 | 341 |
| 0 | 3169 | 3170 | 2 | 2 | 1 | 0.2 | 0 | 0 |
| 1 | 3200 | 3918 | 671 | 671 | 667 | 381.3 | 682 | 341 |
| 1 | 3937 | 3938 | 2 | 2 | 1 | 0.1 | 0 | 0 |
| 1 | 3959 | 4707 | 679 | 679 | 674 | 667.9 | 682 | 341 |
| 1 | 4729 | 4730 | 2 | 2 | 1 | 0.1 | 0 | 0 |

Every body has thisNode.

## Examples

| File | Rev | nodesExt | nodes | Forward map | thisNode | Alternate addresses | Same services |
| --- | --- | --- | --- | --- | --- | --- | --- |
| examples/cluster-2nodes-stable.json | 1616 | 2 | 0 | no | yes | no | yes |
| examples/bucket-2nodes-stable.json | 1616 | 2 | 2 | no | yes | no | yes |
| examples/bucket-3nodes-forward-map.json | 1656 | 3 | 3 | yes | yes | no | yes |
| examples/cluster-2nodes-no-this-node.json | 3147 | 2 | 0 | no | no | no | yes |
| examples/bucket-3nodes-node-outside-bucket.json | 1646 | 3 | 2 | no | yes | no | yes |
| examples/bucket-3nodes-stable.json | 1653 | 3 | 3 | no | yes | no | yes |
| examples/cluster-3nodes-stable.json | 1646 | 3 | 0 | no | yes | no | yes |
