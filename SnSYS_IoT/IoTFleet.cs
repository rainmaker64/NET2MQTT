using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.EventHubs;
using System.Text;
using System.Threading;

namespace SnSYS_IoT
{
    public class IoTFleet
    {
        public string AZ_connectionString { get; set; }
        public string AZ_eventHubEP { get; set; }
        public string AZ_eventHubPath { get; set; }
        public string AZ_sasKey { get; set; }
        public string AZ_sasKeyName { get; set; }
        public bool IsConnected { get; set; }

        public class IoTMessageEventArgs
        {
            public string DeviceID { get; set; }
            public string Message { get; }

            public IoTMessageEventArgs(string deviceID, string message)
            {
                DeviceID = deviceID;
                Message = message;
            }
        }

        public delegate void IoTMessageEventHandler(object sender, IoTMessageEventArgs e);

        private ServiceClient? s_serviceClient;
        private EventHubClient? s_eventHubClient;
        private RegistryManager? s_registryManager;
        private List<Task>? iotFleetTasks;
        private CancellationTokenSource? iotFleetTaskCTS;

        public event IoTMessageEventHandler? IoTMessageEvent;

        public IoTFleet(string connectionString, string eventHubEP, string eventHubPath, string sasKey, string sasKeyName)
        {
            this.IsConnected = false;
            this.s_eventHubClient = null;
            this.s_serviceClient = null;
            this.s_registryManager = null;
            this.iotFleetTasks = null;
            this.iotFleetTaskCTS = null;

            this.AZ_connectionString = connectionString;
            this.AZ_eventHubEP = eventHubEP;
            this.AZ_eventHubPath = eventHubPath;
            this.AZ_sasKey = sasKey;
            this.AZ_sasKeyName = sasKeyName;

            this.IoTMessageEvent = null;
        }

