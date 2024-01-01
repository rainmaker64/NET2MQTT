using Microsoft.Azure.Devices.Client;
using SnSYS_IoT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Microsoft.Azure.Devices;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

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
            byte[] result = null;
            int statusCode = 500;
            HttpHeadResponse headResponse = null;
            MethodResponse methodResponse = null;

            Console.WriteLine($"Method: GetFileSize() is called, {methodRequest.DataAsJson}");

            if (string.IsNullOrEmpty(methodRequest.DataAsJson) == false)
            {
                var jobject = JObject.Parse(methodRequest.DataAsJson);

                if (jobject != null)
                {
                    string url;
                    string filename;

                    try
                    {
                        url = jobject["url"].ToString();
                        filename = jobject["filename"].ToString();

                    }
                    catch (Exception ex)
                    {
                        return methodResponse;
                    }

                    HttpResponseMessage httpResponse;
                    httpResponse = await SendHttpRequest(url, filename, HttpMethod.Head, string.Empty);

                    if (httpResponse != null)
                    {
                        headResponse = new HttpHeadResponse();

                        headResponse.FileURL = filename;
                        headResponse.Length = (int)httpResponse.Content.Headers.ContentLength.Value;

                        result = System.Text.Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(headResponse));
                        statusCode = (int)httpResponse.StatusCode;
                    }
                    try
                    {
                        methodResponse = new MethodResponse(result, statusCode);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            return methodResponse;
        }

        private async Task<MethodResponse> GetFileData(MethodRequest methodRequest, object userContext)
        {
            byte[] result = null;
            int statusCode = 500;
            HttpGetResponse getResponse = null;
            HttpResponseMessage httpResponse;
            MethodResponse methodResponse = null;

            Console.WriteLine($"Method: GetFileSize() is called, {methodRequest.DataAsJson}");

            if (string.IsNullOrEmpty(methodRequest.DataAsJson) == false)
            {
                var jobject = JObject.Parse(methodRequest.DataAsJson);

                if (jobject != null)
                {
                    string url;
                    string filename;
                    int length;
                    int offset;

                    try
                    {
                        url = jobject["url"].ToString();
                        filename = jobject["filename"].ToString();
                        length = (int)jobject["length"];
                        offset = (int)jobject["offset"];

                    }
                    catch (Exception ex)
                    {
                        return methodResponse;
                    }

                    httpResponse = await SendHttpRequest(url, filename, HttpMethod.Get, string.Empty);

                    if (httpResponse != null)
                    {
                        getResponse = new HttpGetResponse();

                        if (length == 0)
                        {
                            length = (int)httpResponse.Content.Headers.ContentLength.GetValueOrDefault();
                        }

                        if (length > 0)
                        {
                            getResponse.Data = new byte[length];
                            using (var filestream = httpResponse.Content.ReadAsStream())
                            {
                                getResponse.Length = filestream.Read(getResponse.Data, offset, length);
                            }

                            getResponse.FileURL = filename;
                            getResponse.TotalLength = (int)httpResponse.Content.Headers.ContentLength.Value;
                            getResponse.LastModified = httpResponse.Content.Headers.LastModified.GetValueOrDefault();
                            getResponse.ContentType = httpResponse.Content.Headers.ContentType.ToString();
                            getResponse.Offset = offset;
                        }
                        else
                        {
                            getResponse.TotalLength = (int)httpResponse.Content.Headers.ContentLength.Value;
                            getResponse.LastModified = httpResponse.Content.Headers.LastModified.GetValueOrDefault();
                            getResponse.Offset = 0;
                            getResponse.Length = 0;
                            getResponse.Data = null;
                        }

                        result = System.Text.Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(getResponse));
                        statusCode = (int)httpResponse.StatusCode;
                    }
                    try
                    {
                        methodResponse = new MethodResponse(result, statusCode);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            return methodResponse;
        }

        public async Task<HttpResponseMessage> SendHttpRequest(string serverUrl, string fileUrl, HttpMethod method, string request)
        {
            HttpResponseMessage httpResponse = null;

            string url = string.Format($@"http://{serverUrl}/{fileUrl}");
            //url = "http://127.0.0.1:80/test.txt";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "NET2MQTT/1.0");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                httpResponse = await client.SendAsync(new HttpRequestMessage(method, url));
            }

            if (httpResponse != null)
            {
                Console.WriteLine("HTTP Response received");
            }
            else
            {
                Console.WriteLine($"HTTP Response is null");
            }

            return httpResponse;
        }
    }
}
