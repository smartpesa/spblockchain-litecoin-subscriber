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
    public class SpBlockSubcriberService
    {
        private bool _debug = false;
        private Task _serverTask;
        private bool _isRunning = true;
        private static readonly ILog _log = LogManager.GetLogger(typeof(SpBlockSubcriberService));
        private static ConsoleCtrlDelegate _closeHandler;
        readonly Encoding _encoding = Encoding.UTF8;

        public SpBlockSubcriberService()
        {
            _log.InfoFormat("SmartPesa BlockChain Subscriber constructor, thread id {0}", Thread.CurrentThread.ManagedThreadId);

            try
            {
                _debug = bool.Parse(ConfigurationManager.AppSettings["Debug"]);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        ///     Start SpBlockChain service
        /// </summary>
        public void Start()
        {
            _log.Info("================================================================================");
            _log.Info("StartUp: " + DateTime.Now.ToString());
            _log.InfoFormat("SmartPesa BlockChain Subscriber v{0}", this.GetType().Assembly.GetName().Version.ToString());
            _log.Info("SmartPesa MQ Version: " + ZeroMQ.lib.zmq.LibraryVersion);
            _log.Info("");
            _serverTask = new Task(ZmqTransactionWorker);
            _serverTask.Start();

            // setup console event handler
            _closeHandler += new ConsoleCtrlDelegate(ConsoleCtrlCheck);
            SetConsoleCtrlHandler(_closeHandler, true);
        }

        /// <summary>
        ///     Stop SmartPesa BlockChain Subscriber service
        /// </summary>
        public void Stop()
        {
            _log.InfoFormat("SmartPesa BlockChain Subscriber Stop, thread id {0}", Thread.CurrentThread.ManagedThreadId);
            this._isRunning = false;
            _serverTask.Wait();
            _log.Info("SmartPesa BlockChain Subscriber Shutdown Completed!");
        }

        #region Console Close Event Handler
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        public delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

        private bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            _log.InfoFormat("console control event {0}", ctrlType);

            switch (ctrlType)
            {
                case CtrlTypes.CTRL_CLOSE_EVENT:
                    Stop();
                    return true;
            }

            return false;
        }
        #endregion


        /// <summary>
        ///     main thread loop
        /// </summary>
        private void ZmqTransactionWorker()
        {
            while (_isRunning)
            {
                try
                {
                    using (var context = new ZContext())
                    using (var subscriber = new ZSocket(context, ZSocketType.SUB))
                    {
                        string router = ConfigurationManager.AppSettings["spBlock.Subscriber"];
                        Console.WriteLine("Subscriber connecting to : " + router);
                        string _topic = ConfigurationManager.AppSettings["spBlock.Topics"];
                        Console.WriteLine("Subscriber topic : " + _topic);
                        string[] topics = _topic.Split(new string[] { ",", ";" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string top in topics)
                        {
                            subscriber.SetOption(ZSocketOption.SUBSCRIBE, top);
                        }
                        subscriber.Connect(router);

                        ZError error;
                        ZMessage incoming;
                        var poll = ZPollItem.CreateReceiver();

                        while (_isRunning)
                        {
                            try
                            {
                                // Poll for activity, or 1 second timeout
                                if (subscriber.PollIn(poll, out incoming, out error, TimeSpan.FromMilliseconds(100)))
                                {
                                    if (incoming != null)
                                    {
                                        byte[] topic = incoming[0].Read();
                                        byte[] body = incoming[1].Read();
                                        switch (_encoding.GetString(topic))
                                        {
                                            case "hashblock":
                                                _log.Debug("- HASH BLOCK (" + BinaryToHexString(body) + ") -");
                                                break;

                                            case "hashtx":
                                                _log.Debug("- HASH TX  (" + BinaryToHexString(body) + ") -");
                                                break;

                                            case "rawblock":
                                                NBitcoin.Block block = NBitcoin.Block.Load(body, NBitcoin.Network.Main);
                                                _log.Debug("- RAW BLOCK HEADER (" + block.Header + ") -");
                                                _log.InfoFormat("Total transactions in block: {0}", block.Transactions.Count);
                                                //_log.Info(BinaryToHexString(body.Take(80).ToArray<byte>()));
                                                break;

                                            case "rawtx":
                                                NBitcoin.Transaction transaction = new NBitcoin.Transaction(body);
                                                _log.Debug("- RAW TX (" + transaction.GetHash() + ") -");
                                                _log.InfoFormat("{0} Inputs Consumed", transaction.Inputs.Count);
                                                _log.InfoFormat("{0} Outputs Created", transaction.Outputs.Count);
                                                _log.InfoFormat("AMOUNT TRANSACTED: {0} LTC", Satoshi2LTC(transaction.Outputs.Sum(x => x.Value)));
                                                break;

                                            default:
                                                _log.Error("Unknow topic: " + _encoding.GetString(topic));
                                                break;

                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                if (_isRunning)
                                {
                                    _log.WarnFormat("server subscriber exception {0}", e.Message);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e.Message);
                }
            }

            _log.Info("server task shutting down...");
        }

        public string BinaryToHexString(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var itm in bytes)
            {
                sb.Append(itm.ToString("X2"));
            }

            return sb.ToString();
        }

        public byte[] FromHex(string hex)
        {
            hex = hex.Replace("-", "");
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return raw;
        }

        private decimal Satoshi2LTC(long satoshi)
        {
            return satoshi / 100000000m;
        }
    }
}