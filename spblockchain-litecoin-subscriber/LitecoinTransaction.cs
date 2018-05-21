using log4net;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;

namespace SpBlockChainSubscriber
{
    public class LitecoinTransaction
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(SpBlockSubcriberService));
        private string _senderAddr;
        private string _receiveAddr;
        private decimal _amount;
        public List<UTXOResponse> _uTXOs { get; set; }

        public LitecoinTransaction()
        {

        }

        public void StartTransaction()
        {
            try
            {
                HandleInput();
                string txHex = CreateRawTransaction();
                string signedHex = SignRawTransaction(txHex);
                if (signedHex == null) return;
                SendRawTransaction(signedHex);

            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
            }
        }

        public void HandleInput()
        {
            Console.WriteLine("Enter sender address:");
            _senderAddr = Console.ReadLine();
            _uTXOs = GetListUnspent(_senderAddr);
            Console.WriteLine("Enter receive address:");
            _receiveAddr = Console.ReadLine();
            Console.WriteLine("Enter amount (LTC):");
            while (!decimal.TryParse(Console.ReadLine(), out _amount)) {
                Console.WriteLine("Enter amount (LTC):");
            }
        }


        public string CreateRawTransaction()
        {
            BitcoinAddress senderAddr = BitcoinAddress.Create(_senderAddr, Network.TestNet);
            BitcoinAddress receiverAddr = BitcoinAddress.Create(_receiveAddr, Network.TestNet);
            BitcoinAddress smartpesaAddr = BitcoinAddress.Create(ConfigurationManager.AppSettings["SmartPesaAddr"], Network.TestNet);

            decimal minerFee = 0.001m;
            decimal fee = CalcFee(_amount);
            if (bool.Parse(ConfigurationManager.AppSettings["IncludeFee"]))
            {
                _amount = _amount - fee;
            }

            Coin[] sendCoins = GetTxOuts(senderAddr, _amount + fee + minerFee);

            var txBuilder = new TransactionBuilder();
            var tx = txBuilder
                .AddCoins(sendCoins)
                .Send(receiverAddr, _amount.ToString())
                .Send(smartpesaAddr, fee.ToString())
                .SetChange(senderAddr)
                .SendFees(minerFee.ToString())
                .BuildTransaction(false);

            _log.Debug(tx);

            return tx.ToHex();
        }

        private List<UTXOResponse> GetListUnspent(string address)
        {
            dynamic obj = new
            {
                addrs = address
            };

            string jsonUTXO = WebUtils.RequestApi(_log, "/addrs/utxo", Newtonsoft.Json.JsonConvert.SerializeObject(obj));
            List<UTXOResponse> uTXOResponse = WebUtils.ParseApiResponse<List<UTXOResponse>>(jsonUTXO);

            _log.InfoFormat("Total amount unspent: {0} LTC", uTXOResponse.Sum(x => x.amount));
            return uTXOResponse;
        }

        private Coin[] GetTxOuts(BitcoinAddress senderAddr, decimal sendAmount)
        {
            List<Coin> coins = new List<Coin>();
            int idx = 0;
            while (coins.Sum(x => x.TxOut.Value.Satoshi) < LTC2Satoshi((long)sendAmount))
            {
                TxOut txOut = new TxOut(new Money(_uTXOs[idx].satoshis), senderAddr);
                coins.Add(new Coin(new OutPoint(uint256.Parse(_uTXOs[idx].txid), _uTXOs[idx].vout), txOut));
                idx++;
            }

            return coins.ToArray();
        }

        private decimal CalcFee(decimal amount)
        {
            return amount * decimal.Parse(ConfigurationManager.AppSettings["PercentFee"]) / 100;
        }

        public static string SignRawTransaction(string txHex)
        {
            Dictionary<string, object> rPCRequest = new Dictionary<string, object>()
            {
                { "jsonrpc", "1.0" },
                { "id", "testid" },
                { "method", "signrawtransaction" },
                { "params", new List<string> {
                    txHex
                }
            }};

            string jsonRequest = Newtonsoft.Json.JsonConvert.SerializeObject(rPCRequest);
            _log.Info("Request: " + jsonRequest);
            string response = WebUtils.RequestRPC(_log, jsonRequest);
            _log.Info("Response: " + response);

            RPCResponse rpcResp = Newtonsoft.Json.JsonConvert.DeserializeObject<RPCResponse>(response);
            if (rpcResp.error != null) return null;
            return rpcResp.result.hex.ToString();
        }

        public static void SendRawTransaction(string signedHex)
        {
            Dictionary<string, object> rPCRequest = new Dictionary<string, object>()
            {
                { "jsonrpc", "1.0" },
                { "id", "testid" },
                { "method", "sendrawtransaction" },
                { "params", new List<string> {
                    signedHex
                }
            }};

            string jsonRequest = Newtonsoft.Json.JsonConvert.SerializeObject(rPCRequest);
            _log.Info("Request: " + jsonRequest);
            string response = WebUtils.RequestRPC(_log, jsonRequest);
            _log.Info("Response: " + response);
        }

        private static long LTC2Satoshi(long ltc)
        {
            return ltc * 100000000;
        }

    }
}