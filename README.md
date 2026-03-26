# PLCcom.Opc.Ua.Sdk - Quick Start Examples

<img src="https://www.indi-an.com//wp-content/uploads/2026/03/PLCcom_720.png" width="200" alt="PLCcom Logo">

This repository provides quick-start examples for developers using the **PLCcom.Opc.Ua.Sdk** library. These examples demonstrate how easy it is to integrate PLCcom into your .NET applications, enabling seamless communication with OPC UA servers.

## Overview of PLCcom.Opc.Ua.Sdk

PLCcom.Opc.Ua.Sdk is a highly optimized and modern SDK designed specifically for .NET software developers to provide convenient client-side access to OPC UA (Open Platform Communications Unified Architecture) servers. The libraries are 100% .NET files and can be directly linked as a reference — no API calls necessary. The internal routines are optimized for high-performance access across platforms.

### Key Features
- Easy to use, many functions can be called by a single line of code
- Automatic Connect, Reconnect, and Disconnect functionality
- Active keep-alive monitoring of the server state
- Addressing of OPC nodes via browse name (e.g. `Objects.Data.Static.Scalar.Int64Value`)
- Support for the most common OPC UA specifications:
  - DataAccess (most used)
  - Alarm and Conditions
  - Historical Data
  - Historical Events
- Extensive tutorials for C# and Visual Basic included

For a full list of supported features and detailed descriptions, refer to the official documentation [here](https://www.indi-an.com/help_opc_ua_client_sdk/net/help/html/R_Project_PLCcom_Opc_Ua_Sdk_Documentation.htm).

## Requirements

- .NET Framework 4.7.2 or higher (up to 4.8.1)
- .NET 8.0
- .NET 9.0
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
