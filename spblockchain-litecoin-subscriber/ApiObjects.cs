using System;
using System.Collections.Generic;

namespace SpBlockChainSubscriber
{
    public class ApiResponse
    {
        public string error { get; set; }
    }

    public class UTXOResponse : ApiResponse
    {
        public string address { get; set; }
        public string txid { get; set; }

        public int vout { get; set; }

        public string scriptPubKey { get; set; }
        public decimal amount { get; set; }
        public Int64 satoshis { get; set; }
        public int height { get; set; }
        public int confirmations { get; set; }
    }
}
