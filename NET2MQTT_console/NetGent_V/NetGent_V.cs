using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SnSYS_IoT;
using Newtonsoft.Json;
using Microsoft.Azure.Amqp.Framing;
using System.IO;
using System.Net.Http.Headers;
using Microsoft.Rest.TransientFaultHandling;
using log4net;
using Azure.Storage.Blobs.Models;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]

namespace NetGent_V
{
    internal class VesselNetAgent: IDisposable
    {
        const string AzureDeviceConnectionString = "HostName=airgazerIoT.azure-devices.net;DeviceId=testVehicle01;SharedAccessKey=gHsfleh0PJvQ2B6TQyKgzHXx/0s5NnsXxULJzmNMAro=";

        public delegate void TcpReceiveEventHandler(object sender, Net2MqttMessage e);
        public event TcpReceiveEventHandler? TcpReceiveEvent;

        private static readonly ILog log = LogManager.GetLogger(typeof(VesselNetAgent));
        private Dictionary<string, TcpInfoV> tcpList;
        private IoTVessel vesselTwin;
        private CancellationTokenSource taskTokenSrc;
        private bool isStarted;

        public VesselNetAgent()
        {
            this.isStarted = false;
            this.taskTokenSrc = null;
            this.tcpList = new Dictionary<string, TcpInfoV>();
            this.TcpReceiveEvent = null;

            log.Info($"*** Connect to Vessel ***");
            log.Info($"   ::{AzureDeviceConnectionString}");
            this.vesselTwin = new IoTVessel(AzureDeviceConnectionString);
            this.vesselTwin.IoTMessageEvent += VesselTwin_IoTMessageEvent;
        }

        public ILog GetLogIF { get { return log; } }
        public IoTVessel GetIoTVessel()
        {
            return this.vesselTwin;
        }

        public void Add_IoTMessageEvent(IoTVessel.IoTMessageEventHandler eventhandler)
        {
            this.vesselTwin.IoTMessageEvent += eventhandler;
        }

        public void Delete_IoTMessageEvent(IoTVessel.IoTMessageEventHandler eventhandler)
        {
            this.vesselTwin.IoTMessageEvent -= eventhandler;
        }

        /// <summary>
        /// Start Network Agent in Fleet Side
        /// </summary>
        public int Start()
        {
            int ret = 0;

            log.Info("*** NetGen_V Started ***");
            if (this.isStarted == false)
            {
                if (vesselTwin != null)
                {
                    ret = vesselTwin.Open();
                    this.isStarted = (ret == 1) ? true : false;

                    if (this.isStarted == true && this.taskTokenSrc == null)
                    {
                        this.taskTokenSrc = new CancellationTokenSource();
                    }
                }
                else
                {
                    log.Error("vesselTwin is null");
                }
            }
            else
            {
                log.Error("netGent_V is already run");
            }

            return ret;
        }

        /// <summary>
        /// Stop Metwork Agent in Fleet Side
        /// </summary>
        public int Stop()
        {
            int ret = 0;

            log.Info("*** NetGen_V stops ***");
            if (vesselTwin != null)
            {
                vesselTwin.Close();
                this.isStarted = false;
            }
            else
            {
                log.Error("vesselTwin is null");
            }

            if (this.taskTokenSrc != null)
            {
                this.taskTokenSrc.Cancel();
            }
            else
            {
                log.Error("token is null");
            }

            return ret;
        }

        /// <summary>
        /// Create TCP Client
        /// </summary>
        public int CreateTcpClient(string ip, int port)
        {
            int ret = 0;

            log.Info($"*** Create TCP Client, {ip}:{port} ***");
            string errorString = string.Empty;
            var tcpInfo = AddTcpClient(ip, port, 100);

            if (tcpInfo != null) {
                try {
                    // try to connect to the host
                    tcpInfo.Client.Connect(tcpInfo.IPServerEP);
                    tcpInfo.Stream = tcpInfo.Client.GetStream();
                    tcpInfo.IsRun = true;
                }
                catch (ArgumentNullException e) {
                    tcpInfo.IsRun = false;
                    errorString = e.Message;
                }
                catch (SocketException e) {
                    tcpInfo.IsRun = false;
                    errorString = e.Message;
                }
                finally {
                    if (tcpInfo.IsRun == true) {
                        var tcpRecieverTask = new Task(() => TcpClientReceiver(tcpInfo), taskTokenSrc.Token);
                        tcpRecieverTask.Start();
                        ret = 1;
                    }
                    else {
                        log.Error(errorString);
                    }
                }
            }
            else
            {
                log.Error("tcpInfo is null");
            }

            return ret;
        }

