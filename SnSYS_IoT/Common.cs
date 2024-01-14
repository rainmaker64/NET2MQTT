using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnSYS_IoT
{
    public class Net2MqttMessage
    {
        public string CMD { get; set; }
        public string IP { get; set; }
        public int Port { get; set; }
        public byte[] Payload { get; set; }
        public int Length { get; set; }
        public Net2MqttMessage(string command, string IPaddress, int portNum, byte[] payload, int payloadlength)
        {
            this.CMD = command;
            this.IP = IPaddress;
            this.Port = portNum;
            this.Payload = payload;
            this.Length = payloadlength;
        }           
    }
}
