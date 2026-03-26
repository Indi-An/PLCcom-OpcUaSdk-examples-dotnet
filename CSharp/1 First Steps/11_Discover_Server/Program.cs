using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client.Sdk;
using System;

class Program
{
    static void Main(string[] args)
    {
        Program p = new Program();
        p.Start();
    }

    void Start()
    {
        try
        {
            // Define the URL of the OPC UA server to discover
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            string url = "opc.tcp://localhost:50520/PLCcom/DataAccessServer";
            Console.WriteLine("Start discover endpoints url: " + url);

            // Use the UaClient.FindServers method to retrieve all registered OPC UA servers
            ApplicationDescriptionCollection servers = UaClient.FindServers(new Uri(url), 60000);

            // Iterate through the discovered servers
            foreach (ApplicationDescription server in servers)
            {
                // Skip discovery servers - we only want application servers
                if (server.ApplicationType == ApplicationType.DiscoveryServer)
                {
                    continue;
                }

                // Each server may expose multiple discovery URLs
                foreach (string discoveryUrl in server.DiscoveryUrls)
                {
                    // Query the available endpoints from the server
                    EndpointDescriptionCollection endpoints = UaClient.GetEndpoints(new Uri(url), 60000);

                    if (endpoints.Count > 0)
                    {
                        Console.WriteLine("endpoints found:");
                        int counter = 0;
                        foreach (EndpointDescription endpoint in endpoints)
                        {
                            // Display each endpoint with its security settings
                            Console.WriteLine(counter++.ToString() + " => " + UaClient.EndpointToString(endpoint));
                        }
                    }
                    else
                    {
                        Console.WriteLine("no discovery endpoints found");
                    }
                }
            }
            Console.WriteLine("End getting Endpoints from UA Application");

            Console.WriteLine();
            Console.WriteLine("press enter for exit");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.WriteLine("press enter for exit");
            Console.ReadLine();
        }
    }

}
