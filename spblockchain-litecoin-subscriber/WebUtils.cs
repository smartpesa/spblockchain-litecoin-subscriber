using System.IO;
using System.Net;
using System;
using log4net;
using System.Configuration;

namespace SpBlockChainSubscriber
{
    public class WebUtils
    {
        public static string CallRequest(ILog log, string jsonParameters = null)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(ConfigurationManager.AppSettings["litecoinFullNode"]);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Headers["authorization"] = "Basic " + ConfigurationManager.AppSettings["litecoinFullNodeAuth"];
            using (var streamWriter = new StreamWriter(req.GetRequestStream()))
            {
                streamWriter.Write(jsonParameters);
                streamWriter.Flush();
            }
            try
            {
                HttpWebResponse rsp = (HttpWebResponse) req.GetResponse();
                return GetResponse(rsp);
            }
            catch (WebException ex)
            {
                log.Error(ex.Message);
                return GetResponse((HttpWebResponse)ex.Response);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                return "{ \"error\": { \"code\" : \"500\", \"message\": \"" + ex.Message + "\" }}";
            }
        }

        private static string GetResponse(HttpWebResponse rsp)
        {
            using (var streamReader = new StreamReader(rsp.GetResponseStream()))
            {
                return streamReader.ReadToEnd();
            }
        }

        public static T ParseResponse<T>(string json) where T : RPCResponse
        {
            T result = (T)Activator.CreateInstance(typeof(T));
            try
            {
                result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                result.error = new Error
                {
                    message = ex.Message
                };
            }
            return result;
        }
    }
}