        #region Cloud Operation
        /// <summary>
        /// Connect to azure IoT Hub
        /// </summary>
        /// <returns></returns>
        public int Connect2Cloud(int timeout = 5000)
        {
            int ret = -1;

            if (string.IsNullOrEmpty(AZ_connectionString) == false)
            {
                try
                {
                    s_serviceClient = ServiceClient.CreateFromConnectionString(AZ_connectionString);
                    ret = 1;
                }
                catch (Exception e)
                {
                    s_serviceClient = null;
                    ret = -2;
                }
            }

            if (string.IsNullOrEmpty(AZ_connectionString) == false && ret >= 0)
            {
                try
                {
                    s_registryManager = RegistryManager.CreateFromConnectionString(AZ_connectionString);
                }
                catch (Exception e)
                {
                    s_registryManager = null;
                    ret = -3;
                }
            }

            if (ret >= 0 && string.IsNullOrEmpty(AZ_eventHubEP) == false && string.IsNullOrEmpty(AZ_eventHubPath) == false && string.IsNullOrEmpty(AZ_sasKey) == false && string.IsNullOrEmpty(AZ_sasKeyName) == false)
            {
                var connectionString = new EventHubsConnectionStringBuilder(new Uri(AZ_eventHubEP), AZ_eventHubPath, AZ_sasKeyName, AZ_sasKey);
                try
                {
                    s_eventHubClient = EventHubClient.CreateFromConnectionString(connectionString.ToString());
                }
                catch (Exception e)
                {
                    s_eventHubClient = null;
                    ret = -4;
                }
            }

            if (ret >= 0 && s_eventHubClient != null)
            {
                EventHubRuntimeInformation? runtimeInfo = null;
                string[]? d2cPartitions = null;

                Task.Run(async () => {
                    for (int retry = 0; retry < 3; retry++)
                    {
                        try
                        {
                            runtimeInfo = await s_eventHubClient.GetRuntimeInformationAsync();
                            this.IsConnected = true;
                            break;
                        }
                        catch (Exception e)
                        {
                            this.IsConnected = false;
                            ret = -5;
                        }
                    }
                }).Wait(timeout);  // Wait for 5 seconds

                if (this.IsConnected == true && runtimeInfo != null)
                {
                    d2cPartitions = runtimeInfo.PartitionIds;
                    if (runtimeInfo.PartitionIds != null)
                    {
                        iotFleetTaskCTS = new CancellationTokenSource();
                        iotFleetTasks = new List<Task>();
                        foreach (string partition in d2cPartitions)
                        {
                            var eventHubReceiver = s_eventHubClient.CreateReceiver("$Default", partition, EventPosition.FromEnqueuedTime(DateTime.Now));
                            iotFleetTasks.Add(D2CMsgProcess(eventHubReceiver, iotFleetTaskCTS.Token));
                        }
                    }
                    else
                    {
                        ret = -6;
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Get List of Devices in IoT Hub
        /// </summary>
        /// <param name="onlyConnected"></param>
        /// <returns></returns>
        public async Task<List<string>?> GetDeviceListAsync(bool onlyConnected)
        {
            IQuery query;
            List<string>? deviceList = null;

            if (s_registryManager != null)
            {
                deviceList = new List<string>();

                try
                {
                    query = s_registryManager.CreateQuery("SELECT * FROM devices", 100);
                }
                catch (Exception ex)
                {
                    return null;
                }

                while (query.HasMoreResults)
                {
                    IEnumerable<Twin> pages;

                    try
                    {
                        pages = await query.GetNextAsTwinAsync();
                    }
                    catch (Exception ex)
                    {
                        break;
                    }

                    foreach (var twin in pages)
                    {
                        if (onlyConnected == true)
                        {
                            if (twin.ConnectionState == DeviceConnectionState.Connected)
                            {
                                deviceList.Add(twin.DeviceId);
                            }
                        }
                        else
                        {
                            deviceList.Add(twin.DeviceId);
                        }
                    }
                }
            }

            return deviceList;
        }

        /// <summary>
        /// flag of connectiveness to IoT Hub
        /// </summary>
        /// <returns></returns>
        public bool IsConnected2Cloud()
        {
            return this.IsConnected;
        }

        /// <summary>
        /// Disconnect from Cloud
        /// </summary>
        /// <returns></returns>
        public int Disconnect2Cloud()
        {
            int ret = 0;

            if (s_eventHubClient != null && s_eventHubClient.IsClosed == false)
            {
                s_eventHubClient.Close();
            }

            if (s_registryManager != null)
            {
                s_registryManager.Dispose();
                s_registryManager.CloseAsync().Wait();
            }

            if (s_serviceClient != null)
            {
                s_serviceClient.Dispose();
                s_serviceClient.CloseAsync();
            }

            if (iotFleetTaskCTS != null)
            {
                iotFleetTaskCTS.Cancel();
            }

            if (iotFleetTasks != null)
            {
                foreach (var item in iotFleetTasks)
                {
                    item.Wait(50);
                }
            }

            return ret;
        }

        /// <summary>
        /// Create new vessel in azure IoT Hub
        /// </summary>
        /// <param name="newDeviceID"></param>
        /// <returns></returns>
        public async Task<int> CreateVesselInIoTHubAsync(string newDeviceID)
        {
            int result = 1;
            Device device;

            var registryManager = RegistryManager.CreateFromConnectionString(this.AZ_connectionString);

            if (registryManager == null)
            {
                result = 0;
            }
            else
            {
                try
                {
                    device = await registryManager.AddDeviceAsync(new Device(newDeviceID));
                }
                catch (DeviceAlreadyExistsException)
                {
                    result = -1;
                }
                catch (Exception ex)
                {
                    result = -2;
                }
            }

            return result;
        }

        /// <summary>
        /// Remove a device from azure IoT Hub
        /// </summary>
        /// <param name="deviceID"></param>
        /// <returns></returns>
        public async Task<int> RemoveVesselFromIoTHubAsync(string deviceID)
        {
            int result = 1;
            var registryManager = RegistryManager.CreateFromConnectionString(this.AZ_connectionString);

            if (registryManager == null)
            {
                result = 0;
            }
            else
            {
                try
                {
                    await registryManager.RemoveDeviceAsync(deviceID);
                }
                catch (DeviceNotFoundException)
                {
                    result = -1;
                }
                catch (Exception ex)
                {
                    result = -2;
                }
            }

            return result;
        }

        #endregion

        /// <summary>
        /// Call Direct Call to a vessel
        /// </summary>
        /// <param name="vesselName"></param>
        /// <param name="commandName"></param>
        /// <param name="payloadJson"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public async Task<(int, string)> Call2Vessel(string vesselName, string commandName, string payloadJson, double timeout = 30)
        {
            int ret = 0;
            string stringResponse = string.Empty;
            CloudToDeviceMethodResult response;

            if (s_serviceClient != null)
            {
                // Create the invoke method
                var method = new CloudToDeviceMethod(commandName) { ResponseTimeout = TimeSpan.FromSeconds(timeout) };
                if (payloadJson != null && payloadJson != string.Empty)
                {
                    method.SetPayloadJson(payloadJson);
                }
                try
                {
                    response = await s_serviceClient.InvokeDeviceMethodAsync(vesselName, method);
                    stringResponse = response.GetPayloadAsJson();
                    ret = response.Status;
                }
                catch (Exception ex)
                {
                    ret = -1;
                }

            }

            return (ret, stringResponse);
        }

        /// <summary>
        /// Send Telemetry to a Vessel
        /// </summary>
        /// <param name="vesselName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public async Task<int> SendTelemetry2Vessel(string vesselName, string msg)
        {
            int ret = 0;
            if (string.IsNullOrEmpty(vesselName) == false && string.IsNullOrEmpty(msg) == false)
            {
                ret = await SendTelemetry2Vessel(vesselName, System.Text.Encoding.ASCII.GetBytes(msg));
            }

            return ret;
        }

        /// <summary>
        /// Send Telemetry Message to a Vessel
        /// </summary>
        /// <param name="vesselName"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        public async Task<int> SendTelemetry2Vessel(string vesselName, byte[] payload)
        {
            int ret = 0;

            if (s_serviceClient != null && string.IsNullOrEmpty(vesselName) == false && payload != null)
            {
                try
                {
                    await s_serviceClient.SendAsync(vesselName, new Message(payload));
                    ret = 1;
                }
                catch (Exception ex)
                {
                    ret = -1;
                }
            }

            return ret;
        }

        private async Task D2CMsgProcess( PartitionReceiver eventHubReceiver, CancellationToken ct)
        {
            string? deviceID;
            string? telemetry;
            IEnumerable<EventData> events;

            while (ct.IsCancellationRequested == false)
            {
                try
                {
                    events = await eventHubReceiver.ReceiveAsync(10);
                }
                catch (Exception e)
                {
                    continue;
                }
                if (events != null)
                {
                    foreach (EventData eventData in events)
                    {
                        if (eventData != null)
                        {
                            var sysProp = eventData.SystemProperties;
                            if (sysProp.TryGetValue("iothub-connection-device-id", out var senderID) == true)
                            {
                                deviceID = (senderID.ToString() == null ? String.Empty : senderID.ToString());
                            }
                            else
                            {
                                deviceID = string.Empty;
                            }
                            telemetry = (eventData.Body.Array != null && eventData.Body.Array.Length == 0) ? string.Empty : Encoding.UTF8.GetString(eventData.Body.Array);
                            if (this.IoTMessageEvent != null && string.IsNullOrEmpty(deviceID) == false && string.IsNullOrEmpty(telemetry) == false)
                            {
                                this.IoTMessageEvent(this, new IoTMessageEventArgs(deviceID, telemetry));
                            }
                        }
                    }
                }
            }
        }
    }
}