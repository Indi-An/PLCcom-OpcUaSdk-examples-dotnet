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
' PLCcom OPC UA Client SDK - Workshop 43: Read Historical Events
'
' OPC UA servers can store historical events (alarms, state changes,
' operator actions). This workshop reads, inspects and deletes past
' events from the server using HistoryRead and HistoryUpdate.
'
' What you will learn:
'   * How to read historical events for a time range
'   * How to specify event filter fields
'   * How to delete historical events by EventId
'
' Required server: Server Workshop 33 (Historical Events)
' opc.tcp://localhost:48410
' ==============================================================================

Imports System
Imports System.Collections.Generic
Imports System.Reflection
Imports System.Text
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk

Public Class Program

    Private client As UaClient = Nothing
    Private nodeId As NodeId = Nothing
    Private filter As EventFilter = Nothing
    Private lastResult As HistoryEvent = Nothing
    Private eventIdIndex As Integer = -1

    Public Shared Sub Main(ByVal args As String())
        Dim program As New Program()
        program.Start()
    End Sub

    Private Sub Start()
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 43: Read Hist. Events   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  OPC UA servers can store historical events (alarms, state   ║")
        Console.WriteLine("║  changes, operator actions). This workshop reads, inspects   ║")
        Console.WriteLine("║  and deletes past events from the server.                    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  What you will learn:                                        ║")
        Console.WriteLine("║    * Read historical events for a time range                 ║")
        Console.WriteLine("║    * Specify event filter fields                             ║")
        Console.WriteLine("║    * Delete historical events by EventId                     ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Required server: Server Workshop 33 (Historical Events)     ║")
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
                Console.WriteLine("  No endpoints found. Is Server Workshop 33 running?")
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

            AddHandler client.ServerConnectionLost, Sub(s, e) Console.WriteLine(Date.Now.ToLocalTime() & " Session connection lost")
            AddHandler client.ServerConnected, Sub(s, e) Console.WriteLine(Date.Now.ToLocalTime() & " Session connected")
            AddHandler client.SessionClosing, Sub(s, e) Console.WriteLine(Date.Now.ToLocalTime() & " Session closed")
            AddHandler client.CertificateValidation, AddressOf CertificateValidationHandler

            Console.Write("  Connecting ... ")
            client.Connect()
            Console.WriteLine("OK")
            Console.WriteLine()

            ' Resolve reactor node via browse path
            nodeId = client.GetNodeIdByPath("Objects.Plant.Reactor")
            If nodeId Is Nothing Then
                Console.WriteLine("  Could not find 'Objects.Plant.Reactor'.")
                Console.WriteLine("  Is Server Workshop 33 running?")
                Console.ReadLine()
                Return
            End If
            Console.WriteLine($"  Reactor NodeId: {nodeId}")
            Console.WriteLine()

            ' Build event filter with SelectClauses for HistoryRead
            filter = New EventFilter()
            filter.SelectClauses = New SimpleAttributeOperandCollection From {
                New SimpleAttributeOperand With {.TypeDefinitionId = ObjectTypeIds.BaseEventType, .BrowsePath = New QualifiedNameCollection From {BrowseNames.EventType},  .AttributeId = Attributes.Value},
                New SimpleAttributeOperand With {.TypeDefinitionId = ObjectTypeIds.BaseEventType, .BrowsePath = New QualifiedNameCollection From {BrowseNames.SourceName}, .AttributeId = Attributes.Value},
                New SimpleAttributeOperand With {.TypeDefinitionId = ObjectTypeIds.BaseEventType, .BrowsePath = New QualifiedNameCollection From {BrowseNames.Time},       .AttributeId = Attributes.Value},
                New SimpleAttributeOperand With {.TypeDefinitionId = ObjectTypeIds.BaseEventType, .BrowsePath = New QualifiedNameCollection From {BrowseNames.Message},    .AttributeId = Attributes.Value},
                New SimpleAttributeOperand With {.TypeDefinitionId = ObjectTypeIds.BaseEventType, .BrowsePath = New QualifiedNameCollection From {BrowseNames.Severity},   .AttributeId = Attributes.Value},
                New SimpleAttributeOperand With {.TypeDefinitionId = ObjectTypeIds.BaseEventType, .BrowsePath = New QualifiedNameCollection From {BrowseNames.EventId},    .AttributeId = Attributes.Value}
            }

            ' Determine EventId field index for delete operations
            For i As Integer = 0 To filter.SelectClauses.Count - 1
                If filter.SelectClauses(i).BrowsePath(0).Name = "EventId" Then
                    eventIdIndex = i
                    Exit For
                End If
            Next

            ' Command loop
            While True
                Console.WriteLine("  Select operation:")
                Console.WriteLine("  1 - Read    (historical events from last 24 hours)")
                Console.WriteLine("  2 - Delete  (delete all events from last read)")
                Console.WriteLine("  3 - Exit")
                Console.Write("  > ")

                Dim input As String = Console.ReadLine()
                If String.IsNullOrEmpty(input) OrElse input = "3" Then Exit While

                Try
                    Select Case input
                        Case "1"
                            ReadEvents()
                        Case "2"
                            DeleteEvents()
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
            If client IsNot Nothing AndAlso client.GetSessionState().Equals(SessionState.Connected) Then
                client.Disconnect()
            End If
        End Try
    End Sub

    Private Sub ReadEvents()
        Console.WriteLine("  Reading historical events from the last 24 hours...")
        lastResult = client.HistoryRead(nodeId, filter,
            Date.UtcNow.AddDays(-1), Date.UtcNow, 100)

        If lastResult?.Events Is Nothing OrElse lastResult.Events.Count = 0 Then
            Console.WriteLine("  (no historical events found)")
            Console.WriteLine("  Tip: Let Server Workshop 33 run for a while to accumulate events.")
            lastResult = Nothing
            Return
        End If

        Console.WriteLine($"  {lastResult.Events.Count} historical event(s) found:")
        Console.WriteLine()

        For Each ev As HistoryEventFieldList In lastResult.Events
            Dim sb As New StringBuilder()
            For i As Integer = 0 To ev.EventFields.Count - 1
                If ev.EventFields(i).Value Is Nothing Then Continue For
                Dim fieldName As String = filter.SelectClauses(i).BrowsePath(0).Name
                Dim value As Object = ev.EventFields(i).Value
                If TypeOf value Is Byte() Then value = ByteArrayToString(CType(value, Byte()))
                sb.Append($"  {fieldName}={value}  ")
            Next
            Console.WriteLine(sb.ToString())
        Next
    End Sub

    Private Sub DeleteEvents()
        If lastResult?.Events Is Nothing OrElse lastResult.Events.Count = 0 Then
            Console.WriteLine("  No events to delete. Run Read first.")
            Return
        End If
        If eventIdIndex < 0 Then
            Console.WriteLine("  EventId field not found in filter.")
            Return
        End If

        Dim eventIds As New List(Of Byte())
        For Each ev As HistoryEventFieldList In lastResult.Events
            Dim id As Byte() = TryCast(ev.EventFields(eventIdIndex).Value, Byte())
            If id IsNot Nothing Then eventIds.Add(id)
        Next
        If eventIds.Count = 0 Then Console.WriteLine("  No EventIds found.") : Return

        Dim result As HistoryUpdateResult = client.HistoryUpdate(nodeId, eventIds)
        Console.WriteLine($"  Deleted {eventIds.Count} event(s)  Result={result?.StatusCode}")
        lastResult = Nothing
        Console.WriteLine("  Done. Run Read again to verify.")
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

    Public Shared Function ByteArrayToString(ByVal ba As Byte()) As String
        Dim hex As New StringBuilder(ba.Length * 2)
        For Each b As Byte In ba
            hex.AppendFormat("{0:x2}", b)
        Next
        Return hex.ToString()
    End Function


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
