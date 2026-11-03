# Profile capella-privatelink

The `reduce` command of the config recorder writes this file from profile.json. Do not edit it.

## Source

Server 8.0.3, captured 2026-11-03.

| Run | TLS | Length (s) |
| --- | --- | --- |
| 20261103T113517Z | yes | 1675.5 |

| Kind | Distinct bodies | Versions with a body |
| --- | --- | --- |
| cluster | 136 | 39 |
| bucket | 5157 | 1123 |

## Fields that vary

perNode means the nodes of one body disagree, or some nodes do not have the field. perVersion means the field changes between bodies, or some bodies do not have it. All other fields are constant and the template holds them.

| Kind | Path | Scope | Distinct | Share | Samples |
| --- | --- | --- | --- | --- | --- |
| cluster | `nodesExt[].alternateAddresses.external.ports.kvSSL` | perNode | 5 | 1 | 11208, 11209, 11210, 11211, 11212 |
| cluster | `nodesExt[].alternateAddresses.external.ports.n1qlSSL` | perNode | 1 | 1 | 18093 |
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
| cluster | `nodesExt[].thisNode` | perNode | 1 | 0.985 | true |
| cluster | `rev` | perVersion | 39 | 1 | 780, 794, 795, 796, 797 |
| bucket | `nodesExt[].alternateAddresses.external.ports.kvSSL` | perNode | 5 | 1 | 11208, 11209, 11210, 11211, 11212 |
| bucket | `nodesExt[].alternateAddresses.external.ports.n1qlSSL` | perNode | 1 | 1 | 18093 |
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
| bucket | `nodesExt[].thisNode` | perNode | 1 | 1 | true |
| bucket | `nodes[].couchApiBase` | perNode | 5 | 1 | "http://node-0.example.com:8092/recorder%2Bb0000000000000000... |
| bucket | `nodes[].hostname` | perNode | 5 | 1 | "node-0.example.com:8091", "node-1.example.com:8091", "node-... |
| bucket | `rev` | perVersion | 1123 | 1 | 780, 794, 795, 796, 797 |
| bucket | `vBucketServerMap.serverList[]` | perNode | 5 | 1 | "node-0.example.com:11210", "node-1.example.com:11210", "nod... |
| bucket | `vBucketServerMap.vBucketMapForward[][]` | perVersion | 5 | 0.961 | 0, 1, 2, 3, 4 |
| bucket | `vBucketServerMap.vBucketMap[][]` | perVersion | 5 | 1 | 0, 1, 2, 3, 4 |

## Fields the SDK does not model

The SDK config classes have no property for these paths, so the SDK drops them.

| Kind | Path | Scope | Samples |
| --- | --- | --- | --- |
| cluster | `prodName` | constant | "Couchbase Server" |
| bucket | `bucketType` | constant | "membase" |
| bucket | `collectionsManifestUid` | constant | "1" |
| bucket | `nodes[].alternateAddresses.external.hostname` | constant | "private-endpoint.example.com" |
| bucket | `numVBuckets` | constant | 1024 |
| bucket | `prodName` | constant | "Couchbase Server" |
| bucket | `storageBackend` | constant | "couchstore" |

## SDK fields the server never sends

No body of this profile has these paths. The SDK keeps their default values.

- `networkResolution`
- `nodesExt[].alternateAddresses.*.ports.capi`
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
- `nodesExt[].alternateAddresses.*.ports.mgmt`
- `nodesExt[].alternateAddresses.*.ports.moxi`
- `nodesExt[].alternateAddresses.*.ports.n1ql`
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
| 1 then 2 then 0 then 3 then 1 | yes | yes | 3 |
| 1 then 2 then 0 | yes | no | 4 |

Server groups follow the pattern `group:{n}` with the values group:1, group:2, group:3.
In join order the members are in group:2, group:3, group:1, group:1, group:2.

Alternate network `external` is on 5 members. All of them share the host name `private-endpoint.example.com`.

| Port key | Value |
| --- | --- |
| mgmtSSL | shared 18091 |
| kvSSL | per member in join order [11210, 11209, 11208, 11211, 11212], a range from 11208 |
| n1qlSSL | shared 18093 |
| capiSSL | shared 18092 |

