using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;
using System.Linq;
using PLCcom.Opc.Ua.Client.ComplexTypes;

class Program
{
    UaClient client = null;

    private bool Verbose { get; set; } = true;
    private bool WriteComplexInt { get; set; } = false;

    private List<INode> allCustomTypeVariables = null;

    private IList<INode> allVariableNodes = null;

    //actual publishing state of subscription
    private PublishingState publishingState = PublishingState.UNDEFINED;

    static void Main(string[] args)
    {
        Program program = new Program();
        program.Start();
    }

    async void Start()
    {
        try
        {

            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            EndpointDescriptionCollection Endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:50540/PLCcom/HistoricalEventsServer"), 60000);

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

                    //disable auto connect functionality
                    sessionConfiguration.AutoConnect = false;

                    // Display the certificate store path for debugging purposes
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " + sessionConfiguration.CertificateStorePath);

                    // Create a new OPC UA client instance with license credentials
                    client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration);

                    Console.WriteLine("Info: license state => " + client.GetLicenceMessage());
                    Console.WriteLine("");

                    // Register event handlers to monitor the connection state
                    client.ServerConnectionLost += Client_ServerConnectionLost;
                    client.ServerConnected += Client_ServerConnected;
                    client.SessionClosing += Client_SessionClosing;
                    client.KeepAlive += Client_KeepAlive;
                    client.CertificateValidation += client_CertificateValidation;

                    Console.WriteLine("Connect the opc ua client");
                    client.Connect();

                    try
                    {

                        Stopwatch stopWatch = new Stopwatch();
                        Console.WriteLine("Begin browse all nodes");
                        stopWatch.Start();
                        allVariableNodes = BrowseAllVariables();
                        allCustomTypeVariables = allVariableNodes.Where(n => ((VariableNode)n).DataType == DataTypeIds.Structure).ToList();
                        allCustomTypeVariables.AddRange(allVariableNodes.Where(n => ((VariableNode)n).DataType.NamespaceIndex != 0).ToList());
                        stopWatch.Stop();

                        Console.WriteLine($" Browse all nodes took {stopWatch.ElapsedMilliseconds}ms.");
                        Console.WriteLine($" Browsed {allVariableNodes.Count} nodes, from which {allCustomTypeVariables.Count} are custom type variables.");

                        Console.WriteLine("Begin load the server type dictionary. This will make all user-defined types known.");

                        stopWatch.Reset();
                        stopWatch.Start();

                        var complexTypeSystem = client.GetComplexTypeSystem();
                        complexTypeSystem.Load();

                        stopWatch.Stop();

                        Console.WriteLine($" Load type system took {stopWatch.ElapsedMilliseconds}ms.");

                        Console.WriteLine("Custom types defined for this session:");
                        foreach (var type in complexTypeSystem.GetDefinedTypes())
                        {
                            Console.WriteLine($"{type.Namespace}.{type.Name}");
                        }

                        Console.WriteLine($" Loaded {client.GetSessionDataTypeSystem().Count} dictionaries:");

                        foreach (var dictionary in client.GetSessionDataTypeSystem())
                        {
                            Console.WriteLine($" + {dictionary.Value.Name}");
                            foreach (var type in dictionary.Value.DataTypes)
                            {
                                Console.WriteLine($" -- {type.Key}:{type.Value}");
                            }
                        }

                        Console.WriteLine("Begin read all variables with custom type");
                        foreach (VariableNode variableNode in allCustomTypeVariables)
                        {
                            try
                            {

                                Console.WriteLine($" read variable {variableNode.NodeId.ToString()}");
                                var value = client.ReadValue(variableNode.NodeId);

                                CastInt32ToEnum(variableNode, value);
                                Console.WriteLine($" -- {variableNode}:{value}");

                                //get all Extension objects from value
                                var allExtensionObjects = GetExtensionObjects(value);

                                foreach (ExtensionObject extensionObject in allExtensionObjects)
                                {
                                    if (extensionObject != null)
                                    {
                                        var complexType = extensionObject.Body as BaseComplexType;
                                        if (complexType != null)
                                        {
                                            foreach (var item in complexType.GetPropertyEnumerator())
                                            {
                                                if (Verbose)
                                                {
                                                    Console.WriteLine($" -- -- {item.Name}:{complexType[item.Name]}");
                                                }
                                                if (WriteComplexInt && item.PropertyType == typeof(Int32))
                                                {
                                                    var data = complexType[item.Name];
                                                    if (data != null)
                                                    {
                                                        complexType[item.Name] = (Int32)data + 1;
                                                    }
                                                    Console.WriteLine($" -- -- Write: {item.Name}, {complexType[item.Name]}");
                                                    client.WriteValue(variableNode.NodeId, value);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (ServiceResultException sre)
                            {
                                if (sre.StatusCode == StatusCodes.BadUserAccessDenied)
                                {
                                    Console.WriteLine($" -- {variableNode}: Access denied!");
                                }
                            }
                        }

                        Console.WriteLine("Begin monitoring all nodes with custom data type");
                        // Create a new subscription for monitoring data changes
                        using (Subscription subscription = new Subscription())
                        {
                            subscription.PublishingEnabled = true;
                            subscription.PublishingInterval = 5000;
                            subscription.DisplayName = "mySubsription";

                            // Register subscription state change events
                            subscription.StateChanged += Subscription_StateChanged;
                            subscription.PublishStatusChanged += Subscription_PublishStatusChanged;

                            // Add the subscription to the client instance
                            client.AddSubscription(subscription);

                            List<MonitoredItem> list = new List<MonitoredItem>();

                            foreach (var customVariable in allCustomTypeVariables)
                            {
                                var newItem = new MonitoredItem(subscription.DefaultItem)
                                {
                                    DisplayName = customVariable.DisplayName.Text,
                                    StartNodeId = ExpandedNodeId.ToNodeId(customVariable.NodeId, client.GetNamespaceUris()),
                                    SamplingInterval = 500,
                                    QueueSize = UInt32.MaxValue

                                };
                                newItem.Notification += OnComplexTypeNotification;
                                list.Add(newItem);
                            }

                            subscription.AddItems(list);

                            // Apply all pending changes to the subscription (creates monitored items on the server)
                            subscription.ApplyChanges();
                            //enable publishing mode of subscription
                            //subscription.SetPublishingMode(true);
                            //subscription.Modify();

                            Console.WriteLine();
                            Console.WriteLine("press enter for exit");
                            Console.ReadLine();

                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        Console.WriteLine("press enter for exit");
                        Console.ReadLine();
                    }
                }
                else
                {
                    Console.WriteLine("invalid number of Endpoint");
                    Console.WriteLine("press enter for exit");
                    Console.ReadLine();

                }
            }
            else
            {
                Console.WriteLine("no endpoints found");
                Console.WriteLine("press enter for exit");
                Console.ReadLine();
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.WriteLine("press enter for exit");
            Console.ReadLine();
        }
    }

    private void OnComplexTypeNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        try
        {
            //lock prozedure
            System.Threading.Monitor.Enter(this);

            //take variableNode from cache
            var variableNode = allCustomTypeVariables.Where(n => ((VariableNode)n).NodeId == monitoredItem.StartNodeId).FirstOrDefault() as VariableNode;
            if (variableNode != null)
            {
                //loop over all values 
                foreach (var value in monitoredItem.DequeueValues())
                {
                    bool successfullyProcessed = false;
                    if (value != null && value.Value != null && StatusCode.IsGood(value.StatusCode))
                    {
                        //cast eventual enum types
                        CastInt32ToEnum(variableNode, value);
                        Console.WriteLine($" -- {variableNode}:{value}");

                        var allExtensionObjects = GetExtensionObjects(value);

                        foreach (ExtensionObject extensionObject in allExtensionObjects)
                        {
                            //check if value a BaseComplexType
                            var complexType = extensionObject.Body as BaseComplexType;
                            if (complexType != null)
                            {
                                //loop over all known propertys
                                foreach (var item in complexType.GetPropertyEnumerator())
                                {
                                    Console.WriteLine($" -- --{monitoredItem.DisplayName} : {item.Name} : Value => {complexType[item.Name]} : SourceTimestamp => {value.SourceTimestamp} : StatusCode => {value.StatusCode}");
                                }
                                successfullyProcessed = true;
                            }

                            if (!successfullyProcessed)
                            {
                                //simple print, value is not a known BaseComplexType 
                                Console.WriteLine("{0}: {1}, {2}", monitoredItem.DisplayName, value.SourceTimestamp, value.StatusCode);
                                if (Verbose)
                                    Console.WriteLine(value);
                            }
                        }
                    }

                    //simple print, value is not a known BaseComplexType 
                    MonitoredItemNotification notification = e.NotificationValue as MonitoredItemNotification;
                    Console.WriteLine("{0}: {1}, {2}", monitoredItem.DisplayName, notification.Value.SourceTimestamp, notification.Value.StatusCode);
                    if (Verbose)
                        Console.WriteLine(notification.Value);

                }
            }
            else
            {
                //simple print, value is not a known variableNode 
                MonitoredItemNotification notification = e.NotificationValue as MonitoredItemNotification;
                Console.WriteLine("{0}: {1}, {2}", monitoredItem.DisplayName, notification.Value.SourceTimestamp, notification.Value.StatusCode);
                if (Verbose)
                    Console.WriteLine(notification.Value);
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            System.Threading.Monitor.Exit(this);
        }
    }

    private List<ExtensionObject> GetExtensionObjects(DataValue value)
    {
        List<ExtensionObject> allExtensionObjects = new List<ExtensionObject>();
        if (value != null && value.Value != null && StatusCode.IsGood(value.StatusCode))
        {

            //check if value a ExtensionObject or a array of ExtensionObject
            if (value.Value.GetType().IsArray)
            {
                var extensionObjects = value.Value as ExtensionObject[];
                if (extensionObjects != null)
                    allExtensionObjects.AddRange(extensionObjects);
            }
            else
            {
                var extensionObject = value.Value as ExtensionObject;
                if (extensionObject != null)
                    allExtensionObjects.Add(extensionObject);
            }
        }
        return allExtensionObjects;
    }

    private void Subscription_StateChanged(Subscription subscription, SubscriptionStateChangedEventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " State of Subscription " + UaClient.SubscriptionToString(subscription) + " changed to => " + e.Status.ToString());
    }

    private void Subscription_PublishStatusChanged(object sender, EventArgs e)
    {
        /*
        check your publish state of your subscription
        if the publish state permanent stopped, then you have to recreate your subscription with old subscription as template
        In this case, please have a look to the PublishingInterval setting, possibly be the value must be increased
        */

        Subscription subscription = sender as Subscription;
        if (subscription != null)
        {
            PublishingState currentpublishingState = subscription.PublishingStopped ? PublishingState.STOPPED : PublishingState.RUNNING;
            if (currentpublishingState != publishingState || currentpublishingState == PublishingState.STOPPED)
                Console.WriteLine(DateTime.Now.ToLocalTime() + "Publishing state of Subscription " + UaClient.SubscriptionToString((Subscription)sender) + " => " + currentpublishingState.ToString());

            publishingState = currentpublishingState;
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

    /// <summary>
    /// Helper to cast a enumeration node value to an enumeration type.
    /// </summary>
    private void CastInt32ToEnum(VariableNode variableNode, DataValue value)
    {
        if (value.Value?.GetType() == typeof(Int32))
        {
            // test if this is an enum datatype?
            Type systemType = client.GetSession().Factory.GetSystemType(NodeId.ToExpandedNodeId(variableNode.DataType, client.GetNamespaceUris()));
            if (systemType != null)
            {
                value.Value = Enum.ToObject(systemType, value.Value);
            }
        }
    }

    /// <summary>
    /// Browse all variables in the objects folder.
    /// </summary>
    private IList<INode> BrowseAllVariables()
    {
        var result = new List<INode>();
        var nodesToBrowse = new ExpandedNodeIdCollection();
        nodesToBrowse.Add(ObjectIds.ObjectsFolder);

        while (nodesToBrowse.Count > 0)
        {
            var nextNodesToBrowse = new ExpandedNodeIdCollection();
            foreach (var node in nodesToBrowse)
            {
                try
                {
                    var organizers = client.GetNodeCache().FindReferencesAsync(
                        node,
                        ReferenceTypeIds.Organizes,
                        false,
                        false).GetAwaiter().GetResult();
                    var components = client.GetNodeCache().FindReferencesAsync(
                        node,
                        ReferenceTypeIds.HasComponent,
                        false,
                        false).GetAwaiter().GetResult();
                    var properties = client.GetNodeCache().FindReferencesAsync(
                        node,
                        ReferenceTypeIds.HasProperty,
                        false,
                        false).GetAwaiter().GetResult();
                    nextNodesToBrowse.AddRange(organizers
                        .Where(n => n is ObjectNode)
                        .Select(n => n.NodeId).ToList());
                    nextNodesToBrowse.AddRange(components
                        .Where(n => n is ObjectNode)
                        .Select(n => n.NodeId).ToList());
                    result.AddRange(organizers.Where(n => n is VariableNode));
                    result.AddRange(components.Where(n => n is VariableNode));
                    result.AddRange(properties.Where(n => n is VariableNode));
                }
                catch (ServiceResultException sre)
                {
                    if (sre.StatusCode == StatusCodes.BadUserAccessDenied)
                    {
                        Console.WriteLine($"Access denied: Skip node {node}.");
                    }
                }
            }
            nodesToBrowse = nextNodesToBrowse;
        }
        return result;
    }

    private enum PublishingState
    {
        UNDEFINED,
        RUNNING,
        STOPPED
    }
}
