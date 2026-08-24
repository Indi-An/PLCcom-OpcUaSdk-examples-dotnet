# PLCcom.Opc.Ua.Sdk - Quick Start Examples

<img src="https://www.indi-an.com//wp-content/uploads/2026/03/PLCcom_720.png" width="200" alt="PLCcom Logo">

This repository provides quick-start examples for developers using the **PLCcom.Opc.Ua.Sdk** library and the **PLCcom.Opc.Ua.PubSub** add-on. These examples demonstrate how easy it is to integrate PLCcom into your .NET applications, enabling seamless communication with OPC UA servers — and how to build your own OPC UA servers and PubSub publishers/subscribers.

## Easy to Use — Address Nodes by Path or NodeId

PLCcom supports two ways to address OPC UA nodes: the classic approach using NodeIds (`ns=2;i=12345`) and — unique to PLCcom — by browse path (`Objects.Plant.Line1.Machine1.Temperature`), just like navigating a folder structure. The SDK resolves the path to the corresponding NodeId in the background:

```csharp
// Resolve a node by path — no cryptic NodeId needed!
NodeId nodeId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.Temperature");

// Read a value
DataValue value = client.ReadValue(nodeId);

// Write a value
client.WriteValue(nodeId, 23.5);
```

This makes your code **readable, maintainable, and independent of server-specific NodeId assignments**. Of course, classic NodeId-based access is fully supported too.

---

## Licensing Information

**Examples License:**
All examples in this repository are released under the **MIT License**. You are free to use, modify, and distribute them according to the MIT license terms.

**PLCcom Library License:**
The **PLCcom OPC UA SDK** itself is proprietary software and is **NOT** included under the MIT license. To use the library in your own projects you must acquire an appropriate license and accept the EULA. More information: [https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/](https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/)

**Evaluation mode:**
The examples pass two empty strings as username and serial. With blank fields the library runs for
15 minutes with full functionality during a debug session – enough to establish a connection and
read values, so you can try the examples out before registering.

When the evaluation period ends, operation stops: an open connection is disconnected and a running
server halts. The same applies when a time-limited license expires. The library never terminates
your application.

**Trial License:**
For uninterrupted work, generate a free trial license (14 days) at the
[PLCcom download page](https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/). License data
does not belong in source code – use configuration files, environment variables or a secret
manager to load it at runtime.

---

## Overview of PLCcom.Opc.Ua.Sdk

PLCcom.Opc.Ua.Sdk is a highly optimized and modern SDK designed specifically for .NET software developers to provide convenient client and server access for OPC UA (Open Platform Communications Unified Architecture). The libraries are 100% .NET assemblies and can be directly linked as a NuGet package — no API calls necessary.

### Key Features
- **Path-based node addressing** — access nodes by browse path (e.g. `Objects.Plant.Line1.Temperature`)
- Easy to use, many functions can be called by a single line of code
- Automatic Connect, Reconnect, and Disconnect functionality
- Active keep-alive monitoring of the server state
- Full **Client SDK** and **Server SDK** in a single assembly
- **OPC UA PubSub** add-on — brokerless UDP/UADP and broker-based MQTT/UADP and MQTT/JSON transport (separate NuGet package)
- Support for **opc.tcp** and **opc.https** transport protocols
- Support for the most common OPC UA specifications:
  - Data Access (most used)
  - Alarm and Conditions
  - Historical Data and Historical Events
  - Complex / Structured Data Types
  - Simple Events
  - Reverse Connect
- Extensive tutorials for C# and Visual Basic included