The bucket nodes[] list also has alternate network `external`, host name shared `private-endpoint.example.com`, no ports.

## Transitions

Each step compares the first body of a version with the first body of the version before it, without thisNode.

### cluster

38 steps between 39 versions with a body. Any source delivered 39 versions.
Gap between versions with a body median 1374, p95 304815, max 599322 ms. Gap between all versions median 1374, p95 304815, max 599322 ms.

| Label | Steps |
| --- | --- |
| nodeAddedToNodesExt | 1 |
| nodeRemovedFromNodesExt | 2 |
| servicesChanged | 6 |
| alternateAddressesChanged | 3 |
| revOnly | 29 |

Bodies without thisNode.

| Run | Rev | Sent by | Sender in nodesExt | Source | First body of the version |
| --- | --- | --- | --- | --- | --- |
| 0 | 1612 | node-3.example.com | no | probe-tap config | yes |
| 0 | 2043 | node-0.example.com | no | probe-tap config | yes |

### bucket

1122 steps between 1123 versions with a body. Any source delivered 1123 versions.
Gap between versions with a body median 46, p95 224, max 599207 ms. Gap between all versions median 46, p95 224, max 599207 ms.
Steps that move vbuckets move median 1, p95 3, max 7 vbuckets, 1581 in total.

| Label | Steps |
| --- | --- |
| nodeAddedToNodesExt | 1 |
| nodeRemovedFromNodesExt | 2 |
| nodeAddedToNodes | 1 |
| nodeRemovedFromNodes | 2 |
| nodesExtOrderChanged | 2 |
| servicesChanged | 6 |
| alternateAddressesChanged | 3 |
| serverListChanged | 3 |
| vbucketMapChanged | 1063 |
| forwardMapSet | 3 |
| forwardMapCleared | 3 |
| revOnly | 41 |

Rebalances. One rebalance is the versions with a forward map, the node additions before them and the node removals after them.

| Run | From rev | To rev | Versions with a body | Versions seen | With forward map | Seconds | vbuckets moved | Active moved |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 845 | 1219 | 363 | 363 | 359 | 25.3 | 653 | 409 |
| 0 | 1270 | 1612 | 330 | 330 | 323 | 48.9 | 452 | 323 |
| 0 | 1634 | 2043 | 394 | 394 | 386 | 60.5 | 476 | 256 |

Bodies without thisNode.

| Run | Rev | Sent by | Sender in nodesExt | Source | First body of the version |
| --- | --- | --- | --- | --- | --- |
| 0 | 1612 | node-3.example.com | no | probe-tap config | yes |
| 0 | 2043 | node-0.example.com | no | probe-tap config | yes |

## Examples

| File | Rev | nodesExt | nodes | Forward map | thisNode | Alternate addresses | Same services |
| --- | --- | --- | --- | --- | --- | --- | --- |
| examples/cluster-3nodes-stable.json | 780 | 3 | 0 | no | yes | yes | yes |
| examples/bucket-3nodes-stable.json | 780 | 3 | 3 | no | yes | yes | yes |
| examples/bucket-5nodes-forward-map.json | 856 | 5 | 5 | yes | yes | yes | no |
| examples/bucket-4nodes-no-this-node.json | 1612 | 4 | 4 | no | no | yes | yes |
| examples/cluster-4nodes-no-this-node.json | 1612 | 4 | 0 | no | no | yes | yes |
| examples/bucket-5nodes-mixed-services.json | 851 | 5 | 5 | no | yes | yes | no |
| examples/bucket-5nodes-node-outside-bucket.json | 1597 | 5 | 4 | no | yes | yes | yes |
| examples/cluster-5nodes-mixed-services.json | 845 | 5 | 0 | no | yes | yes | no |
| examples/bucket-5nodes-stable.json | 1225 | 5 | 5 | no | yes | yes | yes |
| examples/bucket-4nodes-stable.json | 1612 | 4 | 4 | no | yes | yes | yes |
