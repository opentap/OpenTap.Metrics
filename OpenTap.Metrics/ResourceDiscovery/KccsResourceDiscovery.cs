using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenTap;
using System.Net.WebSockets;
using System.Net;
using System.Text.Json.Nodes;
using System.Text;

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
    private static ManualResetEvent Initialized = null;
    private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


    private static int requestId = 0;


    private Dictionary<string, DiscoveredResource> _discoveredResources;

    public IEnumerable<DiscoveredResource> DiscoverResources()
    {
        if (Initialized == null)
        {
            TapThread.Start(() => StartClient(cancellationTokenSource.Token));
        }
        Initialized.WaitOne();
        return _discoveredResources.Values;
    }

    private void StartClient(CancellationToken cancellationToken)
    {
        log.Info("Starting KCCS client...");

        while (cancellationToken.IsCancellationRequested == false)
        {
            try
            {
                StartClientInternal(cancellationToken);
            }
            catch (Exception ex)
            {
                log.Error("Error in KCCS client: {ex}", ex);
                TapThread.Sleep(TimeSpan.FromSeconds(5));
            }
        }
    }

    private void StartClientInternal(CancellationToken cancellationToken)
    {
        Initialized = new ManualResetEvent(false);
        _discoveredResources = new Dictionary<string, DiscoveredResource>();

        var client = new ClientWebSocket
        {
            Options =
                    {
                        KeepAliveInterval = TimeSpan.FromSeconds(5),
                        RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => {
                            // Log.Debug("Sender: {sender}\r\nCertificate:\r\n{certificate}\r\nChain:\r\n{chain}\r\nsslPolicyErrors:\r\n{sslPolicyErrors}",
                            //     sender, certificate, chain, sslPolicyErrors);
                            return true;
                        },
                        Credentials=new NetworkCredential("user", "pass")
                    }
        };

        var url = new Uri("wss://localhost:9290/ws");
        client.ConnectAsync(url, CancellationToken.None).Wait();

        // Send start message:
        // This creates a "session" on the KCCS server and subscribes to the "InstrumentsChanges" topic.
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
        client.SendAsync(new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes(startDataMessage)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
        log.Debug("Sent message: {startDataMessage}", startDataMessage);

        // Receive start message response:
        var receiveBuffer = new byte[16 * 1024];
        WebSocketReceiveResult receiveResult = client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None).Result;
        var receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, receiveResult.Count);
        log.Debug("Received message: {receivedMessage}", receivedMessage);

        // Send all instruments message:
        string allInstrumentsMessage = $@"
{{
  ""Request"": ""AllInstruments"",
  ""RequestID"": ""{requestId}""
}}";
        client.SendAsync(new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes(allInstrumentsMessage)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
        log.Debug("Sent message: {allInstrumentsMessage}", allInstrumentsMessage);

        while (cancellationToken.IsCancellationRequested == false)
        {
            receiveResult = client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cancellationToken).Result;
            receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, receiveResult.Count);
            log.Debug("Received message: {receivedMessage}", receivedMessage);
            processMessage(receivedMessage);
        }
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        Initialized?.Dispose();
    }

    private void processMessage(string receivedMessage)
    {
        var msg = JsonNode.Parse(receivedMessage);
        if (msg is null)
        {
            log.Warning("Received message is null");
            return;
        }
        if (msg["Error"] is not null && msg["Error"]?.ToString() != "")
        {
            log.Error("Error: {error}", msg["Error"].ToString());
        }
        if (msg["AllInstruments"] is not null)
        {
            // This is a response to a "AllInstruments" request.
            // Example message:
            // "{\"AllInstruments\":[{\"Addresses\":[{\"Aliases\":[\"USBInstrument1\"],\"FavoriteConnection\":true,\"Id\":\"84b56221-1fb3-4569-b36b-4f16ee3771a9\",\"SiclAddress\":\"usb0[2391::4161::00000050::0]\",\"StaticallyDefined\":true,\"Status\":\"Failed\",\"VisaAddress\":\"USB0::0x0957::0x1041::00000050::0::INSTR\"}],\"Firmware\":\"4.0\",\"Id\":\"keysight,duthub,00000050\",\"InstrumentType\":\"Instrument\",\"InterfaceType\":\"Usb\",\"Manufacturer\":\"Keysight Technologies\",\"Model\":\"DutHub\",\"ParentId\":\"df8c228b-7f95-4e69-854f-829d5caf6984\",\"ParentVisaId\":\"USB0\",\"SecurityStatus\":\"None\",\"SerialNumber\":\"00000050\"}],\"Error\":\"\",\"MessageNum\":2,\"RequestID\":\"1\"}"
            foreach (var instrument in msg["AllInstruments"].AsArray())
            {
                string model = instrument["Model"]?.ToString();
                string serialNumber = instrument["SerialNumber"]?.ToString();
                string firmware = instrument["Firmware"]?.ToString();
                log.Info("Instrument: {model},{firmware},{serialNumber}", model, firmware, serialNumber);
                _discoveredResources[serialNumber] = new KccsDiscoveredResource()
                {
                    Model = model,
                    Identifier = serialNumber,
                    FirmwareVersion = firmware,
                    Manufacturer = (instrument["Manufacturer"]?.ToString()),
                    InterfaceType = (instrument["InterfaceType"]?.ToString()),
                    VisaAddress = instrument["Addresses"]?.AsArray().Select(a => a["VisaAddress"]?.ToString()).ToArray(),
                    VisaAliases = instrument["Addresses"]?.AsArray().SelectMany(a => a["Aliases"]?.AsArray().Select(b => b.ToString()).ToArray()).ToArray()
                };
            }
            Initialized.Set();
        }
        if (msg["NotificationData"] is not null)
        {
            // This is a notification on our "InstrumentsChanges" subscription.
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
            if (msg["NotificationData"]["Topic"]?.ToString() == "InstrumentsChanges")
            {
                foreach (var instrument in msg["NotificationData"]["Instruments"].AsArray())
                {
                    string model = instrument["Model"]?.ToString();
                    string serialNumber = instrument["SerialNumber"]?.ToString();
                    string firmware = instrument["Firmware"]?.ToString();
                    log.Info("Instrument: {model},{firmware},{serialNumber}", model, firmware, serialNumber);
                    _discoveredResources[serialNumber] = new KccsDiscoveredResource()
                    {
                        Model = model,
                        Identifier = serialNumber,
                        FirmwareVersion = firmware,
                        Manufacturer = (instrument["Manufacturer"]?.ToString()),
                        InterfaceType = (instrument["InterfaceType"]?.ToString()),
                        VisaAddress = instrument["Addresses"]?.AsArray().Select(a => a["VisaAddress"]?.ToString()).ToArray(),
                        VisaAliases = instrument["Addresses"]?.AsArray().SelectMany(a => a["Aliases"]?.AsArray().Select(b => b.ToString()).ToArray()).ToArray()
                    };
                }
            }
        }
    }
}

