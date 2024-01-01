// See https://aka.ms/new-console-template for more information
using NetGent_F;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SnSYS_IoT;
using System;

Console.WriteLine("Hello, World! Start Fleet Network Agent");

var netagent_fleet = new FleetNetAgent("testVehicle01");
netagent_fleet.Add_IoTMessageEvent(FleetTwin_IoTMessageEvent);
netagent_fleet.TcpReceiveEvent += FleetTwin_TcpRxEvent;
netagent_fleet.Start();
//netagent_fleet.CreateTcpServer("127.0.0.1", 80);

Console.ReadLine();

var jsonhead =
    @"{
        ""url"": ""127.0.0.1"",
        ""file"": ""test.txt"",
    }";

var jObjectHead = JObject.Parse(jsonhead);
Console.WriteLine(jObjectHead.ToString());

(int retHead, string responseHead) = await netagent_fleet.DirectCall2Vessel("testVehicle01", "GetFileSize", jObjectHead.ToString());

if (string.IsNullOrEmpty(responseHead) == false)
{
    var headResponse = JsonConvert.DeserializeObject<HttpHeadResponse>(responseHead);
    if (headResponse != null)
    {
        Console.WriteLine("Response of HEAD");
        Console.WriteLine($"FILE: {headResponse.FileURL}");
        Console.WriteLine($"Length: {headResponse.Length}");
    }
}

Console.ReadLine();
var jsonget =
    @"{
        ""url"": ""127.0.0.1"",
        ""file"": ""test.txt"",
        ""offset"": 0,
        ""length"": 0
    }";
var jObjectGet = JObject.Parse(jsonget);
Console.WriteLine(jObjectGet.ToString());

(int retGet, string responseGet) = await netagent_fleet.DirectCall2Vessel("testVehicle01", "GetFileData", jObjectGet.ToString());

if (string.IsNullOrEmpty(responseGet) == false)
{
    var getResponse = JsonConvert.DeserializeObject<HttpGetResponse>(responseGet);
    if (getResponse != null)
    {
        Console.WriteLine("Response of GET");
        Console.WriteLine($"FILE: {getResponse.FileURL}");
        Console.WriteLine($"Total Length: {getResponse.TotalLength}");
        Console.WriteLine($"Offset: {getResponse.Offset}");
        Console.WriteLine($"Length: {getResponse.Length}");
        Console.WriteLine($"Type: {getResponse.ContentType}");
        Console.WriteLine($"LastModified: {getResponse.LastModified}");
        var rxstring = System.Text.Encoding.ASCII.GetString(getResponse.Data, 0, getResponse.Length);
        Console.WriteLine($"Content: \r\n {rxstring}");
    }
}



Console.ReadLine();


void FleetTwin_IoTMessageEvent(object sender, IoTFleet.IoTMessageEventArgs e)
{
    Console.WriteLine($"\tF: {e.DeviceID} sent {e.Message}");
}

void FleetTwin_TcpRxEvent(object sender, Net2MqttMessage e)
{
    Console.WriteLine($"F: Receive Data from IP {e.IP} PORT {e.Port}, PAYLOAD: {e.Payload} ");
}


