using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetGent_F
{
    public class TcpInfoF
    {
        public TcpListener Listener { get; set; }
        public TcpClient Client { get; set; }
        public NetworkStream Stream { get; set; }
        public bool IsRun { get; set; }
        public string IP { get; set; }
        public int Port { get; set; }
        public TcpInfoF()
        {
            this.Listener = null;
            this.Client = null;
            this.Stream = null;
            this.IsRun = false;
            this.IP = string.Empty;
            this.Port = -1;
        }
    }

    internal class IoT_Settings
    {
        static internal string AzureHostName = "airgazerIoT.azure-devices.net";
        static internal string AzureEventHubEP = "sb://iothub-ns-airgazerio-24368083-52e3bbdbc1.servicebus.windows.net/";
        static internal string AzureEventHubPath = "airgazeriot";
        static internal string SASKey = "IoguqxF4OBoCUenJN1ZC5kOP2rnuo4lp9KeeyC49O64=";
        static internal string SASKeyName = "iothubowner";
    }

    internal class Utils
    {
        static internal (string ip, int port) ParseIPPort(string IPPort)
        {
            int portNum = 0;
            string IP = string.Empty;
            char[] delimiterChars = { ' ', ':', '\t' };
            string[] words = IPPort.Split(delimiterChars);

            if (words.Length == 2)
            {
                IP = words[0];
                if (int.TryParse(words[1], out portNum) == false)
                {
                    portNum = -1;
                }
            }
            return (IP, portNum);
        }
    }
}
