using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SnSYS_IoT;

namespace NetGent_F
{
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

        private Dictionary<string, Socket?> tcpClientList;
        private IoTFleet fleetTwin;
        private CancellationTokenSource? taskTokenSrc;
        private bool isStarted;

        public NetGent_F()
        {
            this.isStarted = false;
            this.taskTokenSrc = null;
            this.tcpClientList = new Dictionary<string, Socket?>();

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

            if (fleetTwin != null)
            {
                ret = fleetTwin.Disconnect2Cloud();
                this.isStarted = false;
            }

            if (this.taskTokenSrc != null)
            {
                this.taskTokenSrc.Cancel();
            }

            foreach (var item in this.tcpClientList)
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
        public int AddTcpClient(string IPaddress, int portNum)
        {
            int ret = 0;

            if (string.IsNullOrEmpty(IPaddress) == false && this.isStarted == false)
            {
                var IPPort = IPaddress + ":" + portNum;
                var serversocket = CreateTcpSocketClients(IPaddress, portNum);

                ret = (tcpClientList.TryAdd(IPPort, serversocket) == false) ? -1 : 1;

                if (ret == 1 && serversocket != null && this.taskTokenSrc != null)
                {
                    Task.Factory.StartNew(() =>
                    {
                        Socket thisSocket = serversocket;
                        string thisIP = IPaddress;
                        int thisPort = portNum;
                        byte[] indata = new byte[1024];

                        while( true )
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
        public int DelTcpClient(string IPaddress, int portNum)
        {
            int ret = 0;

            if (string.IsNullOrEmpty(IPaddress) == false)
            {
                var IPPort = IPaddress + ":" + portNum;

                if (tcpClientList.TryGetValue(IPPort, out var serversocket) == true)
                {
                    if (serversocket != null)
                    {
                        serversocket.Close(1000);   // timeout = 1sec.
                    }
                    ret = (tcpClientList.Remove(IPPort) == false) ? -1 : 1;
                }
            }

            return ret;
        }

        private Socket? CreateTcpSocketClients(string ip, int port)
        {
            Socket? clientSocket = null;

            IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(ip), port);

            if (ipep != null)
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    clientSocket.Connect(ipep);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: can't connect to the server");
                    clientSocket = null;
                }
            }

            return clientSocket;
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
