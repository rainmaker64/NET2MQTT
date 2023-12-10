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
    public class TcpInfoV
    {
        public TcpClient Client { get; set; }
        public NetworkStream Stream { get; set; }
        public IPEndPoint IPServerEP { get; set; }
        public bool IsRun { get; set; }
        public TcpInfoV(string ip, int port)
        {
            this.Stream = null;
            this.IsRun = false;

            IPAddress serverAddr = IPAddress.Parse(ip);
            this.IPServerEP = new IPEndPoint(serverAddr, port);
            this.Client = new TcpClient();
        }
    }

    internal class NetGent_V
    {
        const string AzureHostName = "airgazerIoT.azure-devices.net";
        const string AzureEventHubEP = "sb://iothub-ns-airgazerio-24368083-52e3bbdbc1.servicebus.windows.net/";
        const string AzureEventHubPath = "airgazeriot";

        const string SASKey = "IoguqxF4OBoCUenJN1ZC5kOP2rnuo4lp9KeeyC49O64=";
        const string SASKeyName = "iothubowner";

        const string AzureDeviceConnectionString = "HostName=airgazerIoT.azure-devices.net;DeviceId=testVehicle01;SharedAccessKey=gHsfleh0PJvQ2B6TQyKgzHXx/0s5NnsXxULJzmNMAro=";

        private Dictionary<string, TcpInfoV> tcpList;
        private IoTVessel vesselTwin;
        private CancellationTokenSource taskTokenSrc;
        private bool isStarted;

        public NetGent_V()
        {
            this.isStarted = false;
            this.taskTokenSrc = null;
            this.tcpList = new Dictionary<string, TcpInfoV>();

            this.vesselTwin = new IoTVessel(AzureDeviceConnectionString);
            this.vesselTwin.IoTMessageEvent += VesselTwin_IoTMessageEvent;
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
            Task tcpRecieverTask;
            var tcpInfo = AddTcpClient(ip, port);

            if (tcpInfo != null)
            {
                try
                {
                    // try to connect to the host
                    tcpInfo.Client.Connect(tcpInfo.IPServerEP);
                    tcpInfo.Stream = tcpInfo.Client.GetStream();

                    // bring up the receiver task.
                    tcpRecieverTask = new Task(() => TcpClientReceiver(tcpInfo.Stream, ip + ":" + port), taskTokenSrc.Token);
                    tcpRecieverTask.Start();

                }
                catch (ArgumentNullException e)
                {
                    Console.WriteLine("ArgumentNullException: {0}", e);
                }
                catch (SocketException e)
                {
                    Console.WriteLine("SocketException: {0}", e);
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
