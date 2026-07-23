' MIT License
' Copyright (c) Indi.An GmbH
'
' Permission is hereby granted, free of charge, to any person obtaining a copy
' of this software and associated documentation files (the "Software"), to deal
' in the Software without restriction, including without limitation the rights
' to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
' copies of the Software, and to permit persons to whom the Software is
' furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all
' copies or substantial portions of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
' IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
' FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
' AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
' LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
' OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
' SOFTWARE.

' ==============================================================================
' PLCcom OPC UA Client SDK - Workshop 51: Complex Data Types
'
' OPC UA supports several levels of data complexity:
'
'   Level 1 - Scalar variable        e.g. double, string, bool
'   Level 2 - Array of scalars       e.g. double[], string[]
'   Level 3 - Flat struct            fields grouped in one ExtensionObject
'   Level 4 - Nested struct          struct containing another struct
'   Level 5 - Struct with arrays     struct fields that are arrays
'   Level 6 - Array of structs       array of ExtensionObjects
'
' Structs are transmitted as ExtensionObjects (binary-encoded blobs).
' The client must load the server's Type Dictionary first so the SDK
' can decode the binary payload into named fields.
'
' What you will learn:
'   * How to load the server Type Dictionary (GetComplexTypeSystem)
'   * How to read scalar and array variables
'   * How to read and decode struct variables (ExtensionObject)
'   * How to write individual struct fields via child node paths
'   * How to read arrays of structs
'   * How to dispose the client properly (internal reconnects keep the loaded type system)
'
' Required server: Server Workshop 15 (Custom Types)
' opc.tcp://localhost:48410
' ==============================================================================

Imports System
Imports System.Linq
Imports System.Text
Imports System.Reflection
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk
Imports PLCcom.Opc.Ua.Client.ComplexTypes

