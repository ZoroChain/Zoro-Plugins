using Zoro.Ledger;
using Zoro.Wallets;
using Zoro.Cryptography.ECC;

namespace Zoro.Plugins
{
    public class AppChainPlugin : Plugin
    {
        private Wallet wallet;
        private ushort port = Settings.Default.Port;
        private ushort wsport = Settings.Default.WsPort;
        private bool saveJson = Settings.Default.SaveJson;

        public AppChainPlugin(PluginManager pluginMgr)
            : base(pluginMgr)
        {
            if (pluginMgr.System != ZoroSystem.Root)
                return;

            if (port > 0 || wsport > 0)
            {
                Blockchain.AppChainNofity += OnAppChainEvent;
            }
        }

        public override void SetWallet(Wallet wallet)
        {
            this.wallet = wallet;
        }

        private void OnAppChainEvent(object sender, AppChainEventArgs args)
        {
            if (args.Method == "Create")
            {
                string hashString = args.State.Hash.ToString();
                // 启动应用链
                ZoroSystem.Root.StartAppChain(hashString, port, wsport);

                bool startConsensus = false;

                if (wallet != null)
                {
                    startConsensus = checkStartConsensus(args.State.StandbyValidators);

                    if (startConsensus)
                    {
                        ZoroSystem.Root.StartAppChainConsensus(hashString, wallet);
                    }
                }

                if (saveJson)
                {
                    AppChainSettings settings = new AppChainSettings(hashString, port, wsport, startConsensus);

                    AppChainsSettings.Default.Chains.Add(hashString, settings);

                    AppChainsSettings.Default.SaveJsonFile();
                }

                if (port > 0)
                    port++;

                if (wsport > 0)
                    wsport++;
            }
        }

        private bool checkStartConsensus(ECPoint[] Validators)
        {
            for (int i = 0; i < Validators.Length; i++)
            {
                WalletAccount account = wallet.GetAccount(Validators[i]);
                if (account?.HasKey == true)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
