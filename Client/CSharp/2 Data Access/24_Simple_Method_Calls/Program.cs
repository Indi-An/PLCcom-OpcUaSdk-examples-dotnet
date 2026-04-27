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
// PLCcom OPC UA Client SDK - Workshop 24: Simple Method Calls
//
// OPC UA Methods are callable functions in the server address space.
// A client invokes a method by sending a Call request with input
// arguments and receives output arguments in the response.
// This workshop demonstrates calling methods with structured input.
//
// What you will learn:
//   * How to encode structured input arguments with BinaryEncoder
//   * How to create an ExtensionObject for method input
//   * How to call a method and evaluate the result
//   * How to read output arguments from the CallMethodResult
//
// Required server: Server Workshop 13 (Methods)
// Target server:   opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;
using System;
using System.Collections.Generic;

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

             Console.WriteLine();

             Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 24: Simple Method Calls ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  OPC UA Methods are callable functions in the server         ║");
             Console.WriteLine("║  address space. This workshop shows how to call methods      ║");
             Console.WriteLine("║  with structured input arguments using BinaryEncoder.        ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  What you will learn:                                        ║");
             Console.WriteLine("║    * Encode structured input with BinaryEncoder              ║");
             Console.WriteLine("║    * Create ExtensionObjects for method input                ║");
             Console.WriteLine("║    * Call a method and evaluate the result                   ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  Required server: Server Workshop 13 (Methods)               ║");
             Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
             Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
             Console.WriteLine();

            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            EndpointDescriptionCollection Endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:48410"), certificateValidator: client_CertificateValidation);

            //sort endpoints by security level
            Endpoints = UaClient.SortEndpointsBySecurityLevel(Endpoints);

            if (Endpoints.Count > 0)
            {
                Console.WriteLine("endpoints found:");
                int counter = 0;
                foreach (EndpointDescription Endpoint in Endpoints)
                {
                    Console.WriteLine(counter++.ToString() + " => " + Endpoint.ToDisplayString());
                }

                Console.WriteLine("please enter index of desired endpoint");
                string NumberOfEndpoint = Console.ReadLine();
                Console.WriteLine("");
                int iNumberOfEndpoint = -1;
                if (int.TryParse(NumberOfEndpoint, out iNumberOfEndpoint) && iNumberOfEndpoint > -1 && iNumberOfEndpoint < Endpoints.Count)
                {
                    //create a a SessionConfiguration with the selected endpoint and application name
                    SessionConfiguration sessionConfiguration = SessionConfiguration.Build(System.Reflection.Assembly.GetEntryAssembly().GetName().Name,
                                                                                          Endpoints[iNumberOfEndpoint]);
                    sessionConfiguration.AutoConnect = true;

                    //output certificate store path
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " + sessionConfiguration.CertificateStorePath);

                    //Create a new opc client instance and pass your license information
                    using (UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration))
                    {
                        Console.WriteLine("Info: license state => " + client.GetLicenceMessage());
                        Console.WriteLine("");

                        //register events
                        client.ServerConnectionLost += Client_ServerConnectionLost;
                        client.ServerConnected += Client_ServerConnected;
                        client.SessionClosing += Client_SessionClosing;
                        client.KeepAlive += Client_KeepAlive;
                        client.CertificateValidation += client_CertificateValidation;


                        /*
                        lets starting a method call, step by step
                        In this simple case, we pass a simple structure named as 'DataStructure_One" constructed as follows:

                        structure DataStructure_One = 
                        {
                            int myIntValue1,
                            string myStringValue2,
                            int myIntValue3,
                            int myIntValue4,
                            string myStringValue5
                        }

                        Object to which the method should be applied is named as "myObjectNode"
                        Method is named as "myMethodNode"
                        */

                        int myIntValue1 = 1;
                        string myStringValue2 = "testvalue";
                        int myIntValue3 = 3333;
                        int myIntValue4 = 4444;
                        string myStringValue5 = "a_string_value";

                        //create a Encoder instance
                        BinaryEncoder encoder = new BinaryEncoder(client.GetMessageContext());

                        //put objects to encoder with given order
                        encoder.WriteInt32("", myIntValue1);
                        encoder.WriteString("", myStringValue2);
                        encoder.WriteInt32("", myIntValue3);
                        encoder.WriteInt32("", myIntValue4);
                        encoder.WriteString("", myStringValue5);

                        //read byte array from encoder
                        byte[] argumentByteArray = encoder.CloseAndReturnBuffer();

                        //create an extension object and pass arguments to ExtensionObject.Body
                        ExtensionObject extensionObjectWithInputArguments = new ExtensionObject();
                        extensionObjectWithInputArguments.Body = argumentByteArray;

                        //set type of structure, create a new ExpandedNodeId by name and namespace
                        extensionObjectWithInputArguments.TypeId = new ExpandedNodeId("DataStructure_One", 2);

                        //create your InputArguments with extensionObject
                        VariantCollection inputArguments = new VariantCollection();
                        inputArguments.Add(new Variant(extensionObjectWithInputArguments));

                        //create a new NodeId for the Object to which the method should be applied by path
                        NodeId objectNode = client.GetNodeIdByPath("Objects.Plant.myObjectNode");
                        if (objectNode == null)
                        {
                            Console.WriteLine("myObjectNode not found - is Server Workshop 13 running?");
                            return;
                        }

                        // Browse children of myObjectNode to find myMethodNode
                        NodeId methodNode = null;
                        var browseDesc = new BrowseDescription
                        {
                            NodeId = objectNode,
                            BrowseDirection = BrowseDirection.Forward,
                            ReferenceTypeId = ReferenceTypeIds.HasComponent,
                            IncludeSubtypes = true,
                            NodeClassMask = (uint)NodeClass.Method,
                            ResultMask = (uint)BrowseResultMask.All
                        };
                        var browseCol = new BrowseDescriptionCollection { browseDesc };
                        var refs = client.BrowseFull(browseCol);
                        foreach (var r in refs)
                        {
                            if (r.BrowseName.Name == "myMethodNode")
                            {
                                methodNode = ExpandedNodeId.ToNodeId(r.NodeId, client.GetNamespaceUris());
                                break;
                            }
                        }
                        if (methodNode == null)
                        {
                            Console.WriteLine("myMethodNode not found under myObjectNode");
                            return;
                        }

                        //create a CallMethodRequest instance and pass your arguments
                        CallMethodRequest request = new CallMethodRequest();
                        request.ObjectId = objectNode;
                        request.MethodId = methodNode;
                        request.InputArguments = inputArguments;

                        //call your method 
                        CallMethodResult result = client.Call(request);

                        //finaly evaluate your results,
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
        //external certificate validation
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
        //event opc ua server is connected
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connected");
    }

    private void Client_ServerConnectionLost(object sender, EventArgs e)
    {
        //event connection to opc ua server lost
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connection lost");
    }

    void Client_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        //catch the keepalive event of opc ua server
    }

    private void Client_SessionClosing(object sender, EventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session closed");
    }
}
