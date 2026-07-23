// MIT License
// Copyright (c) Indi.An GmbH
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// ==============================================================================
// PLCcom OPC UA Client SDK - Workshop 51: Complex Data Types
//
// OPC UA supports several levels of data complexity:
//
//   Level 1 - Scalar variable        e.g. double, string, bool
//   Level 2 - Array of scalars       e.g. double[], string[]
//   Level 3 - Flat struct            fields grouped in one ExtensionObject
//   Level 4 - Nested struct          struct containing another struct
//   Level 5 - Struct with arrays     struct fields that are arrays
//   Level 6 - Array of structs       array of ExtensionObjects
//
// Structs are transmitted as ExtensionObjects (binary-encoded blobs).
// The client must load the server's Type Dictionary first so the SDK
// can decode the binary payload into named fields.
//
// What you will learn:
//   * How to load the server Type Dictionary (GetComplexTypeSystem)
//   * How to read scalar and array variables
//   * How to read and decode struct variables (ExtensionObject)
//   * How to write individual struct fields via child node paths
//   * How to read arrays of structs
//   * How to dispose the client properly (internal reconnects keep the loaded type system)
//
// Required server: Server Workshop 15 (Custom Types)
// opc.tcp://localhost:48410
// ==============================================================================

using System;
using System.Linq;
using System.Text;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;
using PLCcom.Opc.Ua.Client.ComplexTypes;

class Program
{
    private UaClient client = null;

    static void Main(string[] args)
    {
        new Program().Start();
    }

