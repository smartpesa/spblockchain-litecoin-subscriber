using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Topshelf;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]

namespace SpBlockChainSubscriber
{
    class Program
    {
        static void Main(string[] args)
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
    }
}
