using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnSYS_IoT
{
    internal class Net2MqttMessage
    {
        public string CMD { get; set; }
        public string IP { get; set; }
        public int Port { get; set; }
        public string Payload { get; set; }
        public Net2MqttMessage(string command, string IPaddress, int portNum, string payload)
        {
            this.CMD = command;
            this.IP = IPaddress;
            this.Port = portNum;
            this.Payload = payload;
        }           
    }
}
