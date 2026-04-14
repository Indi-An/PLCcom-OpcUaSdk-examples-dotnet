# PLCcom.Opc.Ua.Sdk - Quick Start Examples

<img src="https://www.indi-an.com//wp-content/uploads/2026/03/PLCcom_720.png" width="200" alt="PLCcom Logo">

This repository provides quick-start examples for developers using the **PLCcom.Opc.Ua.Sdk** library. These examples demonstrate how easy it is to integrate PLCcom into your .NET applications, enabling seamless communication with OPC UA servers — and how to build your own OPC UA servers.

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

## Overview of PLCcom.Opc.Ua.Sdk

PLCcom.Opc.Ua.Sdk is a highly optimized and modern SDK designed specifically for .NET software developers to provide convenient client and server access for OPC UA (Open Platform Communications Unified Architecture). The libraries are 100% .NET assemblies and can be directly linked as a NuGet package — no API calls necessary.

### Key Features
- **Path-based node addressing** — access nodes by browse path (e.g. `Objects.Plant.Line1.Temperature`)
- Easy to use, many functions can be called by a single line of code
- Automatic Connect, Reconnect, and Disconnect functionality
- Active keep-alive monitoring of the server state
- OPC UA Client **and** Server SDK in a single assembly
- Support for **opc.tcp** and **opc.https** transport protocols
- Support for the most common OPC UA specifications:
  - Data Access (most used)
  - Alarm and Conditions
  - Historical Data
  - Historical Events
  - Complex / Structured Data Types
- Extensive tutorials for C# and Visual Basic included

For a full list of supported features and detailed descriptions, refer to the official documentation [here](https://www.indi-an.com/help_opc_ua_client_sdk/net/help/html/R_Project_PLCcom_Opc_Ua_Sdk_Documentation.htm).

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
| 41 | Historical Data | Read, insert, update and delete historical values |
| 42 | Read Historical Events | Read past events from the server history |
| 43 | Monitoring Historical Events | Subscribe to historical event notifications |
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
| 12 | User Authentication | Username/password and certificate authentication |
| 13 | Methods | Expose callable methods in the address space |
| 14 | Variables and Arrays | Various data types and array variables |
| 15 | Custom Types | Define and expose custom structured types |
| 16 | Multiple Namespaces | Organize nodes across multiple namespaces |
| 17 | Dynamic Nodes | Create and remove nodes at runtime |
| 19 | Advanced Server | Advanced server configuration and features |
| **2 Alarms and Events** | | |
| 21 | Simple Events | Fire events from the server |
| 22 | Alarm Conditions | Implement alarm conditions with state management |
| **3 Historical Data** | | |
| 31 | Historical Access | Store and serve historical data values |
| 32 | Historical Update | Insert, update, replace and delete history |
| 33 | Historical Events | Record and serve historical events |
| **4 NodeSet Import** | | |
| 41 | NodeSet Import | Import OPC UA NodeSet2 XML files |
| **5 Logging** | | |
| 51 | Logging | Configure server-side logging and diagnostics |
| **6 Reverse Connect** | | |
| 61 | Reverse Connect | Server-initiated connections through firewalls |

## Requirements

- .NET 10.0
- Visual Studio 2022 or newer (recommended VS2026)

## Important Licensing Information

**Examples License:**
- All examples provided in this repository are released under the **MIT License**. You are free to use, modify, and distribute these examples according to the terms of the MIT license.

**PLCcom Library License:**
- **PLCcom.Opc.Ua.Sdk** itself is proprietary software and is **NOT** included under the MIT license. To use the PLCcom library in your own projects, you must acquire an appropriate license and accept the EULA for the PLCcom.Opc.Ua.Sdk library. More information about purchasing a license can be found [here](https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/).

**Test License:**
- For execution, a (test) license is required. Users can request a trial license themselves via the PLCcom.Opc.Ua.Sdk [download website](https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/).

## Getting Started

1. Clone this repository.
2. Install the PLCcom.Opc.Ua.Sdk NuGet package into your project from the NuGet Package Manager:

```bash
Install-Package PLCcom.Opc.Ua.Sdk
```

## ⚠️ Important Safety Notice

The examples in this repository are **for demonstration purposes only** and **must _not_** be used in production, safety‑critical, or industrial environments without your own checks.
**Use at your own risk!** Deploying these examples in real systems may lead to personal injury, property damage, or environmental harm and is **strictly prohibited**.

The author disclaims all liability—direct, indirect, incidental, or consequential—arising from the use or misuse of these examples.

##### Trademark Information: #####
All product names, company names, and trademarks referenced in this repository are trademarks or registered trademarks of their respective owners. There is no affiliation between the mentioned trademarks or their owners and Indi.An GmbH. Any mention of trademarks is solely for reference purposes regarding usage and application.
