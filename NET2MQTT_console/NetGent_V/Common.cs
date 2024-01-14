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

    internal class CopiedHttpResponse
    {
        public string FileURL { get; set; }
        public int StatusCode { get; set; }
        public string ContentType { get; set; }
        public int ContentLength { get; set; }
        public string LastModified { get; set; }
        public string AcceptRanges { get; set; }
        public string CacheControl { get; set; }
        public string ETag { get; set; }
        public string Date { get; set; }
        public string Connection { get; set; }
        public string KeepAlive { get; set; }
        public int Length { get; set; }
        public int Offset { get; set; }
        public byte[] Data { get; set; }
    }
    public class GetFileDataParam
    {
        public string url { get; set; }
        public string file { get; set; }
        public int offset { get; set; }
        public int length { get; set; }

        public GetFileDataParam(string url, string file, int offset, int length)
        {
            this.url = url;
            this.file = file;
            this.offset = offset;
            this.length = length;
        }
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
