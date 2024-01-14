using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Azure;
using DotNetty.Common.Utilities;
using Microsoft.Azure.Amqp.Framing;
using Newtonsoft.Json;
using SnSYS_IoT;
using static Microsoft.Azure.Amqp.Serialization.SerializableType;
using static System.Net.Mime.MediaTypeNames;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using Microsoft.Azure.EventHubs;
using System.Collections.ObjectModel;
using Microsoft.Azure.Devices.Client;
using System.Collections.Specialized;

namespace NetGent_F
{
    /// <summary>
    /// Network Agent in Fleet Side
    /// Provide TCP Socket Client. It will send connection request to "GITOS" when it get the trigger from "GITOS".
    /// </summary>
    internal class FleetNetAgent: IDisposable
    {
        public delegate void TcpReceiveEventHandler(object sender, Net2MqttMessage e);
        public event TcpReceiveEventHandler? TcpReceiveEvent;

        private HttpListener httpListener;

        private string _vesselIoTName;
        private Dictionary<string, TcpInfoF> tcpList;
        private OrderedDictionary httpMsgList;
        private IoTFleet fleetTwin;
        private CancellationTokenSource taskTokenSrc;
        private bool isStarted;
        const string httpURL = "http://+:80/";

        const int MAX_RECEIVABLE_FILE_SIZE = 4 * 1024;

        public FleetNetAgent(string vesselIoTName)
        {
            this._vesselIoTName = vesselIoTName;
            this.isStarted = false;
            this.taskTokenSrc = new CancellationTokenSource();
            this.tcpList = new Dictionary<string, TcpInfoF>();
            this.TcpReceiveEvent = null;

            this.httpMsgList = new OrderedDictionary();

            var connectionString = string.Format($"HostName={IoT_Settings.AzureHostName};SharedAccessKeyName={IoT_Settings.SASKeyName};SharedAccessKey={IoT_Settings.SASKey}");

            this.httpListener = new HttpListener();

            Task httpServerTask = new Task(() => HttpListnerTask(this.httpListener, httpURL, taskTokenSrc.Token));
            httpServerTask.Start();

            fleetTwin = new IoTFleet(connectionString, IoT_Settings.AzureEventHubEP, IoT_Settings.AzureEventHubPath, IoT_Settings.SASKey, IoT_Settings.SASKeyName);
            fleetTwin.IoTMessageEvent += FleetTwin_IoTMessageEvent;
        }

        public void Add_IoTMessageEvent(IoTFleet.IoTMessageEventHandler eventhandler )
        {
            this.fleetTwin.IoTMessageEvent += eventhandler;
        }

        public void Delete_IoTMessageEvent(IoTFleet.IoTMessageEventHandler eventhandler)
        {
            this.fleetTwin.IoTMessageEvent -= eventhandler;
        }

        /// <summary>
        /// Start Network Agent in Fleet Side
        /// </summary>
        public int Start()
        {
            int ret = 0;

            if (this.isStarted == false)
            {
                if (fleetTwin != null)
                {
                    ret = fleetTwin.Connect2Cloud();
                    this.isStarted = (ret == 1) ? true : false;
                }
            }

            return ret;
        }

        /// <summary>
        /// Stop Metwork Agent in Fleet Side
        /// </summary>
        public int Stop()
        {
            int ret = 0;

            if (fleetTwin != null)
            {
                ret = fleetTwin.Disconnect2Cloud();
                this.isStarted = false;
            }

            if (this.taskTokenSrc != null)
            {
                this.taskTokenSrc.Cancel();
            }

            return ret;
        }

        public async Task<(int, string)> DirectCall2Vessel(string vesselName, string commandName, string payloadJson, double timeout = 30)
        {
            int ret = 0;
            string response;

            (ret, response) = await this.fleetTwin.Call2Vessel(vesselName, commandName, payloadJson, timeout);

            return (ret, response);
        }

        /// <summary>
        /// Create TCP Server
        /// </summary>
        public int CreateTcpServer(string ip, int port)
        {
            int ret = 0;

            var tcpinfo = AddTcpListener(ip, port);

            if (tcpinfo != null)
            {
                Task.Factory.StartNew(() =>
                {
                    RunTcpListener(tcpinfo, ip, port);
                }, taskTokenSrc.Token);
            }

            return ret;
        }

        /// <summary>
        /// Kill TCP Server
        /// </summary>
        public int KillTcpServer(string ip, int port)
        {
            return RemoveTcpListener(ip, port);
        }

