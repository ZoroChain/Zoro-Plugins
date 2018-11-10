using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections.Generic;

namespace Zoro.Plugins
{
    internal static class ListenPortManager
    {
        private static int port = Settings.Default.Port;
        private static int wsport = Settings.Default.WsPort;

        private static readonly HashSet<int> listeningPorts = new HashSet<int>();
        private static readonly HashSet<int> listeningWsPorts = new HashSet<int>();

        private static readonly HashSet<IPAddress> localAddresses = new HashSet<IPAddress>();

        static ListenPortManager()
        {
            localAddresses.UnionWith(NetworkInterface.GetAllNetworkInterfaces().SelectMany(p => p.GetIPProperties().UnicastAddresses).Select(p => p.Address.Unmap()));
        }

        internal static void OnChainStarted(UInt160 chainHash, int port, int wsport)
        {
            listeningPorts.Add(port);
            listeningWsPorts.Add(wsport);
        }

        internal static IPAddress Unmap(this IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();
            return address;
        }

        internal static bool GetAppChainListenPort(string[] seedList, out int listenPort, out int listenWsPort)
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

        private static int GetFreePort()
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

        private static int GetFreeWsPort()
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

        private static int GetListenPortBySeedList(string[] seedList)
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

        private static IPEndPoint GetIPEndpointFromHostPort(string hostNameOrAddress, int port)
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
    }
}
