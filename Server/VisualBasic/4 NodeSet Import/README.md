# 4 NodeSet Import – Server Examples

This folder contains examples for importing OPC UA NodeSet2 XML files into a server.

---

## Examples

### 41_NodeSet_Import
Imports a `NodeSet2.xml` file into the server's address space using `ImportNodeSet()`. The included
sample file (`PLCcom_Workshop_NodeSet.xml`) defines `MotorType` and `SensorType` with two instances
each. Namespaces declared in the NodeSet are registered automatically. After import, the types
appear under `Types -> ObjectTypes` and the instances are browsable under `Objects`.

This is the standard way to load OPC UA companion specifications (PackML, Euromap, DI, Machinery)
or vendor-specific type libraries into a server.

Endpoint: `opc.tcp://localhost:48440`
