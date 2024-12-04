using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;
using OpenTap;
using System.Net.WebSockets;
using System.Net;
using System.Text.Json.Nodes;

namespace OpenTap.Metrics.ResourceDiscovery;

public class KccsDiscoveredResource : DiscoveredResource
{
    public string Manufacturer { get; set; }
    public string InterfaceType { get; set; }
    public string[] VisaAddress { get; set; }
    public string[] VisaAliases { get; set; }
}

public class KccsResourceDiscovery : IResourceDiscovery, IDisposable
{
    public double Priority => 1;

    private static TraceSource log = OpenTap.Log.CreateSource(nameof(KccsResourceDiscovery));
    private static ManualResetEvent StartEvent = null;
    private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


    private static int requestId = 0;


    private Dictionary<string, DiscoveredResource> _discoveredResources;

    public IEnumerable<DiscoveredResource> DiscoverResources()
    {
        if (StartEvent == null)
        {
            StartClient(cancellationTokenSource.Token);
        }
        StartEvent.WaitOne();
        return _discoveredResources.Values;
    }

    private void StartClient(CancellationToken cancellationToken)
    {
        log.Info("Starting KCCS client...");

        StartEvent = new ManualResetEvent(false);
        _discoveredResources = new Dictionary<string, DiscoveredResource>();

        var factory = new Func<ClientWebSocket>(() =>
        {
            var client = new ClientWebSocket
            {
                Options =
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(5),
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => {
                        log.Debug($"Sender: {sender}\r\nCertificate:\r\n{certificate}\r\nChain:\r\n{chain}\r\nsslPolicyErrors:\r\n{sslPolicyErrors}");
                        return true;
                    },
                    Credentials=new NetworkCredential("user", "pass")
                }
            };
            return client;
        });

        var url = new Uri("wss://localhost:9290/ws");