Public Class Program

    Private client As UaClient = Nothing

    Public Shared Sub Main(ByVal args As String())
        Dim program As New Program()
        program.Start()
    End Sub

    Private Sub Start()
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 51: Complex Types       ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  OPC UA supports several levels of data complexity:          ║")
        Console.WriteLine("║    Level 1 - Scalar variable  (double, string, bool)         ║")
        Console.WriteLine("║    Level 2 - Array of scalars (double[], string[])           ║")
        Console.WriteLine("║    Level 3 - Flat struct      (fields in ExtensionObject)    ║")
        Console.WriteLine("║    Level 4 - Nested struct    (struct inside struct)         ║")
        Console.WriteLine("║    Level 5 - Struct with arrays (array fields in struct)     ║")
        Console.WriteLine("║    Level 6 - Array of structs (array of ExtensionObjects)    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Required server: Server Workshop 15 (Custom Types)          ║")
        Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            Dim endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(
                New Uri("opc.tcp://localhost:48410"),
                certificateValidator:=AddressOf CertificateValidationHandler)
            endpoints = UaClient.SortEndpointsBySecurityLevel(endpoints)

            If endpoints.Count = 0 Then
                Console.WriteLine("  No endpoints found. Is Server Workshop 15 running?")
                Console.ReadLine()
                Return
            End If

            Console.WriteLine("endpoints found:")
            For i As Integer = 0 To endpoints.Count - 1
                Console.WriteLine($"  [{i}] {endpoints(i).ToDisplayString()}")
            Next

            Console.Write("  Please enter index of desired endpoint: ")
            Dim idx As Integer
            If Not Integer.TryParse(Console.ReadLine(), idx) OrElse idx < 0 OrElse idx >= endpoints.Count Then
                Console.WriteLine("  Invalid selection.")
                Console.ReadLine()
                Return
            End If
            Console.WriteLine()

            Dim sessionConfig As SessionConfiguration = CreateConfig(endpoints(idx))


            PrintConfig(sessionConfig)

            Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " &
                              sessionConfig.CertificateStorePath)

            client = New UaClient(LicenseUserName, LicenseSerial, sessionConfig)
            Console.WriteLine("Info: license state => " & client.GetLicenceMessage())

            AddHandler client.ServerConnectionLost, Sub(s, e) Console.WriteLine(DateTime.Now.ToLocalTime() & " Session connection lost")
            AddHandler client.ServerConnected,
                Sub(s, e)
                    Console.WriteLine(DateTime.Now.ToLocalTime() & " Session connected")

                    ' No type dictionary reload is needed here. On an internal auto-reconnect to
                    ' the same, unchanged server the SDK re-registers the already loaded complex
                    ' type system for the new session automatically, so decoded structs keep
                    ' working. Reload the type system yourself only if the server's type
                    ' configuration actually changed, via:
                    '     client.ReleaseComplexTypeSystem()       ' drop the cached type system
                    '     client.GetComplexTypeSystem().Load()    ' download and register it again
                End Sub
            AddHandler client.SessionClosing, Sub(s, e) Console.WriteLine(DateTime.Now.ToLocalTime() & " Session closed")
            AddHandler client.CertificateValidation, AddressOf CertificateValidationHandler

            Console.Write("  Connecting ... ")
            client.Connect()
            Console.WriteLine("OK")
            Console.WriteLine()

            ' -- Load the server Type Dictionary ----------------------------------
            ' This is the key step for complex types: the SDK downloads the server's
            ' binary type descriptions and registers them so ExtensionObjects can be
            ' decoded into named fields (BaseComplexType with GetPropertyEnumerator).
            ' Without this step, structs arrive as raw byte[] and cannot be decoded.
            Console.Write("  Loading server Type Dictionary ... ")
            Dim complexTypeSystem = client.GetComplexTypeSystem()
            complexTypeSystem.Load()
            Console.WriteLine("OK")

            Dim types = complexTypeSystem.GetDefinedTypes()
            Console.WriteLine($"  {types.Length} custom type(s) loaded: " &
                              String.Join(", ", types.Select(Function(t) t.Name)))
            Console.WriteLine()

            ' Command loop
            While True
                Console.WriteLine("  Select operation:")
                Console.WriteLine("  1 - Read scalar variable          (Hierarchy.CNC_Machine_01.MainMotor.Speed)")
                Console.WriteLine("  2 - Read array of scalars         (StructData.Sensor_Struct.Readings)")
                Console.WriteLine("  3 - Read flat struct              (StructData.Motor_Struct)")
                Console.WriteLine("  4 - Write flat struct field       (StructData.Motor_Struct.Speed)")
                Console.WriteLine("  5 - Read nested struct            (StructData.Plant_Struct)")
                Console.WriteLine("  6 - Read struct with array fields (StructData.Sensor_Struct)")
                Console.WriteLine("  7 - Read array of structs         (StructData.Motor_Array)")
                Console.WriteLine("  8 - Write array of structs element(StructData.Motor_Array.[1].Speed)")
                Console.WriteLine("  9 - Exit")
                Console.Write("  > ")

                Dim input As String = Console.ReadLine()
                Console.WriteLine()
                If String.IsNullOrEmpty(input) OrElse input = "9" Then Exit While

                Try
                    Select Case input
                        Case "1" : ReadScalar()
                        Case "2" : ReadArrayOfScalars()
                        Case "3" : ReadFlatStruct()
                        Case "4" : WriteFlatStructField()
                        Case "5" : ReadNestedStruct()
                        Case "6" : ReadStructWithArrays()
                        Case "7" : ReadArrayOfStructs()
                        Case "8" : WriteArrayOfStructs()
                    End Select
                Catch ex As Exception
                    Console.WriteLine("  Error: " & ex.Message)
                End Try

                Console.WriteLine()
            End While

            client.Disconnect()

        Catch ex As Exception
            Console.WriteLine("  Error: " & ex.Message)
        Finally
            Console.WriteLine("press enter for exit")
            Console.ReadLine()

            ' Dispose the client instead of only disconnecting: Dispose disconnects
            ' if still needed, stops the auto-reconnect loop and releases all client
            ' resources. Always dispose the client, not just Disconnect().
            If client IsNot Nothing Then client.Dispose()
        End Try
    End Sub

    ' ── Level 1: Scalar variable ──────────────────────────────────────────────

    Private Sub ReadScalar()
        Dim path As String = "Objects.Hierarchy.CNC_Machine_01.MainMotor.Speed"
        Dim value As DataValue = client.ReadValue(path)
        Console.WriteLine($"  Path:   {path}")
        Console.WriteLine($"  Value:  {value.Value}")
        Console.WriteLine($"  Status: {value.StatusCode}")
        Console.WriteLine($"  Time:   {value.SourceTimestamp.ToLocalTime():T}")
    End Sub

    ' ── Level 2: Array of scalars ─────────────────────────────────────────────

    Private Sub ReadArrayOfScalars()
        Dim path As String = "Objects.StructData.Sensor_Struct.Readings"
        Dim value As DataValue = client.ReadValue(path)
        Console.WriteLine($"  Path:   {path}")
        Dim arr As Double() = TryCast(value.Value, Double())
        If arr IsNot Nothing Then
            Console.WriteLine($"  Type:   double[{arr.Length}]")
            For i As Integer = 0 To arr.Length - 1
                Console.WriteLine($"  [{i}] = {arr(i)}")
            Next
        Else
            Console.WriteLine($"  Value:  {value.Value}")
        End If
        Console.WriteLine($"  Status: {value.StatusCode}")
    End Sub

    ' ── Level 3: Flat struct ──────────────────────────────────────────────────

    Private Sub ReadFlatStruct()
        Dim path As String = "Objects.StructData.Motor_Struct"
        Dim value As DataValue = client.ReadValue(path)
        Console.WriteLine($"  Path:   {path}")
        Console.WriteLine($"  Status: {value.StatusCode}")
        PrintExtensionObject(value)
    End Sub

    ' ── Level 3: Write flat struct field ─────────────────────────────────────

    Private Sub WriteFlatStructField()
        Console.Write("  New Speed value: ")
        Dim newSpeed As Double
        If Not Double.TryParse(Console.ReadLine(), newSpeed) Then
            Console.WriteLine("  Invalid value.")
            Return
        End If

        Dim fieldPath As String = "Objects.StructData.Motor_Struct.Speed"
        Dim result As StatusCode = client.WriteValue(fieldPath, newSpeed)
        Console.WriteLine($"  Written {newSpeed} to {fieldPath}")
        Console.WriteLine($"  Result: {result}")
        Console.WriteLine("  Reading back Motor_Struct:")
        ReadFlatStruct()
    End Sub

    ' ── Level 4: Nested struct ────────────────────────────────────────────────

    Private Sub ReadNestedStruct()
        Dim path As String = "Objects.StructData.Plant_Struct"
        Dim value As DataValue = client.ReadValue(path)
        Console.WriteLine($"  Path:   {path}")
        Console.WriteLine($"  Status: {value.StatusCode}")
        Console.WriteLine()
        Console.WriteLine("  Top-level fields:")
        PrintExtensionObject(value)
        Console.WriteLine()

        Console.WriteLine("  Nested field access via child nodes:")
        Dim nestedPaths As String() = {
            "Objects.StructData.Plant_Struct.PlantName",
            "Objects.StructData.Plant_Struct.Motor.Speed",
            "Objects.StructData.Plant_Struct.Motor.Temperature",
            "Objects.StructData.Plant_Struct.Machine.State",
            "Objects.StructData.Plant_Struct.Machine.CycleCount"
        }
        For Each p As String In nestedPaths
            Dim v As DataValue = client.ReadValue(p)
            Dim fieldName As String = p.Split("."c).Last()
            Console.WriteLine($"  {fieldName,20} = {v.Value}")
        Next
    End Sub

    ' ── Level 5: Struct with array fields ─────────────────────────────────────

    Private Sub ReadStructWithArrays()
        Dim path As String = "Objects.StructData.Sensor_Struct"
        Dim value As DataValue = client.ReadValue(path)
        Console.WriteLine($"  Path:   {path}")
        Console.WriteLine($"  Status: {value.StatusCode}")
        PrintExtensionObject(value)
        Console.WriteLine()

        Console.WriteLine("  Array fields via child nodes:")
        Dim readings As DataValue = client.ReadValue("Objects.StructData.Sensor_Struct.Readings")
        Dim thresholds As DataValue = client.ReadValue("Objects.StructData.Sensor_Struct.Thresholds")

        Dim r As Double() = TryCast(readings.Value, Double())
        Dim t As Double() = TryCast(thresholds.Value, Double())
        If r IsNot Nothing Then Console.WriteLine($"  Readings   = [{String.Join(", ", r)}]")
        If t IsNot Nothing Then Console.WriteLine($"  Thresholds = [{String.Join(", ", t)}]")
    End Sub

    ' ── Level 6: Array of structs ─────────────────────────────────────────────

    Private Sub ReadArrayOfStructs()
        Console.WriteLine("  Reading Motor_Array elements via child node paths:")
        Console.WriteLine("  (Each element is a separate ExtensionObject)")
        Console.WriteLine()

        Dim arrayNodeId As NodeId = client.GetNodeIdByPath("Objects.StructData.Motor_Array")
        If arrayNodeId Is Nothing Then Console.WriteLine("  Could not find Motor_Array.") : Return

        Dim children = client.BrowseFull(arrayNodeId)
        Dim elemIndex As Integer = 0
        For Each child In children
            If child.BrowseName.Name.Contains("[") Then
                Dim elemNodeId As NodeId = CType(child.NodeId, NodeId)
                Dim value As DataValue = client.ReadValue(elemNodeId)
                Console.WriteLine($"  Motor_Array[{elemIndex}] ({child.BrowseName.Name}):")
                PrintExtensionObject(value, "    ")

                Dim elemChildren = client.BrowseFull(elemNodeId)
                For Each field In elemChildren
                    Dim fieldVal As DataValue = client.ReadValue(CType(field.NodeId, NodeId))
                    Console.WriteLine($"    {field.BrowseName.Name} = {fieldVal.Value}")
                Next
                Console.WriteLine()
                elemIndex += 1
            End If
        Next
    End Sub

    ' ── Level 6: Write array of structs element ───────────────────────────────

    Private Sub WriteArrayOfStructs()
        Console.Write("  New Speed for Motor_Array[1]: ")
        Dim newSpeed As Double
        If Not Double.TryParse(Console.ReadLine(), newSpeed) Then
            Console.WriteLine("  Invalid value.")
            Return
        End If

        Dim arrayNodeId2 As NodeId = client.GetNodeIdByPath("Objects.StructData.Motor_Array")
        If arrayNodeId2 Is Nothing Then Console.WriteLine("  Could not find Motor_Array.") : Return

        Dim children2 = client.BrowseFull(arrayNodeId2)
        Dim elemNodes = children2.Where(Function(c) c.BrowseName.Name.Contains("[")).ToList()

        If elemNodes.Count < 2 Then
            Console.WriteLine("  Motor_Array has fewer than 2 elements.")
            Return
        End If

        Dim elem1NodeId As NodeId = CType(elemNodes(1).NodeId, NodeId)
        Dim fieldNodes = client.BrowseFull(elem1NodeId)
        Dim speedNode = fieldNodes.FirstOrDefault(Function(f) f.BrowseName.Name = "Speed")

        If speedNode Is Nothing Then Console.WriteLine("  Speed field not found.") : Return

        Dim result As StatusCode = client.WriteValue(CType(speedNode.NodeId, NodeId), newSpeed)
        Console.WriteLine($"  Written {newSpeed} to Motor_Array[1].Speed")
        Console.WriteLine($"  Result: {result}")
        Console.WriteLine("  Reading back Motor_Array[1]:")
        Dim value2 As DataValue = client.ReadValue(elem1NodeId)
        PrintExtensionObject(value2, "    ")
    End Sub

    ' ── Helpers ───────────────────────────────────────────────────────────────

    Private Sub PrintExtensionObject(value As DataValue, Optional indent As String = "  ")
        If value?.Value Is Nothing Then
            Console.WriteLine($"{indent}(null)")
            Return
        End If

        Dim ext As ExtensionObject = TryCast(value.Value, ExtensionObject)
        If ext Is Nothing Then
            Dim arr As ExtensionObject() = TryCast(value.Value, ExtensionObject())
            If arr IsNot Nothing AndAlso arr.Length > 0 Then ext = arr(0)
        End If

        Dim complexType As BaseComplexType = TryCast(ext?.Body, BaseComplexType)
        If complexType IsNot Nothing Then
            For Each prop In complexType.GetPropertyEnumerator()
                Dim fieldValue As Object = complexType(prop.Name)
                Dim nested As BaseComplexType = TryCast(fieldValue, BaseComplexType)
                If nested IsNot Nothing Then
                    Console.WriteLine($"{indent}{prop.Name}:")
                    For Each subProp In nested.GetPropertyEnumerator()
                        Console.WriteLine($"{indent}  {subProp.Name} = {nested(subProp.Name)}")
                    Next
                ElseIf TypeOf fieldValue Is Array Then
                    Dim fieldArr As Array = CType(fieldValue, Array)
                    Dim sb As New StringBuilder("[")
                    For i As Integer = 0 To fieldArr.Length - 1
                        If i > 0 Then sb.Append(", ")
                        sb.Append(fieldArr.GetValue(i))
                    Next
                    sb.Append("]")
                    Console.WriteLine($"{indent}{prop.Name} = {sb}")
                Else
                    Console.WriteLine($"{indent}{prop.Name} = {fieldValue}")
                End If
            Next
        Else
            Console.WriteLine($"{indent}Raw value: {value.Value}")
            Console.WriteLine($"{indent}Tip: Ensure GetComplexTypeSystem().Load() was called.")
        End If
    End Sub

    Private Sub CertificateValidationHandler(ByVal sender As CertificateValidator,
                                             ByVal e As CertificateValidationEventArgs)
        If ServiceResult.IsGood(e.Error) Then
            e.Accept = True
        ElseIf Not e.ContainsUnsuppressibleStatusCodes Then
            e.Accept = True
        ElseIf e.ContainsUnsuppressibleStatusCodes Then
            e.AcceptAll = True
        Else
            Throw New Exception($"Certificate validation failed: {e.Error.Code}")
        End If
    End Sub


    ' =============================================================================
    ' Helper: CreateConfig
    ' =============================================================================
    ' Builds the SessionConfiguration for the selected endpoint.
    '
    ' Certificate handling:
    '   Application certificate -- required for Sign / SignAndEncrypt endpoints.
    '   HTTPS certificate       -- required for opc.https:// endpoints (any SecurityMode).
    '
    ' Load() returns Nothing if the certificate does not exist yet or cannot be read.
    ' Build(True) creates a new self-signed certificate, overwriting any existing file.
    Private Shared Function CreateConfig(ByVal endpoint As EndpointDescription) As SessionConfiguration
        Dim appAlias As String = System.Reflection.Assembly.GetEntryAssembly().GetName().Name
        Dim config As SessionConfiguration = SessionConfiguration.Build(appAlias, endpoint)
        config.AutoConnect = False

        ' HTTPS certificate -- required for opc.https:// endpoints, independent of SecurityMode.
        Dim httpsCert As UaClientCertificate = Nothing
        If endpoint.EndpointUrl IsNot Nothing AndAlso
           endpoint.EndpointUrl.StartsWith("opc.https://", StringComparison.OrdinalIgnoreCase) Then
            Dim host As String = New Uri(endpoint.EndpointUrl).Host
            httpsCert = UaClientCertificate.Load("./pki", host, "secretpassword")
            If httpsCert Is Nothing OrElse Not httpsCert.CheckValidity() Then
                httpsCert = New UaClientCertificate("./pki", "secretpassword", host, 720, "Indi.An GmbH") _
                    .Build(overwrite:=True)
            End If
        End If

        ' Application certificate -- required for secured endpoints (Sign or SignAndEncrypt).
        ' Not needed for SecurityMode.None (unencrypted connections).
        Dim appCert As UaClientCertificate = Nothing
        If Not endpoint.SecurityMode.Equals(MessageSecurityMode.None) Then
            appCert = UaClientCertificate.Load("./pki", appAlias, "secretpassword")
            If appCert Is Nothing OrElse Not appCert.CheckValidity() Then
                appCert = New UaClientCertificate("./pki", "secretpassword", appAlias, 720, "Indi.An GmbH") _
                    .Build(overwrite:=True)
            End If
        End If

        ' SetInstanceCertificate() sets CertificateStorePath and ApplicationCertificateFullPath.
        If appCert IsNot Nothing AndAlso httpsCert IsNot Nothing Then
            config.SetInstanceCertificate(appCert, httpsCert)
        ElseIf appCert IsNot Nothing Then
            config.SetInstanceCertificate(appCert)
        End If

        Return config
    End Function

    ' =============================================================================
    ' Helper: PrintConfig
    ' =============================================================================
    ' Prints the active client configuration to the console so you can verify
    ' all settings at a glance before connecting.
    Private Shared Sub PrintConfig(ByVal config As SessionConfiguration)
        Console.WriteLine("-- Active Client Configuration ------------------------------")
        If config.Endpoint IsNot Nothing Then
            Console.WriteLine("  Endpoint  : " & config.Endpoint.EndpointUrl)
            Console.WriteLine("  Security  : " & config.Endpoint.ToDisplayString())
        End If
        Console.WriteLine("  PKI Store : " & If(config.CertificateStorePath IsNot Nothing, config.CertificateStorePath, "(not set)"))
        Console.WriteLine("  Cert File : " & If(config.ApplicationCertificateFullPath IsNot Nothing, config.ApplicationCertificateFullPath, "(none -- SecurityMode.None)"))
        Console.WriteLine("-------------------------------------------------------------")
        Console.WriteLine()
    End Sub
End Class