    void Start()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 51: Complex Types       ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  OPC UA supports several levels of data complexity:          ║");
        Console.WriteLine("║    Level 1 - Scalar variable  (double, string, bool)         ║");
        Console.WriteLine("║    Level 2 - Array of scalars (double[], string[])           ║");
        Console.WriteLine("║    Level 3 - Flat struct      (fields in ExtensionObject)    ║");
        Console.WriteLine("║    Level 4 - Nested struct    (struct inside struct)         ║");
        Console.WriteLine("║    Level 5 - Struct with arrays (array fields in struct)     ║");
        Console.WriteLine("║    Level 6 - Array of structs (array of ExtensionObjects)    ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Required server: Server Workshop 15 (Custom Types)          ║");
        Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            var endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:48410"),
                certificateValidator: CertificateValidationHandler);
            endpoints = UaClient.SortEndpointsBySecurityLevel(endpoints);

            if (endpoints.Count == 0)
            {
                Console.WriteLine("  No endpoints found. Is Server Workshop 15 running?");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("endpoints found:");
            for (int i = 0; i < endpoints.Count; i++)
                Console.WriteLine($"  [{i}] {endpoints[i].ToDisplayString()}");

            Console.Write("  Please enter index of desired endpoint: ");
            if (!int.TryParse(Console.ReadLine(), out int idx) || idx < 0 || idx >= endpoints.Count)
            {
                Console.WriteLine("  Invalid selection.");
                Console.ReadLine();
                return;
            }
            Console.WriteLine();

            var sessionConfig = CreateConfig(endpoints[idx]);

            client = new UaClient(LicenseUserName, LicenseSerial, sessionConfig);
            Console.WriteLine("Info: license state => " + client.GetLicenceMessage());

            client.ServerConnectionLost += (s, e) => Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connection lost");
            client.ServerConnected += (s, e) =>
            {
                Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connected");

                // No type dictionary reload is needed here. On an internal auto-reconnect to
                // the same, unchanged server the SDK re-registers the already loaded complex
                // type system for the new session automatically, so decoded structs keep
                // working. Reload the type system yourself only if the server's type
                // configuration actually changed, via:
                //     client.ReleaseComplexTypeSystem();      // drop the cached type system
                //     client.GetComplexTypeSystem().Load();   // download and register it again
            };
            client.SessionClosing += (s, e) => Console.WriteLine(DateTime.Now.ToLocalTime() + " Session closed");
            client.CertificateValidation += CertificateValidationHandler;

            Console.Write("  Connecting ... ");
            client.Connect();
            Console.WriteLine("OK");
            Console.WriteLine();

            // -- Load the server Type Dictionary ----------------------------------
            // This is the key step for complex types: the SDK downloads the server's
            // binary type descriptions and registers them so ExtensionObjects can be
            // decoded into named fields (BaseComplexType with GetPropertyEnumerator).
            // Without this step, structs arrive as raw byte[] and cannot be decoded.
            Console.Write("  Loading server Type Dictionary ... ");
            var complexTypeSystem = client.GetComplexTypeSystem();
            complexTypeSystem.Load();
            Console.WriteLine("OK");

            var types = complexTypeSystem.GetDefinedTypes();
            Console.WriteLine($"  {types.Length} custom type(s) loaded: " +
                              string.Join(", ", types.Select(t => t.Name)));
            Console.WriteLine();

            // Command loop
            while (true)
            {
                Console.WriteLine("  Select operation:");
                Console.WriteLine("  1 - Read scalar variable          (Hierarchy.CNC_Machine_01.MainMotor.Speed)");
                Console.WriteLine("  2 - Read array of scalars         (StructData.Sensor_Struct.Readings)");
                Console.WriteLine("  3 - Read flat struct              (StructData.Motor_Struct)");
                Console.WriteLine("  4 - Write flat struct field       (StructData.Motor_Struct.Speed)");
                Console.WriteLine("  5 - Read nested struct            (StructData.Plant_Struct)");
                Console.WriteLine("  6 - Read struct with array fields (StructData.Sensor_Struct)");
                Console.WriteLine("  7 - Read array of structs         (StructData.Motor_Array)");
                Console.WriteLine("  8 - Write array of structs element(StructData.Motor_Array.[1].Speed)");
                Console.WriteLine("  9 - Exit");
                Console.Write("  > ");

                string input = Console.ReadLine();
                Console.WriteLine();
                if (string.IsNullOrEmpty(input) || input == "9") break;

                try
                {
                    switch (input)
                    {
                        case "1": ReadScalar(); break;
                        case "2": ReadArrayOfScalars(); break;
                        case "3": ReadFlatStruct(); break;
                        case "4": WriteFlatStructField(); break;
                        case "5": ReadNestedStruct(); break;
                        case "6": ReadStructWithArrays(); break;
                        case "7": ReadArrayOfStructs(); break;
                        case "8": WriteArrayOfStructs(); break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  Error: " + ex.Message);
                }

                Console.WriteLine();
            }

            client.Disconnect();
        }
        catch (Exception ex)
        {
            Console.WriteLine("  Error: " + ex.Message);
        }
        finally
        {
            Console.WriteLine("press enter for exit");
            Console.ReadLine();

            // Dispose the client instead of only disconnecting: Dispose disconnects
            // if still needed, stops the auto-reconnect loop and releases all client
            // resources. Always dispose the client, not just Disconnect().
            client?.Dispose();
        }
    }

    // ── Level 1: Scalar variable ──────────────────────────────────────────────

    void ReadScalar()
    {
        // A scalar variable is the simplest case - just ReadValue by browse path.
        // Server 15 Part A creates an Object hierarchy with individual Variables.
        string path = "Objects.Hierarchy.CNC_Machine_01.MainMotor.Speed";
        DataValue value = client.ReadValue(path);
        Console.WriteLine($"  Path:   {path}");
        Console.WriteLine($"  Value:  {value.Value}");
        Console.WriteLine($"  Status: {value.StatusCode}");
        Console.WriteLine($"  Time:   {value.SourceTimestamp.ToLocalTime():T}");
    }

    // ── Level 2: Array of scalars ─────────────────────────────────────────────

    void ReadArrayOfScalars()
    {
        // An array variable has ValueRank=OneDimension.
        // ReadValue returns the array directly - no ExtensionObject needed.
        // Server 15 Part D creates SensorDataType with a double[4] Readings field.
        // The child node "Readings" is a scalar array variable.
        string path = "Objects.StructData.Sensor_Struct.Readings";
        DataValue value = client.ReadValue(path);
        Console.WriteLine($"  Path:   {path}");
        if (value.Value is double[] arr)
        {
            Console.WriteLine($"  Type:   double[{arr.Length}]");
            for (int i = 0; i < arr.Length; i++)
                Console.WriteLine($"  [{i}] = {arr[i]}");
        }
        else
        {
            Console.WriteLine($"  Value:  {value.Value}");
        }
        Console.WriteLine($"  Status: {value.StatusCode}");
    }

    // ── Level 3: Flat struct ──────────────────────────────────────────────────

    void ReadFlatStruct()
    {
        // A struct variable holds an ExtensionObject.
        // After loading the Type Dictionary, the SDK decodes it into a
        // BaseComplexType with named fields accessible via GetPropertyEnumerator().
        // Server 15 Part B creates MotorDataType with Speed, Temperature, Running.
        string path = "Objects.StructData.Motor_Struct";
        DataValue value = client.ReadValue(path);
        Console.WriteLine($"  Path:   {path}");
        Console.WriteLine($"  Status: {value.StatusCode}");
        PrintExtensionObject(value);
    }

    // ── Level 3: Write flat struct field ─────────────────────────────────────

    void WriteFlatStructField()
    {
        // Individual struct fields are exposed as child Variable nodes.
        // Writing a child node updates that field and the parent struct value.
        // The path uses the struct variable path + "." + field name.
        Console.Write("  New Speed value: ");
        if (!double.TryParse(Console.ReadLine(), out double newSpeed))
        {
            Console.WriteLine("  Invalid value.");
            return;
        }

        string fieldPath = "Objects.StructData.Motor_Struct.Speed";
        StatusCode result = client.WriteValue(fieldPath, newSpeed);
        Console.WriteLine($"  Written {newSpeed} to {fieldPath}");
        Console.WriteLine($"  Result: {result}");

        // Read back to verify
        Console.WriteLine("  Reading back Motor_Struct:");
        ReadFlatStruct();
    }

    // ── Level 4: Nested struct ────────────────────────────────────────────────

    void ReadNestedStruct()
    {
        // A nested struct contains another struct as a field.
        // Server 15 Part C creates PlantDataType with Motor and Machine fields.
        // The SDK decodes the whole tree - nested structs appear as sub-properties.
        // Child nodes use dotted paths: Plant_Struct.Motor.Speed
        string path = "Objects.StructData.Plant_Struct";
        DataValue value = client.ReadValue(path);
        Console.WriteLine($"  Path:   {path}");
        Console.WriteLine($"  Status: {value.StatusCode}");
        Console.WriteLine();
        Console.WriteLine("  Top-level fields:");
        PrintExtensionObject(value);
        Console.WriteLine();

        // Read individual nested fields via child node paths
        Console.WriteLine("  Nested field access via child nodes:");
        string[] nestedPaths = {
            "Objects.StructData.Plant_Struct.PlantName",
            "Objects.StructData.Plant_Struct.Motor.Speed",
            "Objects.StructData.Plant_Struct.Motor.Temperature",
            "Objects.StructData.Plant_Struct.Machine.State",
            "Objects.StructData.Plant_Struct.Machine.CycleCount"
        };
        foreach (string p in nestedPaths)
        {
            DataValue v = client.ReadValue(p);
            Console.WriteLine($"  {p.Split('.')[^1],20} = {v.Value}");
        }
    }

    // ── Level 5: Struct with array fields ─────────────────────────────────────

    void ReadStructWithArrays()
    {
        // A struct can have array fields (e.g. double[4]).
        // Server 15 Part D creates SensorDataType with Readings[4] and Thresholds[2].
        string path = "Objects.StructData.Sensor_Struct";
        DataValue value = client.ReadValue(path);
        Console.WriteLine($"  Path:   {path}");
        Console.WriteLine($"  Status: {value.StatusCode}");
        PrintExtensionObject(value);
        Console.WriteLine();

        // Array fields are also accessible as child nodes
        Console.WriteLine("  Array fields via child nodes:");
        DataValue readings = client.ReadValue("Objects.StructData.Sensor_Struct.Readings");
        DataValue thresholds = client.ReadValue("Objects.StructData.Sensor_Struct.Thresholds");

        if (readings.Value is double[] r)
            Console.WriteLine($"  Readings   = [{string.Join(", ", r)}]");
        if (thresholds.Value is double[] t)
            Console.WriteLine($"  Thresholds = [{string.Join(", ", t)}]");
    }

    // ── Level 6: Array of structs ─────────────────────────────────────────────

    void ReadArrayOfStructs()
    {
        // An array of structs has child nodes for each element.
        // The element BrowseNames are "Motor_Array[0]", "Motor_Array[1]", etc.
        // We browse the parent node to find the element NodeIds.
        Console.WriteLine("  Reading Motor_Array elements via child nodes:");
        Console.WriteLine("  (Each element is a separate ExtensionObject)");
        Console.WriteLine();

        NodeId arrayNodeId = client.GetNodeIdByPath("Objects.StructData.Motor_Array");
        if (arrayNodeId == null)
        {
            Console.WriteLine("  Could not find Motor_Array.");
            return;
        }

        // Browse children to find element nodes (BrowseName = "Motor_Array[N]")
        var children = client.BrowseFull(arrayNodeId);
        int elemIndex = 0;
        foreach (var child in children)
        {
            if (child.BrowseName.Name.Contains("["))
            {
                NodeId elemNodeId = (NodeId)child.NodeId;
                DataValue value = client.ReadValue(elemNodeId);
                Console.WriteLine($"  Motor_Array[{elemIndex}] ({child.BrowseName.Name}):");
                PrintExtensionObject(value, "    ");

                // Also read individual fields via child nodes of the element
                var elemChildren = client.BrowseFull(elemNodeId);
                foreach (var field in elemChildren)
                {
                    DataValue fieldVal = client.ReadValue((NodeId)field.NodeId);
                    Console.WriteLine($"    {field.BrowseName.Name} = {fieldVal.Value}");
                }
                Console.WriteLine();
                elemIndex++;
            }
        }
    }

    // ── Level 6: Write array of structs element ───────────────────────────────

    void WriteArrayOfStructs()
    {
        Console.Write("  New Speed for Motor_Array[1]: ");
        if (!double.TryParse(Console.ReadLine(), out double newSpeed))
        {
            Console.WriteLine("  Invalid value.");
            return;
        }

        // Find Motor_Array[1] by browsing children
        NodeId arrayNodeId = client.GetNodeIdByPath("Objects.StructData.Motor_Array");
        if (arrayNodeId == null) { Console.WriteLine("  Could not find Motor_Array."); return; }

        var children = client.BrowseFull(arrayNodeId);
        var elemNodes = children.Where(c => c.BrowseName.Name.Contains("[")).ToList();

        if (elemNodes.Count < 2)
        {
            Console.WriteLine("  Motor_Array has fewer than 2 elements.");
            return;
        }

        // Find Speed child of element [1]
        NodeId elem1NodeId = (NodeId)elemNodes[1].NodeId;
        var fieldNodes = client.BrowseFull(elem1NodeId);
        var speedNode = fieldNodes.FirstOrDefault(f => f.BrowseName.Name == "Speed");

        if (speedNode == null) { Console.WriteLine("  Speed field not found."); return; }

        StatusCode result = client.WriteValue((NodeId)speedNode.NodeId, newSpeed);
        Console.WriteLine($"  Written {newSpeed} to Motor_Array[1].Speed");
        Console.WriteLine($"  Result: {result}");

        // Read back element [1]
        Console.WriteLine("  Reading back Motor_Array[1]:");
        DataValue value = client.ReadValue(elem1NodeId);
        PrintExtensionObject(value, "    ");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Prints the fields of an ExtensionObject decoded as BaseComplexType.
    /// After GetComplexTypeSystem().Load() the SDK can decode structs into
    /// named properties accessible via GetPropertyEnumerator().
    /// </summary>
    void PrintExtensionObject(DataValue value, string indent = "  ")
    {
        if (value?.Value == null)
        {
            Console.WriteLine($"{indent}(null)");
            return;
        }

        var ext = value.Value as ExtensionObject;
        if (ext == null && value.Value is ExtensionObject[] arr && arr.Length > 0)
            ext = arr[0];

        if (ext?.Body is BaseComplexType complexType)
        {
            foreach (var prop in complexType.GetPropertyEnumerator())
            {
                object fieldValue = complexType[prop.Name];
                if (fieldValue is BaseComplexType nested)
                {
                    Console.WriteLine($"{indent}{prop.Name}:");
                    foreach (var subProp in nested.GetPropertyEnumerator())
                        Console.WriteLine($"{indent}  {subProp.Name} = {nested[subProp.Name]}");
                }
                else if (fieldValue is Array fieldArr)
                {
                    var sb = new StringBuilder("[");
                    for (int i = 0; i < fieldArr.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(fieldArr.GetValue(i));
                    }
                    sb.Append("]");
                    Console.WriteLine($"{indent}{prop.Name} = {sb}");
                }
                else
                {
                    Console.WriteLine($"{indent}{prop.Name} = {fieldValue}");
                }
            }
        }
        else
        {
            // Type Dictionary not loaded or type not known - show raw value
            Console.WriteLine($"{indent}Raw value: {value.Value}");
            Console.WriteLine($"{indent}Tip: Ensure GetComplexTypeSystem().Load() was called.");
        }
    }

    void CertificateValidationHandler(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        if (ServiceResult.IsGood(e.Error)) e.Accept = true;
        else if (!e.ContainsUnsuppressibleStatusCodes) e.Accept = true;
        else if (e.ContainsUnsuppressibleStatusCodes) e.AcceptAll = true;
        else throw new Exception($"Certificate validation failed: {e.Error.Code}");
    }

    // =============================================================================
    // Helper: CreateConfig
    // =============================================================================
    // Builds the SessionConfiguration for the selected endpoint.
    //
    // Certificate handling:
    //   Application certificate -- required for Sign / SignAndEncrypt endpoints.
    //   HTTPS certificate       -- required for opc.https:// endpoints (any SecurityMode).
    //
    // UaClientCertificate derives file paths automatically from the PKI base directory:
    //   pki/own/certs/<alias>.der    <- certificate
    //   pki/own/private/<alias>.pem  <- private key
    //
    // Load() returns null if the certificate does not exist yet or cannot be read.
    // Build(true) creates a new self-signed certificate, overwriting any existing file.
    static SessionConfiguration CreateConfig(EndpointDescription endpoint)
    {
        string alias = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
        SessionConfiguration config = SessionConfiguration.Build(alias, endpoint);
        config.AutoConnect = false;

        // HTTPS certificate -- required for opc.https:// endpoints, independent of SecurityMode.
        UaClientCertificate httpsCert = null;
        if (endpoint.EndpointUrl != null &&
            endpoint.EndpointUrl.StartsWith("opc.https://", StringComparison.OrdinalIgnoreCase))
        {
            string host = new Uri(endpoint.EndpointUrl).Host;
            httpsCert = UaClientCertificate.Load("./pki", host, "secretpassword");
            if (httpsCert == null || !httpsCert.CheckValidity())
                httpsCert = new UaClientCertificate("./pki", "secretpassword", host, 720, "Indi.An GmbH")
                    .Build(overwrite: true);
        }

        // Application certificate -- required for secured endpoints (Sign or SignAndEncrypt).
        // Not needed for SecurityMode.None (unencrypted connections).
        UaClientCertificate appCert = null;
        if (!endpoint.SecurityMode.Equals(MessageSecurityMode.None))
        {
            appCert = UaClientCertificate.Load("./pki", alias, "secretpassword");
            if (appCert == null || !appCert.CheckValidity())
                appCert = new UaClientCertificate("./pki", "secretpassword", alias, 720, "Indi.An GmbH")
                    .Build(overwrite: true);
        }

        // SetInstanceCertificate() sets CertificateStorePath and ApplicationCertificateFullPath.
        if (appCert != null && httpsCert != null)
            config.SetInstanceCertificate(appCert, httpsCert);
        else if (appCert != null)
            config.SetInstanceCertificate(appCert);

        return config;
    }

    // =============================================================================
    // Helper: PrintConfig
    // =============================================================================
    // Prints the active client configuration to the console so you can verify
    // all settings at a glance before connecting.
    static void PrintConfig(SessionConfiguration config)
    {
        Console.WriteLine("-- Active Client Configuration ------------------------------");
        if (config.Endpoint != null)
        {
            Console.WriteLine($"  Endpoint  : {config.Endpoint.EndpointUrl}");
            Console.WriteLine($"  Security  : {config.Endpoint.ToDisplayString()}");
        }
        Console.WriteLine($"  PKI Store : {(config.CertificateStorePath != null ? config.CertificateStorePath : "(not set)")}");
        Console.WriteLine($"  Cert File : {(config.ApplicationCertificateFullPath != null ? config.ApplicationCertificateFullPath : "(none -- SecurityMode.None)")}");
        Console.WriteLine("-------------------------------------------------------------");
        Console.WriteLine();
    }
}