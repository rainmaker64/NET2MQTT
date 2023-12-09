using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnSYS_IoT
{
    internal class IoTMessage
    {
        public string IP { get; set; }
        public int Port { get; set; }
        public string Payload { get; set; }
        public IoTMessage(string IPaddress, int portNum, string payload)
        {
            this.IP = IPaddress;
            this.Port = portNum;
            this.Payload = payload;
        }           
    }
}