        private void FleetTwin_IoTMessageEvent(object sender, IoTFleet.IoTMessageEventArgs e)
        {
            var net2mqtt = JsonConvert.DeserializeObject<Net2MqttMessage>(e.Message);

            if (net2mqtt != null)
            {
                switch (net2mqtt.CMD)
                {
                    case "NET":
                        {
                            if (tcpList.TryGetValue(string.Format($"{net2mqtt.IP}:{net2mqtt.Port}"), out var tcpInfo) == true)
                            {
                                try
                                {
                                    Console.WriteLine("F: Write data into Stream");
                                    tcpInfo.Stream.Write(net2mqtt.Payload, 0, net2mqtt.Length);
                                }
                                catch (Exception ex)
                                { }
                            }
                            else
                            {
                                Console.WriteLine("IPPort is not found...");
                            }

                            break;
                        }
                    case "HTTP_GET":
                        {
                            if (this.httpMsgList.Contains(net2mqtt.Port) == false)
                            {
                                string msgString = Encoding.Default.GetString(net2mqtt.Payload);
                                this.httpMsgList.Add(net2mqtt.Port, msgString);
                                Console.WriteLine($"GET: cnt = {net2mqtt.Port}, length = {net2mqtt.Length}, string length = {msgString.Length}");
                                if (net2mqtt.Port == 0)
                                {
                                    using (StreamWriter outputFile = new StreamWriter("1st_f.xml"))
                                    {
                                        outputFile.WriteLine(msgString);
                                    }
                                }

                            }
                            break;
                        }

                }
            }
            else
            {
                Console.WriteLine($"Invalid MQTT Data: {e.Message}");
            }
        }

        /*
         * 
         */
        private TcpInfoF AddTcpListener(string IPaddress, int portNum)
        {
            TcpInfoF tcpInfo = null;

            if (string.IsNullOrEmpty(IPaddress) == false)
            {
                tcpInfo = new TcpInfoF() { IP = IPaddress, Port = portNum};
                tcpInfo.Listener = new TcpListener(IPAddress.Parse(IPaddress), portNum);

                if (tcpList.TryAdd(IPaddress + ":" + portNum, tcpInfo) == false)
                {
                    tcpList.TryGetValue(IPaddress + ":" + portNum, out tcpInfo);
                }
            }

            return tcpInfo;
        }

        private int RemoveTcpListener(string IPaddress, int portNum)
        {
            int ret = 0;

            if (string.IsNullOrEmpty(IPaddress) == false)
            {
                var IPPort = IPaddress + ":" + portNum;

                if (tcpList.TryGetValue(IPPort, out var tcpInfo) == true)
                {
                    if (tcpInfo != null && tcpInfo.IsRun == true)
                    {
                        tcpInfo.Listener.Stop();
                        tcpInfo.Client.Close();
                    }

                    ret = tcpList.Remove(IPPort) == true ? 1 : -1;
                }
            }

            return ret;
        }

        private void RunTcpListener(TcpInfoF tcpInfo, string IPaddress, int portNumber)
        {
            RunTcpListener(tcpInfo);
        }

        private void RunTcpListener(TcpInfoF tcpInfo)
        {
            if (tcpInfo != null && tcpInfo.Listener != null)
            {
                Task tcpRecieverTask;

                while (true)
                {
                    try
                    {
                        tcpInfo.Listener.Start();
                        Console.WriteLine($"F: Waiting for connection - {tcpInfo.Port}");
                        tcpInfo.Client = tcpInfo.Listener.AcceptTcpClient();
                        Console.WriteLine("\tF: Connected");
                        tcpInfo.Stream = tcpInfo.Client.GetStream();
                        tcpInfo.IsRun = true;

                        tcpRecieverTask = new Task(() => TcpClientReceiver(tcpInfo), taskTokenSrc.Token);
                        tcpRecieverTask.Start();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception = {ex.Message}");
                        tcpInfo.IsRun = false;
                        break;
                    }
                }
            }
        }

        private async void TcpClientReceiver(TcpInfoF tcpInfo)
        {
            if (tcpInfo != null && tcpInfo.IsRun == true && tcpInfo.Stream != null)
            {
                int ndata;
                byte[] buffer = new byte[1024];

                while (tcpInfo.IsRun == true)
                {
                    ndata = 0;
                    try
                    {
                        ndata = tcpInfo.Stream.Read(buffer, 0, buffer.Length);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"Exception = {ex}");
                        tcpInfo.IsRun = false;
                        break;
                    }

                    var rxstring = System.Text.Encoding.Default.GetString(buffer, 0, ndata);
                    if ( ndata > 0 && string.IsNullOrEmpty(rxstring) == false)
                    {
                        // Check if it is http response
                        int ret = await SendNet2MqttMessage(tcpInfo.IP, tcpInfo.Port, buffer, ndata);

                        if (this.TcpReceiveEvent != null)
                        {
                            this.TcpReceiveEvent(this, new Net2MqttMessage(string.Empty, tcpInfo.IP, tcpInfo.Port, buffer, ndata));
                        }
                    }
                }
            }
        }

        private async void HttpListnerTask(HttpListener httpServer, string httpurl, CancellationToken ct)
        {
            if (httpServer != null && string.IsNullOrEmpty(httpurl) == false)
            {
                httpServer.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                httpServer.Prefixes.Add(httpURL);
                httpServer.Start();

                while (true)
                {
                    HttpListenerContext ctx = await httpServer.GetContextAsync();

                    var response = await HandleHttpRequest(ctx);

                    if (ct.IsCancellationRequested == true)
                    {
                        httpServer.Close();
                        break;
                    }
                }
            }

        }

