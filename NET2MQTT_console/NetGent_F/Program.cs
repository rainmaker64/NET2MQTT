// See https://aka.ms/new-console-template for more information
using NetGent_F;
using SnSYS_IoT;

Console.WriteLine("Hello, World! Start Fleet Network Agent");

var netagent_fleet = new FleetNetAgent("testVehicle01");
netagent_fleet.Add_IoTMessageEvent(FleetTwin_IoTMessageEvent);
netagent_fleet.TcpReceiveEvent += FleetTwin_TcpRxEvent;
netagent_fleet.Start();
netagent_fleet.CreateTcpServer("127.0.0.1", 13000);

Console.ReadLine();


void FleetTwin_IoTMessageEvent(object sender, IoTFleet.IoTMessageEventArgs e)
{
    Console.WriteLine($"\tF: {e.DeviceID} sent {e.Message}");
}

void FleetTwin_TcpRxEvent(object sender, Net2MqttMessage e)
{
    Console.WriteLine($"F: Receive Data from IP {e.IP} PORT {e.Port}, PAYLOAD: {e.Payload} ");
}

