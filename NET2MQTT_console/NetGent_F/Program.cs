﻿// See https://aka.ms/new-console-template for more information
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

while (true)
{
#if false
    Console.ReadLine();

    //var response = await netagent_fleet.FleetTwin_GetFileSizeFromVessel("testVehicle01", "127.0.0.1:8080", @"\projects\SN2234\station\station.station");
    //var response = await netagent_fleet.FleetTwin_GetFileSizeFromVessel("testVehicle01", "222.99.136.234:80", @"\projects\SN2255\station\station.station");
    var response = await netagent_fleet.FleetTwin_GetFileSizeFromVessel("testVehicle01", "222.99.136.234:80", @"\projects\SN2255\SN2255.prj");
    if (response != null)
    {
        Console.WriteLine($"File Size = {response.ContentLength}");
    }
    else
    {
        Console.WriteLine("No Response");
    }

#endif

#if true
    Console.ReadLine();
    //var file_response = await netagent_fleet.FleetTwin_GetFileDataFromVessel("testVehicle01", "127.0.0.1:8080", @"\projects\SN2234\station\station.station");
    //var file_response = await netagent_fleet.FleetTwin_GetFileDataFromVessel("testVehicle01", "222.99.136.234:80", @"\projects\SN2255\station\station.station");
    var file_response = await netagent_fleet.FleetTwin_GetFileDataFromVessel("testVehicle01", "222.99.136.234:80", @"\projects\SN2255\SN2255.prj");

    if (file_response != null)
    {
        Console.WriteLine("Response");
        Console.WriteLine(Encoding.Default.GetString(file_response.Data));
    }
    else
    {
        Console.WriteLine("No Response");
    }
#endif
}


Console.ReadLine();

void FleetTwin_IoTMessageEvent(object sender, IoTFleet.IoTMessageEventArgs e)
{
    //Console.WriteLine($"\tF: {e.DeviceID} sent {e.Message}");
}

void FleetTwin_TcpRxEvent(object sender, Net2MqttMessage e)
{
    //Console.WriteLine($"F: Receive Data from IP {e.IP} PORT {e.Port}, PAYLOAD: {e.Payload} ");
}
