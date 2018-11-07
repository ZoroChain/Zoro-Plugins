using Zoro.Ledger;
using Zoro.Wallets;
using System;

namespace Zoro.Plugins
{
    public class AppChainPlugin : Plugin
    {
        public Wallet Wallet { get; private set; }

        private CommandHandler cmdHander;
        private EventHandler eventHandler;

        private ushort port = Settings.Default.Port;
        private ushort wsport = Settings.Default.WsPort;

        public AppChainPlugin(PluginManager pluginMgr)
            : base(pluginMgr)
        {
            if (pluginMgr.System != ZoroSystem.Root)
                return;

            cmdHander = new CommandHandler(this);
            eventHandler = new EventHandler(this);

            Blockchain.AppChainNofity += OnAppChainEvent;
        }

        public override void SetWallet(Wallet wallet)
        {
            Wallet = wallet;
        }

        public bool NoWallet()
        {
            if (Wallet != null) return false;
            Console.WriteLine("You have to open the wallet first.");
            return true;
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            PluginMgr.Log(nameof(AppChainPlugin), level, message);
        }

        public override bool OnMessage(object message)
        {
            if (message is ZoroSystem.AppChainStarted evt)
            {
                eventHandler.OnAppChainStarted(evt.ChainHash, evt.Port, evt.WsPort);
                return true;
            }
            if (!(message is string[] args)) return false;
            if (args[0] != "appchain") return false;
            return cmdHander.OnAppChainCommand(args);
        }

        private void OnAppChainEvent(object sender, AppChainEventArgs args)
        {
            eventHandler.OnAppChainEvent(args);
        }
    }
}
