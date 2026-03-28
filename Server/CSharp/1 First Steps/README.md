# 1 First Steps – Server Examples

This folder contains introductory examples for building OPC UA servers with the PLCcom.Opc.Ua.Sdk.
Each subfolder is a standalone sample project.

---

## Recommended order

### 11_Simple_Server
The starting point for all server workshops. Creates a minimal OPC UA server with a folder hierarchy
(`Plant -> Line1 -> Machine1`), scalar and array variables of different data types, and a value push
loop that notifies subscribed clients every second.

Endpoint: `opc.tcp://localhost:48410`

### 12_Security_Endpoints
Configures transport security (security policies and modes) and user authentication
(Anonymous, Username/Password, X.509 certificate). Adds three users with different roles
(Engineer, Operator, Observer) and demonstrates session lifecycle events.

Endpoint: `opc.tcp://localhost:48411`

### 13_Methods
Creates callable OPC UA methods in the address space: a no-argument Reset method, arithmetic
methods with typed input/output arguments (Add, Multiply), and a SetTemperature method that
modifies a server-side variable and notifies subscribed clients.

Endpoint: `opc.tcp://localhost:48412`

### 14_Custom_Types
Defines custom ObjectTypes and VariableTypes in the server's type hierarchy. Creates typed
instances whose TypeDefinition attribute points to the custom type — the same pattern used by
OPC UA companion specifications (PackML, Euromap, DI, etc.).

Endpoint: `opc.tcp://localhost:48413`

### 15_Properties
Adds standard OPC UA properties to variables: EURange (min/max for HMI gauges), EngineeringUnits
(unit labels like °C, bar, rpm), and StatusCode (Good / Uncertain / Bad quality stamps).
Demonstrates write validation against EURange and atomic value+quality updates with `UpdateValue()`.

Endpoint: `opc.tcp://localhost:48414`

### 16_OnRead_OnWrite
Intercepts client reads and writes with callbacks. `OnRead` delivers a fresh value from any source
on every read (ideal for hardware registers). `OnWrite` validates incoming values and can accept or
reject them — returning `false` sends `BadOutOfRange` back to the client.

Endpoint: `opc.tcp://localhost:48415`

### 17_Multiple_Namespaces
Registers additional namespace URIs and creates nodes in specific namespaces. Shows how to look up
a namespace index by URI (the safe way to work with namespaces) and explains the fixed OPC UA
namespace table (ns=0 standard types, ns=1 server diagnostics, ns=2+ application namespaces).

Endpoint: `opc.tcp://localhost:48416`

### 18_Complex_Types
Models a hierarchical industrial object (CNC machine with motor and bearing) using nested OPC UA
Objects and a custom type hierarchy (MachineType, MotorType, BearingType). Each component has its
own variables and TypeDefinition attribute.

Endpoint: `opc.tcp://localhost:48417`

### 19_Dynamic_Nodes
Adds and removes nodes at runtime while the server is running — connected clients see the changes
immediately. Also demonstrates path-based node lookup (`Plant.Line1.Temperature`) and the SDK's
built-in circular reference detection.

Endpoint: `opc.tcp://localhost:48418`

---

## Common prerequisites

1. Enter your license credentials (`LicenseUserName` / `LicenseSerial`)
2. Visual Studio 2022 or higher (VS2026 recommended)
3. Connect with any OPC UA client (e.g. UA Expert, PLCcom OPC UA Client)
