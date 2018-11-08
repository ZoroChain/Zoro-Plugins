using Akka.Actor;
using Zoro.Ledger;
using Zoro.Wallets;
using Zoro.Network.P2P;
using Zoro.Cryptography.ECC;
using System;

namespace Zoro.Plugins
{
    internal class EventHandler
    {
        private AppChainPlugin plugin;
        private int port = Settings.Default.Port;
        private int wsport = Settings.Default.WsPort;
        private bool saveJson = Settings.Default.SaveJson;
        private string[] keyNames = Settings.Default.KeyNames;

        public EventHandler(AppChainPlugin plugin)
        {
            this.plugin = plugin;
        }

        public void OnAppChainStarted(UInt160 chainHash, int port, int wsport)
        {
            if (this.port != 0 && port >= this.port)
                this.port = port + 1;

            if (this.wsport != 0 && wsport >= this.wsport)
                this.wsport = wsport + 1;
        }

        public void OnAppChainEvent(AppChainEventArgs args)
        {
            if (args.Method == "Create")
            {
                OnAppChainCreated(args);
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

        private void OnAppChainCreated(AppChainEventArgs args)
        {
            if (!CheckAppChainPort())
            {
                plugin.Log($"failed to check appchain port, name={args.State.Name} hash={args.State.Hash}");
                return;
            }

            if (!CheckAppChainName(args.State.Name.ToLower()))
            {
                plugin.Log($"failed to check appchain key name, name={args.State.Name} hash={args.State.Hash}");
                return;
            }
                
            string hashString = args.State.Hash.ToString();

            int listenPort = port;
            int listenWsPort = wsport;

            bool succeed = ZoroSystem.Root.StartAppChain(hashString, port, wsport);

            if (succeed)
            {
                plugin.Log($"starting appchain, name={args.State.Name} hash={args.State.Hash}");
            }
            else
            {
                plugin.Log($"failed to start appchain, name={args.State.Name} hash={args.State.Hash}");
            }

            bool startConsensus = false;

            if (plugin.Wallet != null)
            {
                startConsensus = CheckStartConsensus(args.State.StandbyValidators);

                if (startConsensus)
                {
                    ZoroSystem.Root.StartAppChainConsensus(hashString, plugin.Wallet);

                    plugin.Log($"starting consensus service, name={args.State.Name} hash={args.State.Hash}");
                }
            }

            if (succeed && saveJson)
            {
                if (AppChainsSettings.Default.AddSettings(args.State.Hash, (ushort)listenPort, (ushort)listenWsPort, startConsensus))
                {
                    AppChainsSettings.Default.SaveJsonFile();

                    plugin.Log($"save to json file, name={args.State.Name} hash={args.State.Hash} port={listenPort} wsport={listenWsPort}");
                }
                else
                {
                    plugin.Log($"already exists in json file, name={args.State.Name} hash={args.State.Hash} port={listenPort} wsport={listenWsPort}");
                }
            }
        }

        private bool CheckAppChainPort()
        {
            return Settings.Default.Port != 0 || Settings.Default.WsPort != 0;
        }

        private bool CheckAppChainName(string name)
        {
            foreach (string key in keyNames)
            {
                if (name.Contains(key))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckStartConsensus(ECPoint[] Validators)
        {
            for (int i = 0; i < Validators.Length; i++)
            {
                WalletAccount account = plugin.Wallet.GetAccount(Validators[i]);
                if (account?.HasKey == true)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
