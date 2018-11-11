using System;
using Akka.Actor;
using Zoro.Ledger;
using Zoro.Wallets;
using Zoro.Network.P2P;
using Zoro.Cryptography.ECC;

namespace Zoro.Plugins
{
    public class AppChainPlugin : Plugin
    {
        public Wallet Wallet { get; private set; }

        private CommandHandler cmdHander;
        private AppChainManager appchainMgr;

        public AppChainPlugin(PluginManager pluginMgr)
            : base(pluginMgr)
        {
            cmdHander = new CommandHandler(this);
            appchainMgr = new AppChainManager(this);

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
            PluginMgr.Log(nameof(AppChainPlugin), level, message, UInt160.Zero);
        }

        public override bool OnMessage(object message)
        {
            if (message is ZoroSystem.ChainStarted evt)
            {
                appchainMgr.OnChainStarted(evt.ChainHash, evt.Port, evt.WsPort);

                return true;
            }
            if (!(message is string[] args)) return false;
            if (args[0] != "appchain") return false;
            return cmdHander.OnAppChainCommand(args);
        }

        private void OnAppChainEvent(object sender, AppChainEventArgs args)
        {
            if (args.Method == "Create")
            {
                appchainMgr.OnAppChainCreated(args);
            }
            else if (args.Method == "ChangeValidators")
            {
                // 通知正在运行的应用链对象，更新共识节点公钥
                if (ZoroSystem.GetAppChainSystem(args.State.Hash, out ZoroSystem system))
                {
                    system.Blockchain.Tell(new Blockchain.ChangeValidators { Validators = args.State.StandbyValidators });
                }
            }
            else if (args.Method == "ChangeSeedList")
            {
                // 通知正在运行的应用链对象，更新种子节点地址
                if (ZoroSystem.GetAppChainSystem(args.State.Hash, out ZoroSystem system))
                {
                    system.LocalNode.Tell(new LocalNode.ChangeSeedList { SeedList = args.State.SeedList });
                }
            }
        }
    }
}
