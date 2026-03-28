# 5 Logging – Server Examples

This folder contains examples for integrating SDK diagnostics into your own logging infrastructure.

---

## Examples

### 51_Logging
Subscribes to the `LogMessage` event to receive all internal SDK trace messages. Demonstrates the
four log levels (Error, Warning, Info, Debug) and how to set the verbosity with `SetLogLevel()`.
Shows how to color-code output by severity and how to forward messages to any logging framework
(NLog, Serilog, `Microsoft.Extensions.Logging`, etc.).

Subscribe before calling `Start()` to capture startup messages. Use `Info` level for development
and `Warning` or `Error` for production.

Endpoint: `opc.tcp://localhost:48450`
