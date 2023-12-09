using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Message = Microsoft.Azure.Devices.Client.Message;
using TransportType = Microsoft.Azure.Devices.Client.TransportType;

namespace SnSYS_IoT
{
    public class IoTVessel: IDisposable
    {
        private string _connectionString;
        private DeviceClient? s_deviceClient;
        private BufferBlock<string> _iotHubRxBuffer;
        private CancellationTokenSource _task_cts;
        private Task _c2dMsgTask;

        public IoTVessel(string connectionString)
        {
            _connectionString = connectionString;
            _iotHubRxBuffer = new BufferBlock<string>();
            _task_cts = new CancellationTokenSource();
            _c2dMsgTask = new Task(async () => await iotC2DMsgProcess(_task_cts));
        }

        public int Open()
        {
            int ret = -1;

            s_deviceClient = DeviceClient.CreateFromConnectionString(_connectionString, TransportType.Amqp);

            if (s_deviceClient != null)
            {
                _c2dMsgTask.Start();
                ret = 1;
            }
            else
            {
                ret = -1;
            }

            return ret;
        }

        public void Close()
        {
            if (s_deviceClient != null)
                s_deviceClient.CloseAsync();
        }

        public void Dispose()
        {
            _task_cts.Cancel();
            _c2dMsgTask.Wait();
        }

        public void ClearBuffer()
        {
            IList<string> itemsList;
            _iotHubRxBuffer.TryReceiveAll(out itemsList);
        }

        public string GetTelemetry()
        {
            return _iotHubRxBuffer.Receive();
        }

        public async Task<int> PutTelemetryAsync(string telemetry)
        {
            int ret = 0;

            if (s_deviceClient != null && string.IsNullOrEmpty(telemetry) == false)
            {
                await s_deviceClient.SendEventAsync(new Message(Encoding.ASCII.GetBytes(telemetry)));

                ret = 1;
            }

            return ret;
        }

        /// <summary>
        /// Register Method into Device Client
        /// </summary>
        /// <param name="commandName">command name to register</param>
        /// <param name="methodCallback">real method to be invoked</param>
        public async void RegisterCommandAsync(string commandName, MethodCallback methodCallback)
        {
            if (s_deviceClient != null)
            {
                await s_deviceClient.SetMethodHandlerAsync(commandName, methodCallback, null);
            }
            else
            {
                Console.WriteLine("ERROR: Cannot register the command into IoT Hub");
            }
        }


        private async Task iotC2DMsgProcess(CancellationTokenSource ct)
        {
            while (true) {
                if (ct.IsCancellationRequested == false && s_deviceClient != null) {
                    try {
                        var msg = await s_deviceClient.ReceiveAsync();

                        if (msg != null) {
                            _iotHubRxBuffer.Post<string>(Encoding.ASCII.GetString(msg.GetBytes()));
                            await s_deviceClient.CompleteAsync(msg);
                        }
                    }
                    catch (Exception err) {}
                }
                else {
                    break;
                }
            }
        }
    }
}
