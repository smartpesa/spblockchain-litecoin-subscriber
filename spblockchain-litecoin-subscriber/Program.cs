using System;
using System.Threading;
using Topshelf;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]

namespace SpBlockChainSubscriber
{
    class Program
    {
        static void Main(string[] args)
        {
            ShowMenu();
        }

        internal class Server
        {
            private SpBlockSubcriberService _spService;
            private Thread _serverThread;
            private bool _shutdownIsInProgress = false;

            public Server()
            {
            }

            public void Start()
            {
                _spService = new SpBlockSubcriberService();
                _serverThread = new Thread(_spService.Start);
                _serverThread.Start();
            }

            public void Stop()
            {
                if (_shutdownIsInProgress) return;
                _shutdownIsInProgress = true;
                _spService.Stop();
            }
        }

        public static void StartServer()
        {
            HostFactory.Run(x =>
            {
                x.Service<Server>(s =>
                {
                    s.ConstructUsing(name => new Server());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();
                x.SetDescription("SmartPesa BlockChain Subscriber Service");
                x.SetDisplayName("SmartPesa BlockChain Subscriber");
                x.SetServiceName("spBlockChainSubscriber");
                x.SetInstanceName("spBlockChainSubscriber");
            });
        }
        
        public static void ShowMenu()
        {
            Console.WriteLine("Please choice:");
            Console.WriteLine("1. Subscriber txn");
            Console.WriteLine("2. Create txn");
            ChoiceMenu();
        }
		
        public static void ChoiceMenu()
        {
            switch (Console.ReadLine())
            {
                case "1":
                    StartServer();
                    break;
                case "2":
                    string receiveAddr;
                    string amount;
                    LitecoinTransaction.HandleInput(out receiveAddr, out amount);
                    string txHex = LitecoinTransaction.CreateRawTransaction(receiveAddr, amount);
                    LitecoinTransaction.SendRawTransaction(txHex);
                    ShowMenu();
                    break;

                default:
                    ShowMenu();
                    break;
            }
        }
    }
}
