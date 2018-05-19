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
        readonly static Encoding _encoding = Encoding.UTF8;

        public LitecoinTransaction()
        {

        }

        public static void HandleInput(out string receiveAddr, out string amount)
        {
            Console.WriteLine("Enter receive address:");
            receiveAddr = Console.ReadLine();
            Console.WriteLine("Enter amount (LTC):");
            amount = Console.ReadLine();
        }


        public static string CreateRawTransaction(string receiveAddr, string amount)
        {            
            BitcoinSecret senderAddr = new BitcoinSecret(ConfigurationManager.AppSettings["ltcPrivateKey"]);
            BitcoinAddress receiverAddr = BitcoinAddress.Create(receiveAddr, Network.TestNet);
            BitcoinAddress smartpesaAddr = BitcoinAddress.Create(ConfigurationManager.AppSettings["SmartPesaAddr"], Network.TestNet);
            
            decimal sendAmount;
            decimal.TryParse(amount, out sendAmount);
            decimal fee = CalcFee(sendAmount);
            if (bool.Parse(ConfigurationManager.AppSettings["IncludeFee"]))
            {
                sendAmount = sendAmount - fee;
            }

            Coin[] sendCoins = GetCoinsUnspent(senderAddr.GetAddress(), sendAmount + fee);

            var txBuilder = new TransactionBuilder();
            var tx = txBuilder
                .AddCoins(sendCoins)
                .AddKeys(senderAddr.PrivateKey)
                .Send(receiverAddr, sendAmount.ToString("N2"))
                .Send(smartpesaAddr, fee.ToString("N2"))
                .SetChange(senderAddr.GetAddress())
                .SendFees("0.001")
                .BuildTransaction(true);

            _log.Debug(tx);

            return tx.ToHex();
        }

        private static Coin[] GetCoinsUnspent(BitcoinPubKeyAddress senderAddr, decimal sendAmount)
        {
            dynamic obj = new
            {
                addrs = senderAddr.ToString()
            };

            string jsonUTXO = WebUtils.RequestApi(_log, "api/addrs/utxo", Newtonsoft.Json.JsonConvert.SerializeObject(obj));
            List<UTXOResponse> uTXOResponse = WebUtils.ParseApiResponse<List<UTXOResponse>>(jsonUTXO);

            List<Coin> coins = new List<Coin>();
            int idx = 0;
            while (coins.Sum(x => x.TxOut.Value.Satoshi) < LTC2Satoshi((long)sendAmount))
            {
                TxOut txOut = new TxOut(new Money(uTXOResponse[idx].satoshis), senderAddr);
                coins.Add(new Coin(new OutPoint(uint256.Parse(uTXOResponse[idx].txid), uTXOResponse[idx].vout), txOut));
                idx++;
            }

            return coins.ToArray();
        }

        private static decimal CalcFee(decimal amount)
        {
            return amount * decimal.Parse(ConfigurationManager.AppSettings["PercentFee"]) / 100;
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