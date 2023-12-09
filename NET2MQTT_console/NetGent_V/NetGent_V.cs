using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SnSYS_IoT;

namespace NetGent_V
{
    internal class NetGent_V
    {
        const string AzureHostName = "airgazerIoT.azure-devices.net";
        const string AzureEventHubEP = "sb://iothub-ns-airgazerio-24368083-52e3bbdbc1.servicebus.windows.net/";
        const string AzureEventHubPath = "airgazeriot";

        const string SASKey = "IoguqxF4OBoCUenJN1ZC5kOP2rnuo4lp9KeeyC49O64=";
        const string SASKeyName = "iothubowner";

        const string AzureDeviceConnectionString = "HostName=airgazerIoT.azure-devices.net;DeviceId=testVehicle01;SharedAccessKey=gHsfleh0PJvQ2B6TQyKgzHXx/0s5NnsXxULJzmNMAro=";

        private Dictionary<string, Socket?> tcpServerList;
        private IoTVessel? vesselTwin;
        private CancellationTokenSource? taskTokenSrc;
        private bool isStarted;

        public NetGent_V()
        {
            this.isStarted = false;
            this.taskTokenSrc = null;
            this.tcpServerList = new Dictionary<string, Socket?>();

            var vesselTwin = new IoTVessel(AzureDeviceConnectionString);
            vesselTwin.IoTMessageEvent += VesselTwin_IoTMessageEvent;
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

            foreach (var item in this.tcpServerList)
            {
                var socket = item.Value;
                if (socket != null)
                {
                    socket.Close(1000);
                }
            }

            return ret;
        }

        /// <summary>
        /// Add TCP Client into a client list.
        /// </summary>
        public int AddTcpServer(string IPaddress, int portNum)
        {
            int ret = 0;

            if (string.IsNullOrEmpty(IPaddress) == false && this.isStarted == false)
            {
                var IPPort = IPaddress + ":" + portNum;
                var serversocket = CreateTcpSocketServers(IPaddress, portNum);

                ret = (tcpServerList.TryAdd(IPPort, serversocket) == false) ? -1 : 1;

                if (ret == 1 && serversocket != null && this.taskTokenSrc != null)
                {
                    Task.Factory.StartNew(() =>
                    {
                        Socket thisSocket = serversocket;
                        string thisIP = IPaddress;
                        int thisPort = portNum;
                        byte[] indata = new byte[1024];

                        while (true)
                        {
                            int nbytes = thisSocket.Receive(indata);

                            if (nbytes > 0)
                            {
                                int ret = SendNet2MqttMessage(thisIP, thisPort, indata, nbytes);
                            }
                        }
                    }, this.taskTokenSrc.Token);
                }
            }

            return ret;
        }


        /// <summary>
        /// Delete TCP Client from a client list
        /// </summary>
        public int DelTcpServer(string IPaddress, int portNum)
        {
            int ret = 0;

            if (string.IsNullOrEmpty(IPaddress) == false)
            {
                var IPPort = IPaddress + ":" + portNum;

                if (tcpServerList.TryGetValue(IPPort, out var serversocket) == true)
                {
                    if (serversocket != null)
                    {
                        serversocket.Close(1000);   // timeout = 1sec.
                    }
                    ret = (tcpServerList.Remove(IPPort) == false) ? -1 : 1;
                }
            }

            return ret;
        }

        private Socket? CreateTcpSocketServers(string ip, int port)
        {
            Socket? serverSocket = null;

            IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(ip), port);

            if (ipep != null)
            {
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    serverSocket.Connect(ipep);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: can't connect to the server");
                    serverSocket = null;
                }
            }

            return serverSocket;
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

        private void VesselTwin_IoTMessageEvent(object sender, IoTVessel.IoTMessageEventArgs e)
        {
            Console.WriteLine($"{e.MessageID} sent {e.Message}");
        }
    }
}