        /// <summary>
        /// Send data to TCP Server, Host
        /// </summary>
        public int SendMessage2Host(string ip, int port, byte[] data, int ndata)
        {
            int ret = 0;

            log.Info($"To {ip}:{port}, len[{ndata}]");
            if (data != null)
            {
                string byteString = string.Empty;
                foreach (var val in data)
                {
                    byteString = byteString + ' ' + val.ToString();
                }
            }
            if (string.IsNullOrEmpty(ip) == false && data != null && ndata > 0)
            {
                string IPPort = ip + ":" + port;
                
                if (tcpList.TryGetValue(IPPort, out var tcpInfo) == true)
                {
                    if (tcpInfo.IsRun == true)
                    {
                        tcpInfo.Stream.Write(data, 0, ndata);

                        ret = ndata;
                    }
                    else
                    {
                        log.Error($"Failed in sending message to {ip}:{port} because this socket is NOT activated");
                    }
                }
            }
            else
            {
                log.Error($"Failed in sending message to {ip}:{port} because data is NOT valid");
            }

            return ret;
        }

        public async Task<(HttpStatusCode, string)> SendHttpRequest(string serverUrl, string fileUrl, HttpMethod method, string request)
        {
            string response = string.Empty;
            HttpStatusCode statusCode = HttpStatusCode.BadRequest;

            string url = string.Format($@"http://{serverUrl}/{fileUrl}");
            log.Info($"Send HTTP Request, url = {url}");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "NET2MQTT/1.0");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var result = await client.SendAsync(new HttpRequestMessage(method, url));

                if (result != null)
                {
                    statusCode = result.StatusCode;                    
                    response = await result.Content.ReadAsStringAsync();
                }
            }

            log.Info($"   -> Status Code: {statusCode}");
            //log.Info($"Response: {response}");

