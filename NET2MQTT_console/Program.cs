using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace NET2MQTT_console
{
    // string connectionString, string eventHubEP, string eventHubPath, string sasKey, string sasKeyName

    internal class Program
    {
        const string AzureConnectionString = "HostName=airgazerIoT.azure-devices.net;DeviceId=testVehicle01;SharedAccessKey=gHsfleh0PJvQ2B6TQyKgzHXx/0s5NnsXxULJzmNMAro=";
        const string AzureEventHubEP = "sb://iothub-ns-airgazerio-24368083-52e3bbdbc1.servicebus.windows.net/";
        const string AzureEventHubPath = "airgazeriot";
        const string SASKey = "IoguqxF4OBoCUenJN1ZC5kOP2rnuo4lp9KeeyC49O64=";
        const string SASKeyName = "iothubowner";

        static void Main(string[] args)
        {
            var fleetTwin = new IoTFleet(AzureConnectionString, AzureEventHubEP, AzureEventHubPath, SASKey, SASKeyName);
        }
    }
}
