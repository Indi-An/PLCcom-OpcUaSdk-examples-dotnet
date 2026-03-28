# 6 Reverse Connect – Server Examples

This folder contains examples for the OPC UA Reverse Connect mechanism.

---

## Examples

### 61_Reverse_Connect
Demonstrates Reverse Connect: instead of the client connecting to the server, the **server**
initiates the TCP connection to the client. This is useful when the server is behind a firewall
that blocks incoming connections (typical in OT/ICS networks).

The server calls `AddReverseConnection()` with the client's listening URL and periodically sends
a `ReverseHello` message. The client receives it and establishes a normal OPC UA session over
that connection. The server also keeps its standard endpoint available for direct connections.

Normal endpoint: `opc.tcp://localhost:48460`  
Reverse Connect target: `opc.tcp://localhost:48500` (server connects to client)

Use together with the matching `71_Reverse_Connect` client workshop.