            return (statusCode, response);
        }

        /// <summary>
        /// Kill TCP Client
        /// </summary>
        public int KillTcpClient(string ip, int port)
        {
            log.Info("*** Kill TCP Client ***");
            return RemoveTcpClient(ip, port);
        }

        public void Dispose()
        {
            this.Stop();
            if (this.taskTokenSrc != null)
            {
                this.taskTokenSrc.Dispose();
            }
        }

        /*
         * 
         */

        private async void TcpClientReceiver(TcpInfoV tcpInfo)
        {
            log.Info("*** Try to run TcpClientReceiver ***");
            if (tcpInfo != null && tcpInfo.Stream != null)
            {
                byte[] buffer;
                while (tcpInfo.IsRun == true)
                {
                    buffer = new byte[32 * 1024];
                    if (tcpInfo.Stream.DataAvailable == true)
                    {
                        try
                        {
                            //log.Info($"Waiting for data from {tcpInfo.IP}:{tcpInfo.Port}");
                            if (tcpInfo.Stream.DataAvailable == true)
                            {
                                var ndata = tcpInfo.Stream.Read(buffer, 0, buffer.Length);
                                if (ndata > 0)
                                {
                                    Array.Resize<byte>(ref buffer, ndata);
                                    log.Info($"From {tcpInfo.IP}:{tcpInfo.Port}:{tcpInfo.UID}, Len[{ndata}]");
                                    if (this.TcpReceiveEvent != null)
                                    {
                                        this.TcpReceiveEvent(this, new Net2MqttMessage(string.Empty, tcpInfo.IP, tcpInfo.Port, tcpInfo.UID, buffer, ndata));
                                    }
#if false
                                    var ret = await SendNet2MqttMessage(tcpInfo.IP, tcpInfo.Port,  buffer, ndata);
#else
                                    var ret = await SendNet2MqttMessage("127.0.0.1", tcpInfo.Port, tcpInfo.UID, buffer, ndata);
#endif
                                }
                            }
                        }
                        catch (IOException ex)
                        {
                            Console.WriteLine($"--> read error in {tcpInfo.IP}:{tcpInfo.Port}");
                            log.Error($"Exception({tcpInfo.IP}:{tcpInfo.Port}): {ex}");
                            //RemoveTcpClient(tcpInfo.IP, tcpInfo.Port);
                            //break;
                        }

                    }
                }
                log.Info("** TcpClientReceiver ends ***");
            }
            else
            {
                log.Error("Failed in running TcpClientReceiver");
            }
        }

        private TcpInfoV AddTcpClient(string IPaddress, int portNum, int uid)
        {
            TcpInfoV tcpInfo = null;

            if (string.IsNullOrEmpty(IPaddress) == false)
            {
                var IPPort = IPaddress + ":" + portNum + ":" + uid;
                tcpInfo = new TcpInfoV(IPaddress, portNum, uid);

                if (tcpList.TryAdd(IPPort, tcpInfo) == false)
                {
                    tcpList.TryGetValue(IPPort, out tcpInfo);
                }
            }

            return tcpInfo;
        }

        private int RemoveTcpClient(string IPaddress, int portNum)
        {
            int ret = 0;

            if (string.IsNullOrEmpty(IPaddress) == false)
            {
                var IPPort = IPaddress + ":" + portNum;

                if (tcpList.TryGetValue(IPPort, out var tcpInfo) == true)
                {
                    tcpInfo.Client.Close();
                    tcpInfo.IsRun = false;

                    ret = (tcpList.Remove(IPPort) == false) ? -1 : 1;
                }
            }

            return ret;
        }

        private async Task<int> SendNet2MqttMessage(string IP, int portNum, int uid, byte[] payload, int ndata)
        {
            int ret = 0;

            if (this.vesselTwin != null)
            {
                var netMsg = new Net2MqttMessage("NET", IP, portNum, uid, payload, ndata);
                ret = await this.vesselTwin.PutTelemetryAsync(JsonConvert.SerializeObject(netMsg));
            }

            return ret;
        }

        private async void VesselTwin_IoTMessageEvent(object sender, IoTVessel.IoTMessageEventArgs evt)
        {
            Task tcpRecieverTask;
            string errorString = string.Empty;
            var net2mqtt = JsonConvert.DeserializeObject<Net2MqttMessage>(evt.Message);

            if (net2mqtt != null) {
                TcpInfoV tcpInfo;

                string IPPort = string.Format($"{net2mqtt.IP}:{net2mqtt.Port}:{net2mqtt.UID}");
                log.Info($"IoT Message event is called, to {IPPort}");
                Console.WriteLine($"IoT Message event is called, to {IPPort}");

                if (tcpList.TryGetValue(IPPort, out tcpInfo) == false) {
                    log.Info($"Add new TCP info to the list, IPInfo = {net2mqtt.IP}:{net2mqtt.Port}");
                    tcpInfo = AddTcpClient(net2mqtt.IP, net2mqtt.Port, net2mqtt.UID);
                }

                if (tcpInfo.IsRun == false) { 
                    tcpInfo.Client = new TcpClient();
                    try {
                        log.Info($"Wait for connection to {tcpInfo.IP}:{tcpInfo.Port}");
                        Console.WriteLine($"Wait for connection to {tcpInfo.IP}:{tcpInfo.Port}");
                        await tcpInfo.Client.ConnectAsync(tcpInfo.IPServerEP);
                        Console.WriteLine($"Connected to {tcpInfo.IP}:{tcpInfo.Port}");
                        log.Info($"Connected to {tcpInfo.IP}:{tcpInfo.Port}");
                        tcpInfo.Stream = tcpInfo.Client.GetStream();
                        tcpInfo.IsRun = true;
                        errorString = string.Empty;
                    }
                    catch (ArgumentNullException ex) {
                        log.Error($"--- exception01: {IPPort}");
                        Console.WriteLine($"--- exception01: {IPPort}");
                        errorString = ex.Message;
                        //tcpInfo.IsRun = false;
                    }
                    catch (SocketException ex) {
                        log.Error($"--- exception02: {IPPort}");
                        Console.WriteLine($"--- exception02: {IPPort}");
                        errorString = ex.Message;
                        //tcpInfo.IsRun = false;
                    }
                    catch (Exception ex) {
                        log.Error($"--- exception03: {IPPort}");
                        Console.WriteLine($"--- exception03: {IPPort}");
                        errorString = ex.Message;
                        tcpInfo.IsRun = false;
                    }
                    finally {
                        if (tcpInfo.IsRun == true) {
                            // bring up the receiver task.
                            tcpRecieverTask = new Task(() => TcpClientReceiver(tcpInfo), taskTokenSrc.Token);
                            log.Info("Start TCP Client Receiver");
                            tcpRecieverTask.Start();
                        }
                        else {
                            log.Error($"IoTMessageEvent: {IPPort}");
                        }
                    }
                }

                if (tcpInfo.IsRun == true) {
                    try {
                        if (net2mqtt.Payload != null)
                        {
                            log.Info($"To {tcpInfo.IP}:{tcpInfo.Port}, Len[{net2mqtt.Length}]");
                            tcpInfo.Stream.Write(net2mqtt.Payload, 0, net2mqtt.Length);
                        }
                    }
                    catch (Exception ex) {
                        //tcpInfo.IsRun = false;
                        log.Error($"IoTMessageEvent_Write({tcpInfo.IP}:{tcpInfo.Port})\n{errorString}");
                    }
                }
            }
        }
    }
}
