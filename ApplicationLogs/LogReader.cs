using Microsoft.AspNetCore.Http;
using Zoro.IO.Data.LevelDB;
using Zoro.IO.Json;
using Zoro.Network.RPC;
using Zoro.Network.P2P;
using System.IO;
using System.Linq;
using Akka.Actor;

namespace Zoro.Plugins
{
    public class LogReader : Plugin, IRpcPlugin
    {
        private readonly DB db;
        private IActorRef logger;

        public override string Name => "ApplicationLogs";

        public LogReader(PluginManager pluginMgr)
            : base(pluginMgr)
        {
            string path = string.Format(Settings.Default.Path, Message.Magic.ToString("X8"), pluginMgr.ChainHash.ToArray().Reverse().ToHexString());
            db = DB.Open(Path.GetFullPath(path), new Options { CreateIfMissing = true });

            logger = pluginMgr.System.ActorSystem.ActorOf(Logger.Props(pluginMgr.System.Blockchain, db));
        }

        public override void Dispose()
        {
            PluginMgr.System.ActorSystem.Stop(logger);
            db.Dispose();
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (method != "getapplicationlog") return null;
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            if (!db.TryGet(ReadOptions.Default, hash.ToArray(), out Slice value))
                throw new RpcException(-100, "Unknown transaction");
            return JObject.Parse(value.ToString());
        }
    }
}
