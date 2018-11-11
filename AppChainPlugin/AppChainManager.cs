using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using Zoro.Ledger;
using Zoro.Wallets;
using Zoro.Cryptography.ECC;

namespace Zoro.Plugins
{
    internal class AppChainManager
    {
        private AppChainPlugin plugin;

        private int port = Settings.Default.Port;
        private int wsport = Settings.Default.WsPort;
        private string[] keyNames = Settings.Default.KeyNames;

        private readonly HashSet<int> listeningPorts = new HashSet<int>();
        private readonly HashSet<int> listeningWsPorts = new HashSet<int>();

        private static readonly HashSet<IPAddress> localAddresses = new HashSet<IPAddress>();

        static AppChainManager()
        {
            localAddresses.UnionWith(NetworkInterface.GetAllNetworkInterfaces().SelectMany(p => p.GetIPProperties().UnicastAddresses).Select(p => Unmap(p.Address)));
        }

        public AppChainManager(AppChainPlugin plugin)
        {
            this.plugin = plugin;
        }

        public void OnChainStarted(UInt160 chainHash, int port, int wsport)
        {
            listeningPorts.Add(port);
            listeningWsPorts.Add(wsport);

            if (chainHash == UInt160.Zero && CheckAppChainPort())
            {
                IEnumerable<AppChainState> appchains = Blockchain.Root.Store.GetAppChains().Find().OrderBy(p => p.Value.Timestamp).Select(p => p.Value);

                foreach (var state in appchains)
                {
                    if (CheckAppChainName(state.Name.ToLower()))
                    {
                        StartAppChain(state);
                    }
                }
            }
        }

        public void OnAppChainCreated(AppChainEventArgs args)
        {
            if (!CheckAppChainPort())
            {
                plugin.Log($"No appchain will be started because all listen ports are zero, name={args.State.Name} hash={args.State.Hash}");
                return;
            }

            if (!CheckAppChainName(args.State.Name.ToLower()))
            {
                plugin.Log($"The appchain is not in the key name list, name={args.State.Name} hash={args.State.Hash}");
                return;
            }

            StartAppChain(args.State);
        }

        private void StartAppChain(AppChainState state)
        {
            string name = state.Name;
            string hashString = state.Hash.ToString();

            if (!GetAppChainListenPort(state.SeedList, out int listenPort, out int listenWsPort))
            {
                plugin.Log($"The specified listen port is already in used, name={name} hash={hashString}, port={listenPort}");
                return;
            }

            bool succeed = ZoroSystem.Root.StartAppChain(hashString, listenPort, listenWsPort);

            if (succeed)
            {
                plugin.Log($"Starting appchain, name={name} hash={hashString} port={listenPort} wsport={listenWsPort}");
            }
            else
            {
                plugin.Log($"Failed to start appchain, name={name} hash={hashString}");
            }

            bool startConsensus = false;

            if (plugin.Wallet != null)
            {
                startConsensus = CheckStartConsensus(state.StandbyValidators);

                if (startConsensus)
                {
                    ZoroSystem.Root.StartAppChainConsensus(hashString, plugin.Wallet);

                    plugin.Log($"Starting consensus service, name={name} hash={hashString}");
                }
            }
        }

        private static IPAddress Unmap(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();
            return address;
        }

        private bool GetAppChainListenPort(string[] seedList, out int listenPort, out int listenWsPort)
        {
            listenWsPort = GetFreeWsPort();
            listenPort = GetListenPortBySeedList(seedList);

            if (listenPort > 0)
            {
                if (listeningPorts.Contains(listenPort))
                {
                    return false;
                }
            }
            else
            {
                listenPort = GetFreePort();
            }

            return true;
        }

        private int GetFreePort()
        {
            if (port > 0)
            {
                while (listeningPorts.Contains(port))
                {
                    port++;
                }
            }

            return port;
        }

        private int GetFreeWsPort()
        {
            if (wsport > 0)
            {
                while (listeningWsPorts.Contains(wsport))
                {
                    wsport++;
                }
            }

            return wsport;
        }

        private int GetListenPortBySeedList(string[] seedList)
        {
            int listenPort = 0;

            foreach (var hostAndPort in seedList)
            {
                string[] p = hostAndPort.Split(':');
                IPEndPoint seed;
                try
                {
                    seed = GetIPEndpointFromHostPort(p[0], int.Parse(p[1]));
                }
                catch (AggregateException)
                {
                    continue;
                }

                if (localAddresses.Contains(seed.Address))
                {
                    listenPort = seed.Port;

                    break;
                }
            }

            return listenPort;
        }

        private IPEndPoint GetIPEndpointFromHostPort(string hostNameOrAddress, int port)
        {
            if (IPAddress.TryParse(hostNameOrAddress, out IPAddress ipAddress))
                return new IPEndPoint(ipAddress, port);
            IPHostEntry entry;
            try
            {
                entry = Dns.GetHostEntry(hostNameOrAddress);
            }
            catch (SocketException)
            {
                return null;
            }
            ipAddress = entry.AddressList.FirstOrDefault(p => p.AddressFamily == AddressFamily.InterNetwork || p.IsIPv6Teredo);
            if (ipAddress == null) return null;
            return new IPEndPoint(ipAddress, port);
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
