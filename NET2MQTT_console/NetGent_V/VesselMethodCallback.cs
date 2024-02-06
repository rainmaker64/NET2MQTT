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
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using DotNetty.Common.Utilities;
using log4net;

namespace NetGent_V
{
    internal class VesselMethodCallback
    {
        private ILog log;
        private IoTVessel iot_vessel;
        private Dictionary<string, MethodCallback> vesselCallbacks;

        public VesselMethodCallback(IoTVessel iotvessel, ILog _log = null)
        {
            vesselCallbacks = new Dictionary<string, MethodCallback>();
            vesselCallbacks.Add("DC_HEAD", HttpRequest_HEAD);
            vesselCallbacks.Add("DC_GET", HttpRequest_GET);

            this.iot_vessel = iotvessel;
            this.log = _log;
        }

        /// <summary>
        /// Install Method-Callback for AZURE Direct2Call
        /// </summary>
        /// <returns></returns>
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

        private async Task<MethodResponse> HttpRequest_HEAD(MethodRequest methodRequest, object userContext)
        {
            int statusCode = 500;
            MethodResponse methodResponse = null;

            log.Info("*** HTTP HEAD REQUUEST ***");
            log.Info($"   data: {methodRequest.DataAsJson}");

            if (string.IsNullOrEmpty(methodRequest.DataAsJson) == false)
            {
                var param = JsonConvert.DeserializeObject<GetFileDataParam>(methodRequest.DataAsJson);

                if (param != null)
                {
                    byte[] buffer = null;
                    HttpResponseMessage httpResponse = await SendRequest2HttpServer(param.url, param.file, HttpMethod.Head, string.Empty);

                    if (httpResponse != null)
                    {
                        var headResponse = CopyHttpResponse(httpResponse);
                        buffer = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(headResponse));
                        statusCode = (int)httpResponse.StatusCode;
                    }
                    else
                    {
                        buffer = System.Text.Encoding.UTF8.GetBytes(string.Empty);
                    }

                    methodResponse = new MethodResponse(buffer, statusCode);
                }
            }
            else
            {
                log.Error("No Data Available");
            }

            return methodResponse;
        }


