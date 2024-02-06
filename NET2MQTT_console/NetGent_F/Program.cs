// See https://aka.ms/new-console-template for more information
using Microsoft.Azure.Amqp.Framing;
using NetGent_F;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SnSYS_IoT;
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

string Local_IP = "127.0.0.1";
int Local_Port = 13001;
string Vessel_IP = "222.99.136.234";
int Vessel_Port = 12501;

Console.WriteLine("Hello, World! Start Fleet Network Agent");

var netagent_fleet = new FleetNetAgent("testVehicle01");
netagent_fleet.Add_IoTMessageEvent(FleetTwin_IoTMessageEvent);
netagent_fleet.TcpReceiveEvent += FleetTwin_TcpRxEvent;
netagent_fleet.Start();
netagent_fleet.CreateTcpServer("127.0.0.1", 12501);
netagent_fleet.CreateTcpServer("127.0.0.1", 13001);

#if false
{

    TcpClient fleetSocketClient;
    TcpClient fleetSocketClient1;

    IPAddress vesselIP = IPAddress.Parse(Vessel_IP);
    var vesselIPEP = new IPEndPoint(vesselIP, Vessel_Port);
    var vesselSocketClient = new TcpClient();
    var vesselSocketClient1 = new TcpClient();


    // Connection to Vessel
    try
    {
        vesselSocketClient.Connect(vesselIPEP);
        Console.WriteLine("connnected...");
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
        vesselSocketClient = null;
    }

    try
    {
        vesselSocketClient1.Connect(vesselIPEP);
        Console.WriteLine("connnected2...");
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
        vesselSocketClient1 = null;
    }
}

#endif

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

void TcpClientFunc(NetworkStream fClientStream, NetworkStream vClientStream)
{
    byte[] fSocketBuffer = new byte[1024];
    byte[] vSocketBuffer = new byte[1024];
    int fSocketDataLen = 0;
    int vSocketDataLen = 0;

    if (fClientStream != null && vClientStream != null)
    {
        while (true)
        {
            if (fClientStream.DataAvailable == true)
            {
                try
                {
                    fSocketDataLen = fClientStream.Read(fSocketBuffer, 0, fSocketBuffer.Length);
                    if (fSocketDataLen > 0)
                    {
                        Console.WriteLine($"[1] Write {fSocketDataLen} data to Vessel");
                        try
                        {
                            vClientStream.Write(fSocketBuffer, 0, fSocketDataLen);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[1] Error in Writing to Vessel");
                            Console.WriteLine("[1]:" + ex.Message);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[1] Error in Reading from Fleet");
                    Console.WriteLine("[1]:" + ex.Message);
                    break;
                }
            }

            if (vClientStream.DataAvailable == true)
            {
                try
                {
                    vSocketDataLen = vClientStream.Read(vSocketBuffer, 0, vSocketBuffer.Length);
                    if (vSocketDataLen > 0)
                    {
                        Console.WriteLine($"[1] Write {vSocketDataLen} data to Vessel");
                        try
                        {
                            fClientStream.Write(vSocketBuffer, 0, vSocketDataLen);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[1] Error in Writing to Fleet");
                            Console.WriteLine("[1]:" + ex.Message);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[1] Error in Reading from Vessel");
                    Console.WriteLine("[1]:" + ex.Message);
                    break;
                }
            }
        }
    }
}