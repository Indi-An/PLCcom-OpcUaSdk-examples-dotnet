using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;
using System;

class Program
{

    static void Main(string[] args)
    {
        Program program = new Program();
        program.Start();
    }

    void Start()
    {
        try
        {

            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            EndpointDescriptionCollection Endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:50520/PLCcom/DataAccessServer"), 60000);

            // Sort endpoints by security level (highest security first)
            Endpoints = UaClient.SortEndpointsBySecurityLevel(Endpoints);

            if (Endpoints.Count > 0)
            {
                Console.WriteLine("endpoints found:");
                int counter = 0;
                foreach (EndpointDescription Endpoint in Endpoints)
                {
                    Console.WriteLine(counter++.ToString() + " => " + UaClient.EndpointToString(Endpoint));
                }

                Console.WriteLine("please enter index of desired endpoint");
                string NumberOfEndpoint = Console.ReadLine();
                Console.WriteLine("");
                int iNumberOfEndpoint = -1;
                if (int.TryParse(NumberOfEndpoint, out iNumberOfEndpoint) && iNumberOfEndpoint > -1 && iNumberOfEndpoint < Endpoints.Count)
                {
                    // Create a SessionConfiguration with the selected endpoint and application name
                    SessionConfiguration sessionConfiguration = SessionConfiguration.Build(System.Reflection.Assembly.GetEntryAssembly().GetName().Name,
                                                                                          Endpoints[iNumberOfEndpoint]);

                    // Enable AutoConnect - the client will connect and reconnect automatically
                    sessionConfiguration.AutoConnect = true;

                    // Display the certificate store path for debugging purposes
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " + sessionConfiguration.CertificateStorePath);

                    // Create a new OPC UA client instance with license credentials
                    using (UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration))
                    {
                        Console.WriteLine("Info: license state => " + client.GetLicenceMessage());
                        Console.WriteLine("");

                        // Register event handlers to monitor the connection state
                        client.ServerConnectionLost += Client_ServerConnectionLost;
                        client.ServerConnected += Client_ServerConnected;
                        client.SessionClosing += Client_SessionClosing;
                        client.KeepAlive += Client_KeepAlive;
                        client.CertificateValidation += client_CertificateValidation;

                        /*
                        let�s starting a method call, step by step
                        In this case, we pass a structure named as 'DataStructure_One" constructed as follows:

                        structure DataStructure_One = 
                        {
                            int myIntValue1,
                            string myStringValue2,
                            DataStructure_two DataStructure_two,
                            int myIntValue3,
                            DataStructure_two[] DataStructure_twoArray
                        }

                        Structure DataStructure_One contains some flat data types, a structure of DataStructure_two 
                        and a array of DataStructure_two structures:

                        structure DataStructure_two = 
                        {
                            int myIntValue10,
                            string myStringValue11,
                            int myIntValue12
                        }

                        Object to which the method should be applied is named as "myObjectNode"
                        Method is named as "myMethodNode"
                        */

                        //create Encoder instances
                        BinaryEncoder encoderDataStructure_One = new BinaryEncoder(client.GetMessageContext());

                        //put objects to encoderDataStructure_One with given order
                        encoderDataStructure_One.WriteInt32("", 1); //myIntValue1
                        encoderDataStructure_One.WriteString("", "test_string"); //myStringValue2

                        #region create and add embedded structure

                        //create encoderDataStructure_Two instance
                        BinaryEncoder encoderDataStructure_Two = new BinaryEncoder(client.GetMessageContext());

                        //put objects to encoderDataStructure_Two with given order
                        encoderDataStructure_Two.WriteInt32("", 222);//myIntValue10
                        encoderDataStructure_Two.WriteString("", "test_string11"); //myStringValue11
                        encoderDataStructure_Two.WriteInt32("", 1212);//myIntValue12

                        // Extract the encoded byte array from the encoder
                        byte[] argumentByteArray = encoderDataStructure_Two.CloseAndReturnBuffer();

                        //create an extension object and pass arguments to ExtensionObject.Body 
                        ExtensionObject extensionObjectDataStructure_Two = new ExtensionObject();
                        extensionObjectDataStructure_Two.Body = argumentByteArray;

                        //set type of structure, create a new ExpandedNodeId by name and namespace
                        extensionObjectDataStructure_Two.TypeId = new ExpandedNodeId("DataStructure_Two", 3);

                        //write structure to input arguments
                        encoderDataStructure_One.WriteExtensionObject("", extensionObjectDataStructure_Two);

                        #endregion

                        encoderDataStructure_One.WriteInt32("", 3333);//myIntValue3

                        #region create and add embedded structure Array 

                        //create a Array of DataStructure_Two objects with three objects

                        ExtensionObjectCollection dataStructure_TwoCollection = new ExtensionObjectCollection();

                        for (int i = 0; i < 3; i++)
                        {
                            //create encoderDataStructure_Two instance
                            encoderDataStructure_Two = new BinaryEncoder(client.GetMessageContext());

                            //put objects to encoderDataStructure_Two with given order
                            encoderDataStructure_Two.WriteInt32("", 555);//myIntValue10
                            encoderDataStructure_Two.WriteString("", "test_stringArray365"); //myStringValue11
                            encoderDataStructure_Two.WriteInt32("", 1212);//myIntValue12

                            // Extract the encoded byte array from the encoder
                            argumentByteArray = encoderDataStructure_Two.CloseAndReturnBuffer();

                            //create an extension object and pass arguments to ExtensionObject.Body 
                            extensionObjectDataStructure_Two = new ExtensionObject();
                            extensionObjectDataStructure_Two.Body = argumentByteArray;

                            //set type of structure, create a new ExpandedNodeId by name and namespace
                            extensionObjectDataStructure_Two.TypeId = new ExpandedNodeId("DataStructure_Two", 3);

                            //add structure to ExtensionObjectCollection
                            dataStructure_TwoCollection.Add(extensionObjectDataStructure_Two);
                        }

                        //write structure array to input arguments
                        encoderDataStructure_One.WriteExtensionObjectArray("", dataStructure_TwoCollection);

                        #endregion
                        encoderDataStructure_One.WriteInt32("", 3333);//myIntValue3

                        // Extract the encoded byte array from the encoder
                        argumentByteArray = encoderDataStructure_One.CloseAndReturnBuffer();

                        //create an extension object and pass arguments to ExtensionObject.Body
                        ExtensionObject extensionObjectWithInputArguments = new ExtensionObject();
                        extensionObjectWithInputArguments.Body = argumentByteArray;

                        //set type of structure, create a new ExpandedNodeId by name and namespace
                        extensionObjectWithInputArguments.TypeId = new ExpandedNodeId("DataStructure_One", 3);

                        //create your InputArguments with extensionObject
                        VariantCollection inputArguments = new VariantCollection();
                        inputArguments.Add(new Variant(extensionObjectWithInputArguments));

                        //create a new NodeId for the Object to which the method should be applied by name and namespace
                        NodeId objectNode = new NodeId("myObjectNode", 3);

                        //create a new NodeId for the Method by name and namespace
                        NodeId methodNode = new NodeId("myMethodNode", 3);

                        //create a CallMethodRequest instance and pass your arguments
                        CallMethodRequest request = new CallMethodRequest();
                        request.ObjectId = objectNode;
                        request.MethodId = methodNode;
                        request.InputArguments = inputArguments;

                        // Execute the method call on the server
                        CallMethodResult result = client.Call(request);

                        // Evaluate the method call results
                        if (StatusCode.IsGood(result.StatusCode))
                        {
                            foreach (Variant outputArgument in result.OutputArguments)
                            {
                                if (outputArgument != Variant.Null)
                                    Console.WriteLine("output argument: " + outputArgument.ToString());
                            }
                        }
                        else
                        {
                            Console.WriteLine("Method call failed " + result.StatusCode.ToString());
                        }
                    }
                }
                else
                {
                    Console.WriteLine("invalid number of Endpoint");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("no endpoints found");
                Console.WriteLine();
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.WriteLine();
        }
        finally
        {
            Console.WriteLine("press enter for exit");
            Console.ReadLine();
        }

    }

    void client_CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        // Handle server certificate validation
        if (ServiceResult.IsGood(e.Error))
            e.Accept = true;
        else if (!e.ContainsUnsuppressibleStatusCodes)
            e.Accept = true;
        else if (e.ContainsUnsuppressibleStatusCodes)
            e.AcceptAll = true; //you can accept all unsuppressible statuscode with this flag
        else
        {
            throw new Exception(string.Format("Failed to validate certificate with error code {0}: {1}", e.Error.Code, e.Error.AdditionalInfo));
        }
    }

    private void Client_ServerConnected(object sender, EventArgs e)
    {
        // Fired when the OPC UA session is successfully established
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connected");
    }

    private void Client_ServerConnectionLost(object sender, EventArgs e)
    {
        // Fired when the connection to the OPC UA server is lost
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connection lost");
    }

    void Client_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        // Fired periodically to indicate the server is still alive
    }

    private void Client_SessionClosing(object sender, EventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session closed");
    }
}
