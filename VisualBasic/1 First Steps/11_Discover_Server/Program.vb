Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client.Sdk

Public Class Program
    Public Shared Sub Main(ByVal args As String())
        Dim p As Program = New Program()
        p.Start()
    End Sub

    Private Sub Start()
        Try
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            Dim url As String = "opc.tcp://localhost:50520/UA/DataAccessServer"
            Console.WriteLine($"Start discover endpoints url: { url}")

            'get all servers
            Dim servers As ApplicationDescriptionCollection = UaClient.FindServers(New Uri(url), 60000)

            ' populate the server list with the discovery URLs for the available servers.
            For Each server As ApplicationDescription In servers

                ' don't show discovery servers.
                If server.ApplicationType = ApplicationType.DiscoveryServer Then
                    Continue For
                End If

                For Each discoveryUrl As String In server.DiscoveryUrls
                    'Get Endpoints from server
                    Dim endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri(url), 60000)

                    If endpoints.Count > 0 Then
                        Console.WriteLine("endpoints found:")
                        Dim counter As Integer = 0

                        For Each endpoint As EndpointDescription In endpoints
                            Console.WriteLine($"{Math.Min(Threading.Interlocked.Increment(counter), counter - 1).ToString() } => { UaClient.EndpointToString(endpoint)}")
                        Next
                    Else
                        Console.WriteLine("no discovery endpoints found")
                    End If
                Next
            Next

            Console.WriteLine("End getting Endports from UA Application")
            Console.WriteLine()
            Console.WriteLine("press enter for exit")
            Console.ReadLine()
        Catch ex As Exception
            Console.WriteLine(ex)
            Console.WriteLine("press enter for exit")
            Console.ReadLine()
        End Try
    End Sub
End Class
