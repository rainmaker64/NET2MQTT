// See https://aka.ms/new-console-template for more information
using NetGent_F;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SnSYS_IoT;
using System;
using System.Net;
using System.Text;

Console.WriteLine("Hello, World! Start Fleet Network Agent");

var netagent_fleet = new FleetNetAgent("testVehicle01");
netagent_fleet.Add_IoTMessageEvent(FleetTwin_IoTMessageEvent);
netagent_fleet.TcpReceiveEvent += FleetTwin_TcpRxEvent;
netagent_fleet.Start();
netagent_fleet.CreateTcpServer("127.0.0.1", 12501);
netagent_fleet.CreateTcpServer("127.0.0.1", 13001);

Console.ReadLine();

var jsonhead =
    @"{
        ""url"": ""127.0.0.1:8080"",
        ""file"": ""/projects/SN2234/Station/Station.station"",
    }";

var jObjectHead = JObject.Parse(jsonhead);
Console.WriteLine(jObjectHead.ToString());

var response = await netagent_fleet.FleetTwin_GetFileSizeFromVessel("testVehicle01", "127.0.0.1:8080", @"\projects\SN2234\station\station.station");
Console.WriteLine($"File Size = {response.ContentLength}");

Console.ReadLine();
#if true
var file_response = await netagent_fleet.FleetTwin_GetFileFromVessel("testVehicle01", "127.0.0.1:8080", @"\projects\SN2234\station\station.station");

if (file_response != null)
{
    var rxstring = System.Text.Encoding.ASCII.GetString(file_response.Data, 0, file_response.Data.Length);
    Console.WriteLine($"Content: \r\n {rxstring}");
    
}
#endif



Console.ReadLine();

void FleetTwin_IoTMessageEvent(object sender, IoTFleet.IoTMessageEventArgs e)
{
    Console.WriteLine($"\tF: {e.DeviceID} sent {e.Message}");
}

void FleetTwin_TcpRxEvent(object sender, Net2MqttMessage e)
{
    //Console.WriteLine($"F: Receive Data from IP {e.IP} PORT {e.Port}, PAYLOAD: {e.Payload} ");
}
