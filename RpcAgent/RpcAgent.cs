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
            server.ClientConnected += OnClientConnected;
            server.ClientDisconnected += OnClientDisconnected;
            server.ClientDataReceived += OnClientDataReceived;
            server.Listen();

            Console.WriteLine(string.Format("Rpc agent is running on port {0}.", Settings.Default.Port));
        }
        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public override void Dispose()
        {
            server?.Shutdown();
        }

        void Log(string message)
        {
            PluginMgr.Log(nameof(RpcAgent), LogLevel.Info, message, UInt160.Zero);
        }

        void OnClientConnected(object sender, TcpClientConnectedEventArgs e)
        {
            int count = server.SessionCount;
            if (count >= Settings.Default.MaxConnections)
            {
                throw new InvalidOperationException($"The maximum number of connections has been exceeded {count}.");
            }
            else
            {
                if (Settings.Default.DebugMode)
                    Log(string.Format("TCP client {0} has connected {1}.", e.Session.RemoteEndPoint, e.Session.LocalEndPoint));
            }
        }

        void OnClientDisconnected(object sender, TcpClientDisconnectedEventArgs e)
        {
            if (Settings.Default.DebugMode)
                Log(string.Format("TCP client {0} has disconnected.", e.Session));
        }

        void OnClientDataReceived(object sender, TcpClientDataReceivedEventArgs e)
        {
            if (Settings.Default.DebugMode)
                Log($"RpcAgent recv data, length:{e.DataLength}");

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

            JObject result = PluginManager.Singleton.ProcessRpcMethod(null, payload.Method, _params);

            if (result == null)
                result = handler.Process(payload.Method, _params);

            return result;
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

            if (Settings.Default.DebugMode)
                Log($"RpcAgent send data, length:{msg.Size}");
        }

        private void SendRpcException(TcpSocketSession session, Guid guid, Exception exception)
        {
            RpcExceptionPayload payload = RpcExceptionPayload.Create(guid, exception);

            Message msg = Message.Create("rpc-error", payload.ToArray());
            server.SendTo(session, msg.ToArray());

            if (Settings.Default.DebugMode)
                Log($"RpcAgent send data, length:{msg.Size}");
        }
    }
}
