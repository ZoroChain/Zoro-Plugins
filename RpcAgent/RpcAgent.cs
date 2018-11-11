using System;
using System.Linq;
using Cowboy.Sockets;
using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Network.RPC;
using Zoro.Network.P2P;
using Zoro.Wallets;

namespace Zoro.Plugins
{
    public class RpcAgent : Plugin
    {
        private TcpSocketServer server;
        private RpcHandler handler;

        public RpcAgent(PluginManager pluginMgr)
            : base(pluginMgr)
        {
            handler = new RpcHandler();

            var config = new TcpSocketServerConfiguration();
            config.AllowNatTraversal = false;

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

        public override void SetWallet(Wallet wallet)
        {
            handler.SetWallet(wallet);
        }

        void server_ClientConnected(object sender, TcpClientConnectedEventArgs e)
        {
            int count = server.SessionCount;
            if (count >= Settings.Default.MaxConnections)
            {
                throw new InvalidOperationException($"The maximum number of connections has been exceeded {count}.");
            }
            else
            {
                //Console.WriteLine(string.Format("TCP client {0} has connected {1}.", e.Session.RemoteEndPoint, e.Session));
            }
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

                ProcessRpcReuqest(e, payload);
            }
        }

        private void ProcessRpcReuqest(TcpClientDataReceivedEventArgs e, RpcRequestPayload payload)
        {
            try
            {
                JObject result = HandleRequest(payload);

                SendRpcResponse(e.Session, payload.Guid, result);
            }
            catch (Exception exception)
            {
                SendRpcException(e.Session, payload.Guid, exception);
            }
        }


        public JObject HandleRequest(RpcRequestPayload payload)
        {
            JArray _params = null;

            try
            {
                JObject parameters = JArray.Parse(payload.Params);

                if (parameters is JArray)
                {
                    _params = (JArray)parameters;
                }
            }
            catch
            {

            }

            if (_params == null)
            {
                throw new RpcException(-32602, "Error occurred when parsing parameters.");
            }

            return handler.Process(payload.Method, _params);
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

        private void SendRpcException(TcpSocketSession session, Guid guid, Exception exception)
        {
            RpcExceptionPayload payload = RpcExceptionPayload.Create(guid, exception);

            Message msg = Message.Create("rpc-error", payload.ToArray());
            server.SendTo(session, msg.ToArray());
        }
    }
}
