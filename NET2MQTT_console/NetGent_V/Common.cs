using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetGent_V
{
    internal class TcpInfoV
    {
        public TcpClient Client { get; set; }
        public NetworkStream Stream { get; set; }
        public IPEndPoint IPServerEP { get; set; }
        public bool IsRun { get; set; }
        public string IP { get; set; }
        public int Port { get; set; }
        public TcpInfoV(string ip, int port)
        {
            this.Stream = null;
            this.IsRun = false;
            this.IP = ip;
            this.Port = port;

            IPAddress serverAddr = IPAddress.Parse(ip);
            this.IPServerEP = new IPEndPoint(serverAddr, port);
            this.Client = new TcpClient();
        }
    }

    internal class HttpHeadResponse
    {
        public string FileURL { get; set; }
        public int Length { get; set; }
    }

    internal class HttpGetResponse
    {
        public string FileURL { get; set; }
        public int TotalLength { get; set; }
        public string ContentType { get; set; }
        public DateTimeOffset LastModified { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
        public byte[] Data { get; set; }
    }


    internal class Utils
    {
        static public (string ip, int port) ParseIPPort(string IPPort)
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
