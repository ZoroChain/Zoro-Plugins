using Akka.Actor;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Zoro.IO.Data.LevelDB;
using Zoro.IO.Json;
using Zoro.Network.RPC;
using Zoro.Network.P2P;
using System.IO;
using Zoro.Ledger;
using Zoro.Persistence;
using Zoro.Network.P2P.Payloads;
using System.Text.RegularExpressions;

namespace Zoro.Plugins
{
    public class ZoroSpider : Plugin
    {
        private ConcurrentDictionary<UInt160, IActorRef> spiders = new ConcurrentDictionary<UInt160, IActorRef>();
        private ConcurrentDictionary<UInt160, DB> dbs = new ConcurrentDictionary<UInt160, DB>();

        public override string Name => "ZoroSpider";

        public ZoroSpider(PluginManager pluginMgr)
            : base(pluginMgr)
        {          
        }

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
            System.Net.ServicePointManager.DefaultConnectionLimit = 512;

            AppDomain.CurrentDomain.UnhandledException += LogConfig.CurrentDomain_UnhandledException;

            ProjectInfo.head();

            MysqlConn.conf = Settings.Default.MysqlConfig;
            MysqlConn.dbname = Settings.Default.DataBaseName;

            ProjectInfo.tail();
        }

        public override void Dispose()
        {
            var actors = spiders.Select(p => p.Value).ToArray();
            foreach (var actor in actors)
            {
                ZoroChainSystem.Singleton.ActorSystem.Stop(actor);
            }
        }

        public override bool OnMessage(object message)
        {
            if (message is ZoroSystem.ChainStarted started)
            {
                // 每次有一条链启动时，打开爬虫文件
                CreateSpider(started.ChainHash);
            }
            if (message is ConcurrentDictionary<UInt160, DB> dbs) {
                SaveNotify.setDBs(dbs);
            }
            return false;
        }

        private void CreateSpider(UInt160 chainHash) {
            if (!spiders.ContainsKey(chainHash)) {
                Blockchain blockchain = ZoroChainSystem.Singleton.GetBlockchain(chainHash);
                if (blockchain != null) {                    
                    ChainSpider spider = new ChainSpider(chainHash, blockchain);
                    spider.Start(-1);
                }
            }

            TransactionDal tran = new TransactionDal();
            try
            {                
                tran.BeginTransaction();
                AppChainListSpider appChainListSpider = new AppChainListSpider();
                appChainListSpider.Start(tran.conn);
                tran.CommitTransaction();
            }
            catch (Exception e) {
                tran.RollbackTransaction();
                throw e;
            }           
        }
    }
}