        using (IWebsocketClient client = new WebsocketClient(url, null, factory))
        {
            client.Name = "KCCS";
            client.ReconnectTimeout = TimeSpan.FromSeconds(3600);
            client.ErrorReconnectTimeout = TimeSpan.FromSeconds(30);
            client.ReconnectionHappened.Subscribe(info =>
            {
                if (info.Type != ReconnectionType.Initial)
                    log.Debug("Websocket reconnected, type: {type}, url: {url}", info.Type, client.Url);
            });
            client.DisconnectionHappened.Subscribe(info =>
                log.Warning("Websocket disconnected, type: {type}", info.Type));

            client.MessageReceived.Subscribe(msg =>
            {
                log.Debug("Message received:\r\n{message}", msg.ToString());

                var objMsg = JsonNode.Parse(msg.ToString());
                if (objMsg["Error"] is not null || objMsg["Error"]?.ToString() != "")
                {
                    log.Error("Error: {error}", objMsg["Error"].ToString());
                }
                if (objMsg["AllInstruments"] is not null)
                {
                    // "{\"AllInstruments\":[{\"Addresses\":[{\"Aliases\":[\"USBInstrument1\"],\"FavoriteConnection\":true,\"Id\":\"84b56221-1fb3-4569-b36b-4f16ee3771a9\",\"SiclAddress\":\"usb0[2391::4161::00000050::0]\",\"StaticallyDefined\":true,\"Status\":\"Failed\",\"VisaAddress\":\"USB0::0x0957::0x1041::00000050::0::INSTR\"}],\"Firmware\":\"4.0\",\"Id\":\"keysight,duthub,00000050\",\"InstrumentType\":\"Instrument\",\"InterfaceType\":\"Usb\",\"Manufacturer\":\"Keysight Technologies\",\"Model\":\"DutHub\",\"ParentId\":\"df8c228b-7f95-4e69-854f-829d5caf6984\",\"ParentVisaId\":\"USB0\",\"SecurityStatus\":\"None\",\"SerialNumber\":\"00000050\"}],\"Error\":\"\",\"MessageNum\":2,\"RequestID\":\"1\"}"
                    foreach (var instrument in objMsg["AllInstruments"].AsArray())
                    {
                        string id = instrument["Id"]?.ToString();
                        string manufacturer = instrument["Manufacturer"]?.ToString();
                        string model = instrument["Model"]?.ToString();
                        string serialNumber = instrument["SerialNumber"]?.ToString();
                        string firmware = instrument["Firmware"]?.ToString();
                        string instrumentType = instrument["InstrumentType"]?.ToString();
                        string interfaceType = instrument["InterfaceType"]?.ToString();
                        string securityStatus = instrument["SecurityStatus"]?.ToString();
                        string parentId = instrument["ParentId"]?.ToString();
                        string parentVisaId = instrument["ParentVisaId"]?.ToString();
                        log.Info("Instrument: {model},{firmware},{serialNumber}", model, firmware, serialNumber);
                        _discoveredResources[serialNumber] = new KccsDiscoveredResource()
                        {
                            Model = model,
                            Identifier = serialNumber,
                            FirmwareVersion = firmware,
                            Manufacturer = manufacturer,
                            InterfaceType = interfaceType,
                            VisaAddress = instrument["Addresses"]?.AsArray().Select(a => a["VisaAddress"]?.ToString()).ToArray(),
                            VisaAliases = instrument["Addresses"]?.AsArray().SelectMany(a => a["Aliases"]?.AsArray().Select(b => b.ToString()).ToArray()).ToArray()
                        };
                    }
                }
                if (objMsg["StartData"] is not null)
                {
                    // "{\"MessageNum\":1,\"RequestID\":\"0\",\"Error\":\"\",\"Message\":\"notifications enabled: InstrumentsChanges\",\"StartData\":{\"Versions\":[\"1\"]}}"
                    StartEvent.Set();
                }
                if (objMsg["NotificationData"] is not null)
                {
                    // Example message:
                    // {
                    //   "MessageNum": 2,
                    //   "RequestID": "007",
                    //   "Error": "",
                    //   "NotificationData": {
                    //       "Topic": "InstrumentsChanges",
                    //       "Instruments": [
                    //       {
                    //         "Id": "KEYSIGHT TECHNOLOGIES,SCPI Regression Test,123",
                    //         "Manufacturer": "KEYSIGHT TECHNOLOGIES",
                    //         "Model": "N3410A",
                    //         "SerialNumber": "123",
                    //         "Firmware": "\u003cUnknown Revision\u003e",
                    //         "InstrumentType": "Instrument",
                    //         "InterfaceType": "Lan",
                    //         "ParentId": "bc3463c3-82b6-4688-9d09-94bc1a3dc14d",
                    //         "ParentVisaId": "TCPIP0",
                    //         "Addresses": [
                    //           {
                    //             "Id": "9891879d-ad48-450b-bf08-17274409daa7",
                    //             "ElementType": "Instrument",
                    //             "FailedReason": "",
                    //             "StaticallyDefined": true,
                    //             "FavoriteConnection": true,
                    //             "SiclAddress": "lan,4880;hislip[10.74.2.100]:hislip0",
                    //             "VisaAddress": "TCPIP0::10.74.2.100::hislip0::INSTR",
                    //             "Status": "Verified",
                    //             "Aliases": [ "HislipInstrument1"],
                    //             "Action": "Added"
                    //           }
                    //         ]
                    //       }
                    //     ]
                    //   }
                    // } 
                    if (objMsg["NotificationData"]["Topic"]?.ToString() == "InstrumentsChanges")
                    {
                        foreach (var instrument in objMsg["NotificationData"]["Instruments"].AsArray())
                        {
                            string id = instrument["Id"]?.ToString();
                            string manufacturer = instrument["Manufacturer"]?.ToString();
                            string model = instrument["Model"]?.ToString();
                            string serialNumber = instrument["SerialNumber"]?.ToString();
                            string firmware = instrument["Firmware"]?.ToString();
                            string instrumentType = instrument["InstrumentType"]?.ToString();
                            string interfaceType = instrument["InterfaceType"]?.ToString();
                            string parentId = instrument["ParentId"]?.ToString();
                            string parentVisaId = instrument["ParentVisaId"]?.ToString();
                            log.Info("Instrument: {model},{firmware},{serialNumber}", model, firmware, serialNumber);
                            _discoveredResources[serialNumber] = new KccsDiscoveredResource()
                            {
                                Model = model,
                                Identifier = serialNumber,
                                FirmwareVersion = firmware,
                                Manufacturer = manufacturer,
                                InterfaceType = interfaceType,
                                VisaAddress = instrument["Addresses"]?.AsArray().Select(a => a["VisaAddress"]?.ToString()).ToArray(),
                                VisaAliases = instrument["Addresses"]?.AsArray().SelectMany(a => a["Aliases"]?.AsArray().Select(b => b.ToString()).ToArray()).ToArray()
                            };
                        }
                    }
                }

                //TODO: Handle Change messages
            });

            log.Info("Starting...");
            client.Start().Wait();
            log.Info("Started.");

            Task.Run(() => StartDataMessage(client));

            // make sure StartData is sent before any other messages are sent
            StartEvent.WaitOne();

            // get all instruments once, updates will arrive via notifications after this
            Task.Run(() => GetInstrumentInfo(client));

            cancellationToken.WaitHandle.WaitOne();
        }
    }

    private static async Task StartDataMessage(IWebsocketClient client)
    {
        while (true)
        {
            await Task.Delay(1000);

            if (!client.IsRunning)
                continue;

            string startDataMessage = $@"
{{""Request"": ""StartData"",
  ""RequestId"": ""{requestId}"",
  ""MessageNum"": ""1"",
  ""StartData"": {{
  ""Topic"": [
      ""InstrumentsChanges""
    ]
  }}
}}";

            client.Send(startDataMessage);
            requestId++;
            break;
        }
    }

    private static async Task GetInstrumentInfo(IWebsocketClient client)
    {
        while (true)
        {
            await Task.Delay(1000);

            if (!client.IsRunning)
                continue;

            string allInstrumentsMessage = $@"
{{
  ""Request"": ""AllInstruments"",
  ""RequestID"": ""{requestId}""
}}";
            client.Send(allInstrumentsMessage);
            requestId++;
            break;
        }
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        StartEvent?.Dispose();
    }
}
