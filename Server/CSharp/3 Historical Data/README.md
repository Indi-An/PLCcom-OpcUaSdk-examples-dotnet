# 3 Historical Data – Server Examples

This folder contains examples for providing historical data from an OPC UA server.

---

## Examples

### 31_Historical_Access
Enables history recording on variables (`Historizing = true`) and stores values in an in-memory
circular buffer (max 500 entries per variable). Records temperature and humidity every second with
explicit timestamps. Clients can retrieve the stored values using the OPC UA `HistoryRead` service
and display them in a history trend view.

Endpoint: `opc.tcp://localhost:48430`
