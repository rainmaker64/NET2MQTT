﻿using System;
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

namespace NetGent_V
{
    internal class VesselNetAgent
    {
        const string AzureDeviceConnectionString = "HostName=airgazerIoT.azure-devices.net;DeviceId=testVehicle01;SharedAccessKey=gHsfleh0PJvQ2B6TQyKgzHXx/0s5NnsXxULJzmNMAro=";

        public delegate void TcpReceiveEventHandler(object sender, Net2MqttMessage e);
        public event TcpReceiveEventHandler? TcpReceiveEvent;

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

            this.vesselTwin = new IoTVessel(AzureDeviceConnectionString);
            this.vesselTwin.IoTMessageEvent += VesselTwin_IoTMessageEvent;
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
            }

            return ret;
        }

        /// <summary>
        /// Stop Metwork Agent in Fleet Side
        /// </summary>
        public int Stop()
        {
            int ret = 0;

            if (vesselTwin != null)
            {
                vesselTwin.Close();
                this.isStarted = false;
            }

            if (this.taskTokenSrc != null)
            {
                this.taskTokenSrc.Cancel();
            }

            return ret;
        }

        /// <summary>
        /// Create TCP Client
        /// </summary>
        public int CreateTcpClient(string ip, int port)
        {
            int ret = 0;
            string errorString = string.Empty;
            var tcpInfo = AddTcpClient(ip, port);

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
                        Console.WriteLine($"Error: {errorString}");
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Send data to TCP Server, Host
        /// </summary>
        public int SendMessage2Host(string ip, int port, byte[] data, int ndata)
        {
            int ret = 0;

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
                }

            }

            return ret;
        }

        /// <summary>
        /// Kill TCP Client
        /// </summary>
        public int KillTcpClient(string ip, int port)
        {
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
            if (tcpInfo != null && tcpInfo.Stream != null)
            {
                byte[] buffer = new byte[1024];
                while (tcpInfo.IsRun == true)
                {
                    try
                    {
                        var ndata = tcpInfo.Stream.Read(buffer, 0, buffer.Length);
                        var rxstring = System.Text.Encoding.ASCII.GetString(buffer, 0, ndata);
                        if (this.TcpReceiveEvent != null)
                        {
                            this.TcpReceiveEvent(this, new Net2MqttMessage(string.Empty, tcpInfo.IP, tcpInfo.Port, rxstring));
                        }

#if false
                        var ret = await SendNet2MqttMessage(tcpInfo.IP, tcpInfo.Port, rxstring);
#else
                        var ret = await SendNet2MqttMessage(tcpInfo.IP, 130000, rxstring);
#endif
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"Exception: {ex}");
                        RemoveTcpClient(tcpInfo.IP, tcpInfo.Port);
                        break;
                    }
                }
            }
            Console.WriteLine("TcpClientReceiver finished");
        }

        private TcpInfoV AddTcpClient(string IPaddress, int portNum)
        {
            TcpInfoV tcpInfo = null;

            if (string.IsNullOrEmpty(IPaddress) == false)
            {
                var IPPort = IPaddress + ":" + portNum;
                tcpInfo = new TcpInfoV(IPaddress, portNum);

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

        private async Task<int> SendNet2MqttMessage(string IP, int portNum, string payload)
        {
            int ret = 0;

            if (this.vesselTwin != null)
            {
                var netMsg = new Net2MqttMessage(string.Empty, IP, portNum, payload);
                ret = await this.vesselTwin.PutTelemetryAsync(JsonConvert.SerializeObject(netMsg));
            }

            return ret;
        }

        private async Task<int> SendNet2MqttMessage(string IP, int portNum, byte[] payload, int ndata)
        {
            int ret = 0;

            if (this.vesselTwin != null)
            {
                var netMsg = new Net2MqttMessage(string.Empty, IP, portNum, System.Text.Encoding.Default.GetString(payload));
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
#if true
                net2mqtt.Port = 13001;
#endif
                string IPPort = string.Format($"{net2mqtt.IP}:{net2mqtt.Port}");

                if (tcpList.TryGetValue(IPPort, out tcpInfo) == false) {
                    tcpInfo = AddTcpClient(net2mqtt.IP, net2mqtt.Port);
                }

                if (tcpInfo.IsRun == false) { 
                    tcpInfo.Client = new TcpClient();
                    try {
                        await tcpInfo.Client.ConnectAsync(tcpInfo.IPServerEP);
                        tcpInfo.Stream = tcpInfo.Client.GetStream();
                        tcpInfo.IsRun = true;
                        errorString = string.Empty;
                    }
                    catch (ArgumentNullException ex) {
                        errorString = ex.Message;
                        tcpInfo.IsRun = false;
                    }
                    catch (SocketException ex) {
                        errorString = ex.Message;
                        tcpInfo.IsRun = false;
                    }
                    catch (Exception ex) {
                        errorString = ex.Message;
                        tcpInfo.IsRun = false;
                    }
                    finally {
                        if (tcpInfo.IsRun == true) {
                            // bring up the receiver task.
                            tcpRecieverTask = new Task(() => TcpClientReceiver(tcpInfo), taskTokenSrc.Token);
                            tcpRecieverTask.Start();
                        }
                        else {
                            Console.WriteLine($"Error: {errorString}");
                        }
                    }
                }

                if (tcpInfo.IsRun == true) {
                    Byte[] bytesbuffer = System.Text.Encoding.ASCII.GetBytes(net2mqtt.Payload);
                    try {
                        tcpInfo.Stream.Write(bytesbuffer, 0, bytesbuffer.Length);
                    }
                    catch (Exception ex) {
                        tcpInfo.IsRun = false;
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }
            }
        }
    }
}
