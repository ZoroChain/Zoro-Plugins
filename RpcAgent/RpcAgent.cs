using System;
using System.Linq;
using Cowboy.Sockets;
using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Network.RPC;
using Zoro.Network.P2P;

namespace Zoro.Plugins
{
    public class RpcAgent : Plugin
    {
        private TcpSocketServer server;
        private RpcHandler handler;

        public RpcAgent(PluginManager pluginMgr)
            : base(pluginMgr)
        {
            if (pluginMgr.System != ZoroSystem.Root)
                return;

            handler = new RpcHandler();

            var config = new TcpSocketServerConfiguration();

            server = new TcpSocketServer(Settings.Default.Port, config);
            server.ClientConnected += server_ClientConnected;
            server.ClientDisconnected += server_ClientDisconnected;
            server.ClientDataReceived += server_ClientDataReceived;
            server.Listen();

            Console.WriteLine(string.Format("Rpc agent is running on port {0}.", Settings.Default.Port));
        }

        public override void Dispose()
        {
            server?.Shutdown();
        }

        void server_ClientConnected(object sender, TcpClientConnectedEventArgs e)
        {
            //Console.WriteLine(string.Format("TCP client {0} has connected {1}.", e.Session.RemoteEndPoint, e.Session));
        }

        void server_ClientDisconnected(object sender, TcpClientDisconnectedEventArgs e)
        {
            //Console.WriteLine(string.Format("TCP client {0} has disconnected.", e.Session));
        }

        void server_ClientDataReceived(object sender, TcpClientDataReceivedEventArgs e)
        {
            ; byte[] data = e.Data.Skip(e.DataOffset).Take(e.DataLength).ToArray();
            Message msg = data.AsSerializable<Message>();

            if (msg.Command == "rpc-request")
            {
                RpcRequestPayload payload = msg.Payload.AsSerializable<RpcRequestPayload>();

                JObject result = handler.HandleRequest(payload);

                SendRpcResponse(e.Session, payload.Guid, result);
            }
        }

        private void SendRpcResponse(TcpSocketSession session, Guid guid, JObject result)
        {
            RpcResponsePayload payload = new RpcResponsePayload
            {
                Guid = guid,
                Result = result
            };

            Message msg = Message.Create("rpc-response", payload.ToArray());
            server.SendTo(session, msg.ToArray());
        }
    }
}
