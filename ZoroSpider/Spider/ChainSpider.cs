using Akka.Actor;
using MySql.Data.MySqlClient;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Zoro.IO.Json;
using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using Zoro.Persistence;

namespace Zoro.Plugins
{
    class ChainSpider : UntypedActor, IDisposable
    {
        private Task task;
        private SaveBlock block;
        private SaveChainListHeight listHeight;
        private Blockchain blockchain;
        private TransactionDal tran;

        private UInt160 chainHash;
        private uint currentHeight = 0;

        private uint retryNum = 0;

        public static uint checkHeight = 0;

        public ChainSpider(UInt160 chainHash, Blockchain blockChain)
        {
            this.chainHash = chainHash;
            this.blockchain = blockChain;
            block = new SaveBlock(chainHash);
            listHeight = new SaveChainListHeight(chainHash);
        }

        public void Start(int startHeight)
        {
            this.currentHeight = startHeight >= 0 ? (uint)startHeight : listHeight.getHeight(chainHash.ToString());
            checkHeight = currentHeight;

            LogConfig.Log($"Starting chain spider {chainHash} {currentHeight}", LogConfig.LogLevel.Warning);

            task = Task.Factory.StartNew(() =>
            {
                Process();
            });
        }

        public void Dispose()
        {
            task.Dispose();
        }

        private uint GetBlockCount()
        {
            try
            {
                return blockchain.Height;
            }
            catch (Exception e)
            {
                LogConfig.Log($"error occured when call getblockcount {chainHash}, reason:{e.Message}", LogConfig.LogLevel.Error);
            }
            return 0;
        }

        private uint GetBlock(uint height)
        {
            try
            {
                Block block = blockchain.Store.GetBlock(height);
                JObject json = block.ToJson();
                json["confirmations"] = blockchain.Height - block.Index + 1;
                UInt256 hash = blockchain.Store.GetNextBlockHash(block.Hash);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();

                tran = new TransactionDal();
                tran.BeginTransaction();
                {
                    this.block.Save(tran.conn, json, height);
                    //每获取一个块做一次高度记录，方便下次启动时做开始高度
                    listHeight.Save(tran.conn, chainHash.ToString(), height.ToString());
                }
                tran.CommitTransaction();
                tran.DisposeTransaction();
                return height + 1;
            }
            catch (MySqlException e)
            {
                retryNum++;
                LogConfig.Log($"error occured when call getblock {height} {chainHash}, reason:{e.Message}", LogConfig.LogLevel.Error);
                tran.RollbackTransaction();
                if (retryNum > 1) {
                    throw e;
                }
                return GetBlock(height);
            }
        }

        private void Process()
        {
            while (true)
            {
                uint blockCount = GetBlockCount();

                while (currentHeight < blockCount)
                {
                    currentHeight = GetBlock(currentHeight);
                }
            }
        }

        protected override void OnReceive(object message)
        {
            // 处理Blockchain发来的消息通知
            if (message is Blockchain.PersistCompleted e)
            {
                //JObject jObject = e.Block.ToJson();
                //block.Save();                //JObject jObject = e.Block.ToJson();
                //block.Save();
            }
            if (message is Blockchain.ApplicationExecuted log)
            {
                
            }
        }
    }
}