For a full list of supported features and detailed descriptions, refer to the official documentation [here](https://www.indi-an.com/help_opc_ua_client_sdk/net/help/html/R_Project_PLCcom_Opc_Ua_Sdk_Documentation.htm).

---

## Workshop Overview

### Client Workshops (C# and Visual Basic)

| # | Workshop | Description |
|---|----------|-------------|
| **1 First Steps** | | |
| 11 | Discover Server | Discover available OPC UA servers on the network |
| 12 | Connect Endpoint | Connect to a server endpoint |
| 13 | Connect with User Auth | Connect with username/password authentication |
| 14 | Connect with Cert Auth | Connect with certificate-based authentication |
| 15 | Browse by NodeId | Browse the server address space by NodeId |
| 16 | Browse by Path | Browse the server address space by browse path |
| 19 | Enable Debug Tracing | Enable diagnostic tracing for troubleshooting |
| **2 Data Access** | | |
| 21 | Read/Write by NodeId | Read and write values using NodeIds |
| 22 | Read/Write by Path | Read and write values using browse paths |
| 23 | Monitoring Items | Subscribe to value changes with monitored items |
| 24 | Simple Method Calls | Call OPC UA methods with structured input |
| 25 | Advanced Calls with Structs | Call methods with nested structures and arrays |
| 26 | Read Attributes | Read node attributes (DataType, Description, etc.) |
| 27 | Registered Read/Write | High-performance read/write with registered nodes |
| **3 Alarm Conditions** | | |
| 31 | Incoming Alarms | Subscribe to and display incoming alarm events |
| 32 | Alarm List | Maintain a live list of all active alarms |
| 33 | Alarm Conditions | Acknowledge, confirm and comment on alarms |
| **4 Historical Data** | | |
| 41 | Historical Data | Read historical values from the server |
| 42 | Historical Data Update | Insert, update, replace and delete historical values |
| 43 | Read Historical Events | Read past events from the server history |
| 44 | Monitoring Historical Events | Subscribe to historical event notifications |
| **5 Complex Datatypes** | | |
| 51 | Complex Types | Read and decode structured/complex data types |
| **6 Simple Events** | | |
| 61 | Simple Events | Subscribe to and display event notifications |
| **7 Reverse Connect** | | |
| 71 | Reverse Connect | Server-initiated connections through firewalls |

### Server Workshops (C# and Visual Basic)

| # | Workshop | Description |
|---|----------|-------------|
| **1 Data Access** | | |
| 11 | Simple Server | Basic OPC UA server with variables |
| 12a | User Authentication | Username/password and certificate authentication with roles |
| 12b | Custom Auth Validator | Custom credential and permission validators (IUaCredentialValidator / IUaPermissionValidator) |
| 13 | Methods | Expose callable methods in the address space |
| 14 | Variables and Arrays | Various data types, properties, callbacks and array variables |
| 15 | Custom Types | Define and expose custom structured types (Structs) |
| 16 | Multiple Namespaces | Organize nodes across multiple namespaces |
| 17 | Dynamic Nodes | Create and remove nodes at runtime |
| 19 | Advanced Server | Production-grade server combining all Data Access features |
| **2 Alarms and Events** | | |
| 21 | Alarm Conditions | Implement alarm conditions with state management |
| **3 Historical Data** | | |
| 31 | Historical Access | Store and serve historical data values |
| 32 | Historical Update | Insert, update, replace and delete history |
| 33 | Historical Events | Record and serve historical events |
| 34 | Custom History Store | Implement IHistoryStore for custom storage back-ends |
| 35 | Custom Event History Store | Implement IEventHistoryStore for custom event storage |
| **4 NodeSet Import** | | |
| 41 | NodeSet Import | Import OPC UA NodeSet2 XML files |
| **5 Logging** | | |
| 51 | Logging | Configure server-side logging and diagnostics |
| **6 Simple Events** | | |
| 61 | Simple Events | Fire events from the server |
| **7 Reverse Connect** | | |
| 71 | Reverse Connect | Server-initiated connections through firewalls |

### PubSub Workshops (C# and Visual Basic)

The PubSub workshops require the **PLCcom.Opc.Ua.PubSub** add-on package:
```bash
Install-Package PLCcom.Opc.Ua.PubSub
```

| # | Workshop | Description |
|---|----------|-------------|
| **1 Brokerless UADP** | | |
| 11 | UADP Unicast Publisher | Publish directly to a single subscriber via UDP unicast |
| 12 | UADP Unicast Subscriber | Receive unicast messages with dynamic field discovery |
| 13 | UADP Multicast Publisher | Publish to a multicast group — no broker required |
| 14 | UADP Multicast Subscriber | Join a multicast group and receive published data |
| 15 | UADP Broadcast Publisher | Publish UADP messages to the local broadcast address |
| 16 | UADP Broadcast Subscriber | Receive broadcast UADP messages without discovery |
| **2 Broker UADP** | | |
| 21 | MQTT UADP Publisher | Publish via MQTT broker with compact UADP binary encoding |
| 22 | MQTT UADP Subscriber | Receive UADP messages from an MQTT broker |
| 23 | sMQTT UADP Publisher | Publish via MQTT broker with TLS encryption |
| 24 | sMQTT UADP Subscriber | Receive UADP messages over a TLS-secured MQTT connection |
| **3 Broker JSON** | | |
| 31 | MQTT JSON Publisher | Publish via MQTT broker with human-readable JSON encoding |
| 32 | MQTT JSON Subscriber | Receive JSON messages from an MQTT broker |
| 33 | sMQTT JSON Publisher | Publish JSON via MQTT broker with TLS encryption |
| 34 | sMQTT JSON Subscriber | Receive JSON messages over a TLS-secured MQTT connection |

### Client ↔ Server Pairing

Many client workshops are designed to work with a specific server workshop. Start the server first, then run the matching client:

| Client | Server | Topic |
|--------|--------|-------|
| 11 Discover Server | *any server* | Endpoint discovery |
| 12 Connect Endpoint | 11 Simple Server | Basic connection |
| 13 Connect with User Auth | 12a User Authentication | Username/password login |
| 14 Connect with Cert Auth | 12a User Authentication | Certificate login |
| 15 Browse by NodeId | 11 Simple Server | Browse address space |
| 16 Browse by Path | 11 Simple Server | Browse by path |
| 21–22 Read/Write | 11 Simple Server | Data Access |
| 23 Monitoring Items | 11 Simple Server | Subscriptions |
| 24 Simple Method Calls | 13 Methods | Method calls |
| 25 Advanced Calls with Structs | 13 Methods | Nested struct arguments |
| 26 Read Attributes | 14 Variables and Arrays | Node attributes |
| 27 Registered Read/Write | 11 Simple Server | Registered nodes |
| 31–33 Alarm Conditions | 21 Alarm Conditions | Alarms |
| 41 Historical Data | 31 Historical Access | Read history |
| 42 Historical Data Update | 32 Historical Update | Write history |
| 43 Read Historical Events | 33 Historical Events | Event history |
| 44 Monitoring Historical Events | 33 Historical Events | Event history subscription |
| 51 Complex Types | 15 Custom Types | Structured data types |
| 61 Simple Events | 61 Simple Events | Events |
| 71 Reverse Connect | 71 Reverse Connect | Firewall traversal |

### Publisher ↔ Subscriber Pairing

Each publisher workshop has a matching subscriber. Run the publisher first, then the subscriber:

| Publisher | Subscriber | Transport |
|-----------|------------|-----------|
| 11 UADP Unicast Publisher | 12 UADP Unicast Subscriber | UDP unicast (brokerless) |
| 13 UADP Multicast Publisher | 14 UADP Multicast Subscriber | UDP multicast (brokerless) |
| 15 UADP Broadcast Publisher | 16 UADP Broadcast Subscriber | UDP broadcast (brokerless) |
| 21 MQTT UADP Publisher | 22 MQTT UADP Subscriber | MQTT broker, UADP encoding |
| 23 sMQTT UADP Publisher | 24 sMQTT UADP Subscriber | MQTT broker, UADP encoding, TLS |
| 31 MQTT JSON Publisher | 32 MQTT JSON Subscriber | MQTT broker, JSON encoding |
| 33 sMQTT JSON Publisher | 34 sMQTT JSON Subscriber | MQTT broker, JSON encoding, TLS |

---

## Requirements

- .NET 10.0
- Visual Studio 2022 or newer (recommended VS2026)
- [PLCcom.Opc.Ua.Sdk](https://www.nuget.org/packages/PLCcom.Opc.Ua.Sdk) 10.7.* or newer
- [PLCcom.Opc.Ua.PubSub](https://www.nuget.org/packages/PLCcom.Opc.Ua.PubSub) 10.7.* or newer (PubSub workshops only)

---

## Getting Started

1. Clone this repository.
2. Install the PLCcom.Opc.Ua.Sdk NuGet package:

```bash
Install-Package PLCcom.Opc.Ua.Sdk
```

3. For PubSub workshops, also install the add-on package:

```bash
Install-Package PLCcom.Opc.Ua.PubSub
```

---

## ⚠️ Important Safety Notice

The examples in this repository are **for demonstration purposes only** and **must _not_** be used in production, safety‑critical, or industrial environments without your own checks.
**Use at your own risk!** Deploying these examples in real systems may lead to personal injury, property damage, or environmental harm and is **strictly prohibited**.

The author disclaims all liability—direct, indirect, incidental, or consequential—arising from the use or misuse of these examples.

---

##### Trademark Information: #####
All product names, company names, and trademarks referenced in this repository are trademarks or registered trademarks of their respective owners. There is no affiliation between the mentioned trademarks or their owners and Indi.An GmbH. Any mention of trademarks is solely for reference purposes regarding usage and application.
