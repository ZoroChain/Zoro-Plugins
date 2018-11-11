using Microsoft.AspNetCore.Http;
using Zoro.IO.Data.LevelDB;
using Zoro.IO.Json;
using Zoro.Network.RPC;
using Zoro.Network.P2P;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using Akka.Actor;

namespace Zoro.Plugins
{
    public class LogReader : Plugin, IRpcPlugin
    {
        private ConcurrentDictionary<UInt160, IActorRef> loggers = new ConcurrentDictionary<UInt160, IActorRef>();
        private ConcurrentDictionary<UInt160, DB> dbs = new ConcurrentDictionary<UInt160, DB>();

        public override string Name => "ApplicationLogs";

        public LogReader(PluginManager pluginMgr)
            : base(pluginMgr)
        {
        }

        public override void Dispose()
        {
            foreach (var logger in loggers.Values)
            {
                PluginMgr.System.ActorSystem.Stop(logger);
            }
        }

        public override bool OnMessage(object message)
        {
            if (message is ZoroSystem.ChainStarted evt)
            {
                CreateLogger(evt.ChainHash);
                return true;
            }
            return false;
        }

        private void CreateLogger(UInt160 chainHash)
        {
            if (!loggers.ContainsKey(chainHash))
            {
                string path = string.Format(Settings.Default.Path, Message.Magic.ToString("X8"), chainHash.ToArray().Reverse().ToHexString());
                DB db = DB.Open(Path.GetFullPath(path), new Options { CreateIfMissing = true });

                IActorRef logger = PluginMgr.System.ActorSystem.ActorOf(Logger.Props(PluginMgr.System.Blockchain, db));
                loggers.TryAdd(chainHash, logger);
                dbs.TryAdd(chainHash, db);
            }
        }

        private bool TryGetDB(JObject param, out DB db)
        {
            UInt160 chainHash;
            string hashString = param.AsString();
            if (hashString.Length == 40 || (hashString.StartsWith("0x") && hashString.Length == 42))
            {
                chainHash = UInt160.Parse(param.AsString());
            }
            else
            {
                chainHash = UInt160.Zero;
            }

            return dbs.TryGetValue(chainHash, out db);
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (method != "getapplicationlog") return null;
            if (!TryGetDB(_params[0], out DB db)) return null;
            if (db.IsDisposed) return null;
            UInt256 hash = UInt256.Parse(_params[1].AsString());
            if (!db.TryGet(ReadOptions.Default, hash.ToArray(), out Slice value))
                throw new RpcException(-100, "Unknown transaction");
            return JObject.Parse(value.ToString());
        }

    }
}
