// See https://aka.ms/new-console-template for more information
using SnSYS_IoT;

Console.WriteLine("Hello, World!");

const string AzureHostName = "airgazerIoT.azure-devices.net";
const string AzureEventHubEP = "sb://iothub-ns-airgazerio-24368083-52e3bbdbc1.servicebus.windows.net/";
const string AzureEventHubPath = "airgazeriot";

const string SASKey = "IoguqxF4OBoCUenJN1ZC5kOP2rnuo4lp9KeeyC49O64=";
const string SASKeyName = "iothubowner";

const string AzureDeviceConnectionString = "HostName=airgazerIoT.azure-devices.net;DeviceId=testVehicle01;SharedAccessKey=gHsfleh0PJvQ2B6TQyKgzHXx/0s5NnsXxULJzmNMAro=";


var connectionString = string.Format($"HostName={AzureHostName};SharedAccessKeyName={SASKeyName};SharedAccessKey={SASKey}");

var fleetTwin = new IoTFleet(connectionString, AzureEventHubEP, AzureEventHubPath, SASKey, SASKeyName);
fleetTwin.IoTMessageEvent += FleetTwin_IoTMessageEvent;
int ret = fleetTwin.Connect2Cloud();
Console.WriteLine($"Fleet connected to Cloud = {ret}");

var vesselTwin = new IoTVessel(AzureDeviceConnectionString);
vesselTwin.IoTMessageEvent += VesselTwin_IoTMessageEvent;
ret = vesselTwin.Open();
Console.WriteLine($"Create Vessel Twin: {ret}");

if (ret > 0)
{
    int count = 0;
    Task.Run(async () => {
        while(true)
        {
            await vesselTwin.PutTelemetryAsync(String.Format($"Message #{count}"));
            //Console.WriteLine(String.Format($"Message #{count}"));

            Byte[] buffer = System.Text.Encoding.ASCII.GetBytes($"SOS{count}....");
            await fleetTwin.SendTelemetry2Vessel("testVehicle01", buffer);
            count++;
            Thread.Sleep(1000);
        }
    });
}
Console.ReadLine();

void FleetTwin_IoTMessageEvent(object sender, IoTFleet.IoTMessageEventArgs e)
{
    Console.WriteLine($"{e.DeviceID} sent {e.Message}");
}

void VesselTwin_IoTMessageEvent(object sender, IoTVessel.IoTMessageEventArgs e)
{
    Console.WriteLine($"A Fleet sent {e.Message}");
}