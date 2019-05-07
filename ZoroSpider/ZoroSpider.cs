using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Text;
using Zoro.IO.Json;
using Zoro.Ledger;

namespace Zoro.Plugins
{
    class Spider : UntypedActor
    {
        private Dictionary<UInt160, TransactionDal> tranList = new Dictionary<UInt160, TransactionDal>();
        private SaveBlock saveBlock = null;
        private SaveNotify saveNotify = null;
        private SaveTransaction saveTransaction = null;

        private ZoroSpider spider = null;
        private UInt160 chainHash = null;

        private Spider(ZoroSpider zoroSpider, IActorRef blockchain, UInt160 chainHash)
        {
            this.spider = zoroSpider;
            this.chainHash = chainHash;

            saveBlock = new SaveBlock(chainHash);
            saveNotify = new SaveNotify(chainHash);
            saveTransaction = new SaveTransaction(chainHash);
            // 注册Blockchain的分发事件
            blockchain.Tell(new Blockchain.Register());
        }

        protected override void PostStop()
        {
            spider.RemoveLogger(chainHash);
        }

        protected override void OnReceive(object message)
        {
            TransactionDal tran = null;
            if (message is Blockchain.ApplicationExecuted log)
            {
                tran.BeginTransaction();
                foreach (var notify in log.ExecutionResults)
                {
                    saveNotify.Save(tran.conn, JObject.Parse(notify.ToString()));
                }
                tran.CommitTransaction();
                tran.DisposeTransaction();
            }
            else if (message is Blockchain.PersistCompleted block)
            {
                tran.BeginTransaction();
                JObject jObject = block.Block.ToJson();
                saveBlock.Save(tran.conn, jObject, block.Block.Index);
                tran.CommitTransaction();
                tran.DisposeTransaction();
            }
        }

        public static Props Props(ZoroSpider spider, IActorRef blockchain, UInt160 chainHash)
        {
            return Akka.Actor.Props.Create(() => new Spider(spider, blockchain, chainHash));
        }
    }
}
