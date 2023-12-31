// See https://aka.ms/new-console-template for more information
using NetGent_V;
using SnSYS_IoT;

Console.WriteLine("Hello, World! Start Vessel Network Agent Vessel");

var netagent_vessel = new VesselNetAgent();

netagent_vessel.Add_IoTMessageEvent(VesselTwin_IoTMessageEvent);
netagent_vessel.TcpReceiveEvent += VesselTwin_TcpRxEvent;
netagent_vessel.Start();

var netagent_methods = new VesselMethodCallback(netagent_vessel.GetIoTVessel());
int nMethods = netagent_methods.InstallMethodCallbacks();
Console.WriteLine($"number of methods: {nMethods}");

//netagent_vessel.CreateTcpClient("127.0.0.1", 13001);

Console.ReadLine();

void VesselTwin_IoTMessageEvent(object sender, IoTVessel.IoTMessageEventArgs e)
{
    Console.WriteLine($"\tV: The FLEET sent {e.Message}");
}

void VesselTwin_TcpRxEvent(object sender, Net2MqttMessage e)
{
    Console.WriteLine($"V: Receive Data from IP {e.IP} PORT {e.Port}, PAYLOAD: {e.Payload} ");
}
