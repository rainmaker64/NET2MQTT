using Microsoft.Azure.Devices.Client;
using SnSYS_IoT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetGent_V
{
    internal class VesselMethodCallback
    {
        private IoTVessel iot_vessel;
        private Dictionary<string, MethodCallback> vesselCallbacks;

        public VesselMethodCallback(IoTVessel iotvessel)
        {
            vesselCallbacks = new Dictionary<string, MethodCallback>();
            vesselCallbacks.Add("GetFileSize", GetFileSize);
            vesselCallbacks.Add("GetFileData", GetFileData);

            this.iot_vessel = iotvessel;
        }

        public int InstallMethodCallbacks()
        {
            int ret = 0;

            if (this.iot_vessel != null)
            {
                foreach (var item in vesselCallbacks)
                {
                    this.iot_vessel.RegisterCommandAsync(item.Key, item.Value);
                    ret++;
                }
            }

            return ret;
        }

        private async Task<MethodResponse> GetFileSize(MethodRequest methodRequest, object userContext)
        {
            HttpStatusCode statusCode;
            string response;

            Console.WriteLine("Method: GetFileSize() is called");

            (statusCode, response) = await SendHttpRequest("127.0.0.1:80", "test.txt", HttpMethod.Head, string.Empty);

            var result = System.Text.Encoding.ASCII.GetBytes(response);

            MethodResponse methodResponse = new MethodResponse(result, (int)statusCode);

            return methodResponse;
        }

        private Task<MethodResponse> GetFileData(MethodRequest methodRequest, object userContext)
        {
            MethodResponse response = null;

            Console.WriteLine("Method: GetFileData() is called");

            return Task.FromResult(response);
        }

        public async Task<(HttpStatusCode, string)> SendHttpRequest(string serverUrl, string fileUrl, HttpMethod method, string request)
        {
            string response = string.Empty;
            HttpStatusCode statusCode = HttpStatusCode.BadRequest;

            string url = string.Format($@"http://{serverUrl}/{fileUrl}");
            //url = "http://127.0.0.1:80/test.txt";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "NET2MQTT/1.0");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var result = await client.SendAsync(new HttpRequestMessage(method, url));

                if (result != null)
                {
                    statusCode = result.StatusCode;
                    Console.WriteLine($"{result.Content.Headers.ContentLength}");
                    response = await result.Content.ReadAsStringAsync();
                }
            }

            Console.WriteLine($"status: {statusCode}, response: {response}");

            return (statusCode, response);
        }
    }
}
