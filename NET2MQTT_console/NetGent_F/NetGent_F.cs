using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Azure.Amqp.Framing;
using Newtonsoft.Json;
using SnSYS_IoT;

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

        private string _vesselIoTName;
        private Dictionary<string, TcpInfoF> tcpList;
        private IoTFleet fleetTwin;
        private CancellationTokenSource taskTokenSrc;
        private bool isStarted;
        const int MAX_RECEIVABLE_FILE_SIZE = 4 * 1024;

        public FleetNetAgent(string vesselIoTName)
        {
            this._vesselIoTName = vesselIoTName;
            this.isStarted = false;
            this.taskTokenSrc = new CancellationTokenSource();
            this.tcpList = new Dictionary<string, TcpInfoF>();
            this.TcpReceiveEvent = null;

            var connectionString = string.Format($"HostName={IoT_Settings.AzureHostName};SharedAccessKeyName={IoT_Settings.SASKeyName};SharedAccessKey={IoT_Settings.SASKey}");

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

        public void FleetTwin_IoTMessageEvent(object sender, IoTFleet.IoTMessageEventArgs e)
        {
            var net2mqtt = JsonConvert.DeserializeObject<Net2MqttMessage>(e.Message);

            if (net2mqtt != null)
            {
                if (tcpList.TryGetValue(string.Format($"{net2mqtt.IP}:{net2mqtt.Port}"), out var tcpInfo) == true)
                {
                    Byte[] bytesbuffer = System.Text.Encoding.ASCII.GetBytes(net2mqtt.Payload);
                    try
                    {
                        Console.WriteLine("F: Write data into Stream");
                        tcpInfo.Stream.Write(bytesbuffer, 0, bytesbuffer.Length);
                    }
                    catch (Exception ex)
                    { }
                }
                else
                {
                    Console.WriteLine("IPPort is not found...");
                }
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
                        Console.WriteLine("F: Waiting for connection");
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
            int ret = 0;

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

                    var rxstring = System.Text.Encoding.ASCII.GetString(buffer, 0, ndata);
                    if ( ndata > 0 && string.IsNullOrEmpty(rxstring) == false)
                    {
                        // Check if it is http response
                        //if (tcpInfo.Port == 80 || tcpInfo.Port == 8080)
                        if (tcpInfo.Port == 80)
                        {
                            var lines = rxstring.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                            var httpCmd = HandleHttpCommand(lines);
                            if (httpCmd != null)
                            {
                                Console.WriteLine($"CMD: {httpCmd.Command}");
                                Console.WriteLine($"PARAM: {httpCmd.Param}");
                                Console.WriteLine($"HTTP VERSION: {httpCmd.HttpVersion}");
                                Console.WriteLine($"Connection: {httpCmd.Connection}");
                                Console.WriteLine($"Host: {httpCmd.Host}");

                                /// Call IoT Direct Call to get the data from the remote HTTP Server.
                                switch (httpCmd.Command)
                                {
                                    case "HEAD":
                                        {
                                            int filesize = await FleetTwin_GetFileSizeFromVessel("testVehicle01", "127.0.0.1:8080", httpCmd.Param);
                                            Console.WriteLine($"File Size = {filesize}");

                                            string httpResponse = "HTTP/1.1 200 OK\r\n";
                                            httpResponse += $"Content-Length: {filesize}\r\n";
                                            httpResponse += "Content-Type: application/json\r\n\r\n";

                                            Byte[] txbuffer = System.Text.Encoding.ASCII.GetBytes(httpResponse);
                                            tcpInfo.Stream.Write(txbuffer);

                                            break;

                                        }
#if true
                                    case "GET":
                                        {
                                            var memstream = await FleetTwin_GetFileFromVessel("testVehicle01", "127.0.0.1:8080", @"\projects\SN2234\station\station.station");

                                            if (memstream != null)
                                            {
                                                var getstring = System.Text.Encoding.ASCII.GetString(memstream.GetBuffer(), 0, memstream.GetBuffer().Length);
                                                Console.WriteLine(getstring);
                                                string httpResponse = "HTTP/1.1 200 OK\r\n";
                                                httpResponse += $"Content-Length: {memstream.GetBuffer().Length}\r\n";
                                                httpResponse += "Content-Type: application/json\r\n\r\n";
                                                httpResponse += "Keep-Alive: timeout=5";
                                                httpResponse += "\r\n";
                                                httpResponse += getstring;

                                                Byte[] txbuffer = System.Text.Encoding.ASCII.GetBytes(httpResponse);
                                                tcpInfo.Stream.Write(txbuffer);
                                            }
                                            break;

                                        }
#endif
                                }
                            }
                        }
                        else
                        {
                            ret = await SendNet2MqttMessage(tcpInfo.IP, tcpInfo.Port, rxstring);
                        }

                        if (this.TcpReceiveEvent != null)
                        {
                            this.TcpReceiveEvent(this, new Net2MqttMessage(string.Empty, tcpInfo.IP, tcpInfo.Port, rxstring));
                        }
                    }
                }
            }

            Console.WriteLine("TcpClientReceiver finished");
        }

        public void Dispose()
        {
            this.Stop();
            if (this.taskTokenSrc != null)
            {
                this.taskTokenSrc.Dispose();
            }
        }


        private async Task<int> SendNet2MqttMessage(string IP, int portNum, string payload)
        {
            int ret = 0;

            if (fleetTwin != null)
            {
                var netMsg = new Net2MqttMessage(string.Empty, IP, portNum, payload);
                ret = await fleetTwin.SendTelemetry2Vessel(this._vesselIoTName, JsonConvert.SerializeObject(netMsg));
            }

            return ret;
        }

        private async Task<int> SendNet2MqttMessage(string IP, int portNum, byte[] payload, int ndata)
        {
            int ret = 0;

            if (fleetTwin != null)
            {
                var netMsg = new Net2MqttMessage(string.Empty, IP, portNum, System.Text.Encoding.Default.GetString(payload));
                ret = await fleetTwin.SendTelemetry2Vessel(this._vesselIoTName, JsonConvert.SerializeObject(netMsg));

                Console.WriteLine($"A vessel send a message to the fleet: {ret}");
            }

            return ret;
        }

        public async Task<int> FleetTwin_GetFileSizeFromVessel(string vesselname, string httpurl, string fileurl)
        {
            int length = 0;

            var param = new GetFileSizeParam(httpurl, fileurl);

            string jsonparam = JsonConvert.SerializeObject(param);

            (int retHead, string responseHead) = await DirectCall2Vessel(vesselname, "GetFileSize", jsonparam);

            if (retHead == 200 && string.IsNullOrEmpty(responseHead) == false)
            {
                var headResponse = JsonConvert.DeserializeObject<HttpHeadResponse>(responseHead);
                if (headResponse != null)
                {
                    length = headResponse.Length;
                }
            }

            return length;
        }

        public async Task<MemoryStream> FleetTwin_GetFileFromVessel(string vesselname, string httpurl, string fileurl)
        {
            bool bloop = true;
            int curlength = 0;
            int totalsize = int.MaxValue;
            int len = (totalsize > MAX_RECEIVABLE_FILE_SIZE) ? MAX_RECEIVABLE_FILE_SIZE : totalsize;

            var param = new GetFileDataParam(httpurl, fileurl, curlength, len);

            MemoryStream memorystream = null;

            while (bloop == true)
            {
                string jsonparam = JsonConvert.SerializeObject(param);

                (int retGet, string responseGet) = await DirectCall2Vessel(vesselname, "GetFileData", jsonparam);

                if (retGet == 200 && string.IsNullOrEmpty(responseGet) == false)
                {
                    var getResponse = JsonConvert.DeserializeObject<HttpGetResponse>(responseGet);
                    if (getResponse != null)
                    {
                        // when getting the total length, allocate the memory size
                        if (memorystream == null)
                        {
                            memorystream = new MemoryStream(getResponse.TotalLength);
                        }
                        memorystream.Seek(curlength, SeekOrigin.Begin);
                        memorystream.Write(getResponse.Data, 0, getResponse.Length);

                        curlength = curlength + getResponse.Length;
                        param.offset = curlength;
                        param.length = ((getResponse.TotalLength - curlength) > MAX_RECEIVABLE_FILE_SIZE) ? MAX_RECEIVABLE_FILE_SIZE : (getResponse.TotalLength - curlength);

                        if (curlength >= getResponse.TotalLength)
                        {
                            bloop = false;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            return memorystream;
        }
#region HTTP
        private string[] ParseHttpResponse(string response)
        {
            if (string.IsNullOrEmpty(response) == false)
            {
                string[] lines = response.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    Console.WriteLine(line);
                }

                return lines;
            }
            else
            {
                return null;
            }
        }

        private HttpCommand HandleHttpCommand(string[] lines)
        {
            HttpCommand httpCmd = new HttpCommand();

            if (lines != null && lines.Length > 0)
            {
                foreach (var line in lines)
                {
                    Console.WriteLine(line);
                    string[] words = line.Split(new string[] { " ", "\t" }, StringSplitOptions.None);

                    if (words != null && words.Length > 0)
                    {
                        switch (words[0])
                        {
                            case "HEAD":
                            case "GET":
                                if (words.Length > 2)
                                {
                                    httpCmd.Command = words[0];
                                    httpCmd.Param = words[1];
                                    httpCmd.HttpVersion = words[2];
                                }
                                break;
                            case "host:":
                            case "Host:":
                                if (words.Length > 1)
                                {
                                    httpCmd.Host = words[1];
                                }
                                break;
                            case "Connection:":
                            case "connection:":
                                if (words.Length > 1)
                                {
                                    httpCmd.Connection = words[1];
                                }
                                break;
                        }
                    }
                }
            }
            return httpCmd;
        }
#endregion
    }
}