        private async Task<MethodResponse> HttpRequest_GET(MethodRequest methodRequest, object userContext)
        {
            byte[] result = null;
            int statusCode = 500;
            CopiedHttpResponse getResponse = null;
            HttpResponseMessage httpResponse;
            MethodResponse methodResponse = null;

            log.Info("*** HTTP GET REQUUEST ***");
            log.Info($"   data: {methodRequest.DataAsJson}");

            if (string.IsNullOrEmpty(methodRequest.DataAsJson) == false)
            {
                var param = JsonConvert.DeserializeObject<GetFileDataParam>(methodRequest.DataAsJson);

                if (param != null)
                {
                    httpResponse = await SendRequest2HttpServer(param.url, param.file, HttpMethod.Get, string.Empty);
                    if (httpResponse != null)
                    {
                        var contentLength = httpResponse.Content.Headers.ContentLength.GetValueOrDefault();

                        if (contentLength > 0)
                        {
                            getResponse = CopyHttpResponse(httpResponse);                           
                            await Task.Run(async () =>
                            {
                                int bulkcnt = 0;
                                int fileOffset = 0;
                                int bufferSize = 32*1024;
                                var buffer = new byte[bufferSize];

                                using (var filestream = httpResponse.Content.ReadAsStream())
                                {
                                    int bulksize = 0;
                                    string stringFileData = string.Empty;
                                    while (fileOffset < contentLength)
                                    {
                                        try
                                        {
                                            filestream.Seek(fileOffset, SeekOrigin.Begin);
                                            bulksize = filestream.Read(buffer, 0, bufferSize);
                                            Array.Resize<byte>(ref buffer, bulksize);

                                            fileOffset = fileOffset + bulksize;
                                        }
                                        catch (Exception ex)
                                        {
                                            break;
                                        }
                                        finally
                                        {
                                            int retTel = await this.iot_vessel.PutTelemetryAsync(JsonConvert.SerializeObject(new Net2MqttMessage("HTTP_GET", string.Empty, bulkcnt++, 0, buffer, bulksize)));
                                        }
                                    }
                                }
                            });
                        }
                        else
                        {
                            getResponse = CopyHttpResponse(httpResponse);
                        }
                        result = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(getResponse));
                        statusCode = (int)httpResponse.StatusCode;
                    }

                    try
                    {
                        methodResponse = new MethodResponse(result, statusCode);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                    }
                }
            }
            else
            {
                log.Error("No Data Available");
            }
            return methodResponse;
        }


        private async Task<HttpResponseMessage> SendRequest2HttpServer(string serverUrl, string fileUrl, HttpMethod method, string request)
        {
            HttpResponseMessage httpResponse = null;

            using (var client = new HttpClient()) 
            {
                client.DefaultRequestHeaders.Add("User-Agent", "NET2MQTT/1.0");
                client.DefaultRequestHeaders.Add("Accept", "text/xml; charset=utf-8");
                client.DefaultRequestHeaders.Add("Date", DateTime.Now.ToUniversalTime().ToString("r"));

                try
                {
                    log.Info($"Send HTTP Request, {method} to {string.Format($@"http://{serverUrl}/{fileUrl}")}");

                    httpResponse = await client.SendAsync(new HttpRequestMessage(method, string.Format($@"http://{serverUrl}/{fileUrl}")));
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                }
            }
            if (httpResponse != null)
            {
                log.Info($"*** RESPONSE of {method.ToString()} ***");
                log.Info($"   Status Code: {httpResponse.StatusCode}");
                log.Info($"   Last-Modified: {httpResponse.Content.Headers.LastModified}");
                log.Info($"   Content-Length: {httpResponse.Content.Headers.ContentLength}");
            }
            else
            {
                log.Error("Response is null");
            }

            return httpResponse;
        }

        private CopiedHttpResponse CopyHttpResponse(HttpResponseMessage httpResponse)
        {
            int length = 0;
            var copiedHttpResponse = new CopiedHttpResponse();

            if (httpResponse.Content != null)
            {
                string strLeng = string.Empty;

                // Status Code
                copiedHttpResponse.StatusCode = (int)httpResponse.StatusCode;

                // Content-Length
                var strLength = ExtractValueFromHttpContentHeader(httpResponse.Content.Headers, "Content-Length");
                copiedHttpResponse.ContentLength = int.TryParse(strLength, out length) == true ? length : 0;

                // file URL
                if (httpResponse.RequestMessage.RequestUri != null &&
                    httpResponse.RequestMessage.RequestUri.IsAbsoluteUri == true)
                {
                    copiedHttpResponse.FileURL = httpResponse.RequestMessage.RequestUri.AbsolutePath;
                }
                else
                {
                    copiedHttpResponse.FileURL = string.Empty;
                }
                
                copiedHttpResponse.ContentType = ExtractValueFromHttpContentHeader(httpResponse.Content.Headers, "Content-Type"); // Conent-Type
                copiedHttpResponse.LastModified = ExtractValueFromHttpContentHeader(httpResponse.Content.Headers, "Last-Modified"); // Last-Modified                
                copiedHttpResponse.AcceptRanges = ExtractValueFromHttpContentHeader(httpResponse.Content.Headers, "Accept-Ranges"); // Accept-Ranges                
                copiedHttpResponse.CacheControl = ExtractValueFromHttpContentHeader(httpResponse.Content.Headers, "Cache-Control"); // Cache-Control
                copiedHttpResponse.ETag = ExtractValueFromHttpContentHeader(httpResponse.Content.Headers, "ETag"); // ETag
                copiedHttpResponse.Date = ExtractValueFromHttpContentHeader(httpResponse.Content.Headers, "Date"); // Date
                copiedHttpResponse.Connection = ExtractValueFromHttpContentHeader(httpResponse.Content.Headers, "Connection"); // Connection
                copiedHttpResponse.KeepAlive = ExtractValueFromHttpContentHeader(httpResponse.Content.Headers, "Keep-Alive"); // Keep-Alive
            }

            return copiedHttpResponse;
        }

        private string ExtractValueFromHttpContentHeader(HttpContentHeaders headers, string type)
        {
            string value = string.Empty;

            try
            {
                var list = headers.GetValues(type);
                value = list.First();
            }
            catch (Exception ex)
            { }

            return value;
        }
    }
}
