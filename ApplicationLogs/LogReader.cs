using Microsoft.AspNetCore.Http;
using Zoro.IO.Data.LevelDB;
using Zoro.IO.Json;
using Zoro.AppChain;
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

        // 处理ZoroSystem发来的消息通知
        public override bool OnMessage(object message)
        {
            if (message is ZoroSystem.ChainStarted started)
            {
                // 每次有一条链启动时，打开对应的ApplicationLog数据库文件
                CreateLogger(started.ChainHash);
                return true;
            }
            else if (message is ZoroSystem.ChainStopped stopped)
            {
                // 每次有一条链关闭时，结束对应的Actor对象，并关闭对应的ApplicationLog数据库
                StopLogger(stopped.ChainHash);
                return true;
            }
            return false;
        }

        private void CreateLogger(UInt160 chainHash)
        {
            if (!loggers.ContainsKey(chainHash))
            {
                // 根据链的Hash，获取对应的ZoroSystem对象
                ZoroSystem system = AppChainManager.Singleton.GetZoroSystem(chainHash);
                if (system != null)
                {
                    // 用MagicNumber加上ChainHash作为ApplicationLog数据库的文件名
                    string path = string.Format(Settings.Default.Path, Message.Magic.ToString("X8"), chainHash.ToArray().Reverse().ToHexString());
                    DB db = DB.Open(Path.GetFullPath(path), new Options { CreateIfMissing = true });

                    // 创建Actor对象来处理Blockchain发来的消息通知
                    IActorRef logger = PluginMgr.System.ActorSystem.ActorOf(Logger.Props(system.Blockchain, db), "ApplicationLogs_" + chainHash.ToString());

                    // 记录创建的Actor和Db对象
                    loggers.TryAdd(chainHash, logger);
                    dbs.TryAdd(chainHash, db);
                }
            }
        }

        private void StopLogger(UInt160 chainHash)
        {
            if (loggers.TryRemove(chainHash, out IActorRef logger))
            {
                // 停止Actor对象
                PluginMgr.System.ActorSystem.Stop(logger);
            }

            dbs.TryRemove(chainHash, out DB db);
        }

        // 根据ChainHash，获取对应的Db对象
        private bool TryGetDB(JObject param, out DB db)
        {
            string hashString = param.AsString();

            if (AppChainManager.Singleton.TryParseChainHash(hashString, out UInt160 chainHash))
            {
                return dbs.TryGetValue(chainHash, out db);
            }
            db = null;
            return false;
        }
        
        // 第一个参数是ChainHash，第二个参数是交易Id
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
