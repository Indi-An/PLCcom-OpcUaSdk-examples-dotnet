# 7 Reverse Connect – Examples

This folder contains examples for connecting to an OPC UA server via Reverse Connect.

---

## Examples

### 71_Reverse_Connect
Demonstrates the client side of OPC UA Reverse Connect. Instead of the client dialing the server,
the **server** initiates the TCP connection. The client opens a listening port with
`StartReverseConnectListener()`, then calls `ConnectReverse()` to wait for the server's
`ReverseHello` message and establish the session. After that, the API is identical to a normal
connection — the example subscribes to `Plant.Temperature` and monitors its value.

Use together with the matching `61_Reverse_Connect` server workshop or the `ReverseConnect_Server`
test project.

Listen URL (client): `opc.tcp://localhost:48500`  
Server endpoint:     `opc.tcp://localhost:48460`
