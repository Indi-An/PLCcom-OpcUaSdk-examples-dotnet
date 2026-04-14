# 2 Data Access — Client (Visual Basic)

These workshops demonstrate reading, writing and monitoring OPC UA variables.

| # | Workshop | What you will learn |
|---|----------|-------------------|
| 21 | Read/Write by NodeId | Read and write values using NodeIds |
| 22 | Read/Write by Path | Read and write values using browse paths — the PLCcom way |
| 23 | Monitoring Items | Subscribe to value changes with monitored items |
| 24 | Simple Method Calls | Call methods with structured input arguments |
| 25 | Advanced Calls with Structs | Call methods with nested structures and arrays |
| 26 | Read Attributes | Read node attributes (DataType, AccessLevel, etc.) |
| 27 | Registered Read/Write | High-performance access with registered nodes |

**Target server:** `opc.tcp://localhost:48410` (Server Workshop 11)

> **Tip:** Workshops 21-23 and 26-27 use browse paths like `Objects.Plant.Line1.Machine1.Temperature` to address nodes. This is more readable and maintainable than hardcoded NodeIds.