        private async Task<HttpListenerResponse> HandleHttpRequest(HttpListenerContext httpctx)
        {
            HttpListenerRequest req;
            HttpListenerResponse resp = null;

            if (httpctx != null && (req = httpctx.Request) != null)
            {
                resp = httpctx.Response;
                /// Call IoT Direct Call to get the data from the remote HTTP Server.
                switch (req.HttpMethod)
                {
                    case "HEAD":
                        {
                            Console.WriteLine($"HEAD Command");
#if false   // for local server test
                            var response = await FleetTwin_GetFileSizeFromVessel("testVehicle01", "127.0.0.1:80", req.RawUrl);
#else
                            var response = await FleetTwin_GetFileSizeFromVessel("testVehicle01", "222.99.136.234:80", req.RawUrl);
#endif
                            if (response != null)
                            {
                                resp.ContentLength64 = response.ContentLength;
                                resp.StatusCode = response.StatusCode;
                                resp.AddHeader("accept-ranges", response.AcceptRanges);
                                resp.AddHeader("cache-control", response.CacheControl);
                                resp.AddHeader("last-modified", response.LastModified);
                                resp.AddHeader("etag", response.ETag);
                                resp.ContentType = response.ContentType;
                                resp.AddHeader("Connection", response.Connection);
                                resp.AddHeader("Keep-Alive", response.KeepAlive);
                            }
                            else
                            {
                                resp.StatusCode = 400;  // Bad Request
                                resp.ContentLength64 = 0;
                            }
                            resp.Close();
                            break;
                        }
                    case "GET":
                        {
                            Console.WriteLine($"GET Command");
#if false // For Local Server Test
                            var response = await FleetTwin_GetFileFromVessel("testVehicle01", "127.0.0.1:80", req.RawUrl);
#else
                            var response = await FleetTwin_GetFileDataFromVessel("testVehicle01", "222.99.136.234:80", req.RawUrl);
#endif
                            // Read the source file into a byte array.
                            if (response != null)
                            {
                                resp.StatusCode = response.StatusCode;
                                resp.ContentLength64 = response.ContentLength;
                                resp.ContentType = response.ContentType;
                                resp.AddHeader("accept-ranges", response.AcceptRanges);
                                resp.AddHeader("cache-control", response.CacheControl);
                                resp.AddHeader("last-modified", response.LastModified);
                                resp.AddHeader("etag", response.ETag);
                                resp.AddHeader("Connection", response.Connection);
                                resp.AddHeader("Keep-Alive", response.KeepAlive);

                                await resp.OutputStream.WriteAsync(response.Data, 0, response.Data.Length);
                            }
                            else
                            {
                                resp.StatusCode = 400;  // Bad Request
                                resp.ContentLength64 = 0;
                            }
                            resp.Close();
                            break;
                        }
                }
            }

            return resp;
        }

        public void Dispose()
        {
            this.Stop();
            if (this.taskTokenSrc != null)
            {
                this.taskTokenSrc.Dispose();
            }
        }

        private async Task<int> SendNet2MqttMessage(string IP, int portNum, byte[] payload, int length)
        {
            int ret = 0;

            if (fleetTwin != null)
            {
                var netMsg = new Net2MqttMessage(string.Empty, IP, portNum, payload, length);
                ret = await fleetTwin.SendTelemetry2Vessel(this._vesselIoTName, JsonConvert.SerializeObject(netMsg));
            }

            return ret;
        }

        #region Direct2Call
        private async Task<HttpHeadResponse> FleetTwin_GetFileSizeFromVessel(string vesselname, string httpurl, string fileurl)
        {
            HttpHeadResponse headResponse = null;
            var param = new GetFileSizeParam(httpurl, fileurl);

            string jsonparam = JsonConvert.SerializeObject(param);

            (int retHead, string responseHead) = await DirectCall2Vessel(vesselname, "DC_HEAD", jsonparam);

            if (string.IsNullOrEmpty(responseHead) == false)
            {
                headResponse = JsonConvert.DeserializeObject<HttpHeadResponse>(responseHead);
            }

            return headResponse;
        }

        internal async Task<HttpGetResponse> FleetTwin_GetFileDataFromVessel(string vesselname, string httpurl, string fileurl)
        {
            var param = new GetFileDataParam(httpurl, fileurl, 0, 0);

            HttpGetResponse getResponse = null;
            this.httpMsgList.Clear();

            string jsonparam = JsonConvert.SerializeObject(param);

            // Direct Call in AZURE
            (int retGet, string responseGet) = await DirectCall2Vessel(vesselname, "DC_GET", jsonparam);

            if (retGet == 200 && string.IsNullOrEmpty(responseGet) == false)
            {
                getResponse = JsonConvert.DeserializeObject<HttpGetResponse>(responseGet);
                var msgEmulator = this.httpMsgList.GetEnumerator();
                if (msgEmulator != null)
                {
                    string fullmessage = string.Empty;
                    // Gather all messages from a vessel.
                    while (msgEmulator.MoveNext())
                    {
                        fullmessage = fullmessage + msgEmulator.Value;
                    }
                    getResponse.Data = System.Text.Encoding.UTF8.GetBytes(fullmessage);
                }
            }

            return getResponse;
        }
        #endregion
    }
}
