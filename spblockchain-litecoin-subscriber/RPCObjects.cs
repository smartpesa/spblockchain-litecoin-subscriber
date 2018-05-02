using System.Collections.Generic;

namespace SpBlockChainSubscriber
{
    public class Inputs
    {
        public string txid { get; set; }
        public int vout { get; set; }
        public int sequence { get; set; }
    }

    public class Outputs
    {
        public string address { get; set; }
        public string data { get; set; }
    }

    public class RPCResponse
    {
        public string transaction { get; set; }
        public dynamic result { get; set; }
        public Error error { get; set; }
        public string id { get; set; }
    }

    public class Error
    {
        public int code { get; set; }
        public string message { get; set; }
    }
}
