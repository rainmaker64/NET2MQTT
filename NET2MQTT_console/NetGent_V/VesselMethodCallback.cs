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
                        filename = jobject["file"].ToString();

                    }
                    catch (Exception ex)
                    {
                        return methodResponse;
                    }

                    HttpResponseMessage httpResponse = await SendHttpRequest(url, filename, HttpMethod.Head, string.Empty);

                    if (httpResponse != null)
                    {
                        int length;
                        var headResponse = new HttpHeadResponse();

                        headResponse.StatusCode = (int)httpResponse.StatusCode;
                        headResponse.FileURL = httpResponse.RequestMessage.RequestUri.AbsolutePath;
                        Console.WriteLine($"File URL = {headResponse.FileURL}");
                        headResponse.ContentType = httpResponse.Content.Headers.GetValues("Content-Type").ToList()[0];
                        var contentlength_int = httpResponse.Content.Headers.GetValues("Content-Length").ToList()[0];
                        headResponse.ContentLength = int.TryParse(contentlength_int, out length) == true ? length: 0;

                        headResponse.LastModified = httpResponse.Content.Headers.GetValues("Last-Modified").ToList()[0];
                        headResponse.AcceptRanges = httpResponse.Headers.GetValues("Accept-Ranges").ToList()[0];
                        headResponse.CacheControl = httpResponse.Headers.GetValues("Cache-Control").ToList()[0];
                        headResponse.ETag = httpResponse.Headers.GetValues("ETag").ToList()[0];
                        headResponse.Date = httpResponse.Headers.GetValues("Date").ToList()[0];
                        headResponse.Connection = httpResponse.Headers.GetValues("Connection").ToList()[0];
                        headResponse.KeepAlive = httpResponse.Headers.GetValues("Keep-Alive").ToList()[0];

                        result = System.Text.Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(headResponse));
                        statusCode = (int)httpResponse.StatusCode;
                    }
                    else
                    {
                        result = System.Text.Encoding.ASCII.GetBytes(string.Empty);
                    }

                    methodResponse = new MethodResponse(result, statusCode);
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

            Console.WriteLine($"Method: GetFileData() is called, {methodRequest.DataAsJson}");

            if (string.IsNullOrEmpty(methodRequest.DataAsJson) == false)
            {
                var param = JsonConvert.DeserializeObject<GetFileDataParam>(methodRequest.DataAsJson);

                if (param != null)
                {
                    httpResponse = await SendHttpRequest(param.url, param.file, HttpMethod.Get, string.Empty);

                    if (httpResponse != null)
                    {
                        /*
                        Console.WriteLine($"==> {httpResponse.ToString()}");
                        Console.WriteLine("length = " + httpResponse.Content.Headers.ContentLength.Value);
                        Console.WriteLine("LastModified = " + httpResponse.Content.Headers.LastModified);
                        */
                        getResponse = new HttpGetResponse();

                        if (param.length == 0)
                        {
                            param.length = (int)httpResponse.Content.Headers.ContentLength.GetValueOrDefault();
                        }

                        if (param.length > 0)
                        {
                            int length;
                            getResponse.Data = new byte[param.length];

                            try
                            {
                                var filestream = httpResponse.Content.ReadAsStream();
                                filestream.Seek(param.offset, SeekOrigin.Begin);
                                getResponse.Length = filestream.Read(getResponse.Data, 0, param.length);
                                filestream.Close();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception:" + ex.Message);
                            }

                            getResponse.FileURL = httpResponse.RequestMessage.RequestUri.AbsolutePath;
                            getResponse.StatusCode = (int)httpResponse.StatusCode;
                            getResponse.ContentType = httpResponse.Content.Headers.GetValues("Content-Type").ToList()[0];
                            var contentlength_int = httpResponse.Content.Headers.GetValues("Content-Length").ToList()[0];
                            getResponse.ContentLength = int.TryParse(contentlength_int, out length) == true ? length : 0;

                            getResponse.LastModified = httpResponse.Content.Headers.GetValues("Last-Modified").ToList()[0];
                            getResponse.AcceptRanges = httpResponse.Headers.GetValues("Accept-Ranges").ToList()[0];
                            getResponse.CacheControl = httpResponse.Headers.GetValues("Cache-Control").ToList()[0];
                            getResponse.ETag = httpResponse.Headers.GetValues("ETag").ToList()[0];
                            getResponse.Date = httpResponse.Headers.GetValues("Date").ToList()[0];
                            getResponse.Connection = httpResponse.Headers.GetValues("Connection").ToList()[0];
                            getResponse.KeepAlive = httpResponse.Headers.GetValues("Keep-Alive").ToList()[0];

                            getResponse.Offset = param.offset;
                        }
                        else
                        {
                            int length;
                            var contentlength_int = httpResponse.Content.Headers.GetValues("Content-Length").ToList()[0];

                            getResponse.ContentLength = int.TryParse(contentlength_int, out length) == true ? length : 0;
                            getResponse.LastModified = httpResponse.Content.Headers.GetValues("Last-Modified").ToList()[0];
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
                        //Console.WriteLine("Return successfully methodResponse");
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
