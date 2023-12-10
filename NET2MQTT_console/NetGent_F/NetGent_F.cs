using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp.Framing;
using SnSYS_IoT;

namespace NetGent_F
{
    public class TcpInfoF {
        public TcpListener Listener { get; set; }
        public TcpClient Client { get; set; }
        public NetworkStream Stream { get; set; }
        public bool IsRun { get; set; }
        public TcpInfoF() {
            this.Listener = null;
            this.Client = null;
            this.Stream = null;
            this.IsRun = false;
        }
    }

    /// <summary>
    /// Network Agent in Fleet Side
    /// Provide TCP Socket Client. It will send connection request to "GITOS" when it get the trigger from "GITOS".
    /// </summary>
    internal class NetGent_F: IDisposable
    {
        const string AzureHostName = "airgazerIoT.azure-devices.net";
        const string AzureEventHubEP = "sb://iothub-ns-airgazerio-24368083-52e3bbdbc1.servicebus.windows.net/";
        const string AzureEventHubPath = "airgazeriot";

        const string SASKey = "IoguqxF4OBoCUenJN1ZC5kOP2rnuo4lp9KeeyC49O64=";
        const string SASKeyName = "iothubowner";

        const string AzureDeviceConnectionString = "HostName=airgazerIoT.azure-devices.net;DeviceId=testVehicle01;SharedAccessKey=gHsfleh0PJvQ2B6TQyKgzHXx/0s5NnsXxULJzmNMAro=";

        private Dictionary<string, TcpInfoF> tcpList;
        private IoTFleet fleetTwin;
        private CancellationTokenSource taskTokenSrc;
        private bool isStarted;

        public NetGent_F()
        {
            this.isStarted = false;
            this.taskTokenSrc = new CancellationTokenSource();
            this.tcpList = new Dictionary<string, TcpInfoF>();

            var connectionString = string.Format($"HostName={AzureHostName};SharedAccessKeyName={SASKeyName};SharedAccessKey={SASKey}");

            fleetTwin = new IoTFleet(connectionString, AzureEventHubEP, AzureEventHubPath, SASKey, SASKeyName);
            fleetTwin.IoTMessageEvent += FleetTwin_IoTMessageEvent;
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

        private TcpInfoF AddTcpListener(string IPaddress, int portNum)
        {
            TcpInfoF tcpInfo = null;

            if (string.IsNullOrEmpty(IPaddress) == false)
            {
                tcpInfo = new TcpInfoF();
                var IPPort = IPaddress + ":" + portNum;
                IPAddress IP = IPAddress.Parse(IPaddress);
                tcpInfo.Listener = new TcpListener(IP, portNum);

                if (tcpList.TryAdd(IPPort, tcpInfo) == false)
                {
                    tcpList.TryGetValue(IPPort, out tcpInfo);
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
            RunTcpListener(tcpInfo,IPaddress + ":" + portNumber);
        }

        private void RunTcpListener(TcpInfoF tcpInfo, string IPPort)
        {
            if (tcpInfo != null && tcpInfo.Listener != null)
            {
                Task tcpRecieverTask;

                while (true)
                {
                    try
                    {
                        tcpInfo.Listener.Start();
                        Console.WriteLine("Waiting for connection...");
                        tcpInfo.Client = tcpInfo.Listener.AcceptTcpClient();
                        Console.WriteLine("Connected...");
                        tcpInfo.Stream = tcpInfo.Client.GetStream();
                        tcpInfo.IsRun = true;

                        tcpRecieverTask = new Task(() => TcpClientReceiver(tcpInfo.Stream, IPPort), taskTokenSrc.Token);
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

        private void TcpClientReceiver(NetworkStream stream, string IPPort)
        {
            if (stream != null)
            {
                byte[] buffer = new byte[1024];

                while (true)
                {
                    try
                    {
                        var ndata = stream.Read(buffer, 0, buffer.Length);
                        Console.WriteLine($"Number of Data = {ndata}");
                        var rxstring = System.Text.Encoding.ASCII.GetString(buffer, 0, ndata);
                        Console.WriteLine("Received: {0} from {1}", rxstring, IPPort);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"Exception: {ex}");
                        break;
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

        private int SendNet2MqttMessage(string IP, int portNum, byte[] payload, int ndata)
        {
            int ret = 0;

            return ret;
        }

        void FleetTwin_IoTMessageEvent(object sender, IoTFleet.IoTMessageEventArgs e)
        {
            Console.WriteLine($"{e.DeviceID} sent {e.Message}");
            // parsing the message
            // get IP and Port, payload
            // Does this number exist in a list.
            // if yes, call its sender with payload.
        }
    }
}
