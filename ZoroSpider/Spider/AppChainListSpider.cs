using System;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Zoro.Ledger;
using MySql.Data.MySqlClient;
using Zoro.IO.Json;
using Zoro.Network.RPC;

namespace Zoro.Plugins
{
    class AppChainListSpider : IDisposable
    {
        private Task task;
        private WebClient wc = new WebClient();
        private SaveHashlist hashlist = new SaveHashlist();
        private SaveAppChain appchain = new SaveAppChain();
        private List<UInt160> currentList = new List<UInt160>();

        public void Start(MySqlConnection conn)
        {
            Process(conn);
        }

        public void Dispose()
        {
            task.Dispose();
        }

        private List<UInt160> GetAppChainHashList(MySqlConnection conn)
        {
            try
            {
                JObject json = new JObject();
                IEnumerable<AppChainState> appchains = Blockchain.Root.Store.GetAppChains().Find().OrderBy(p => p.Value.Timestamp).Select(p => p.Value);
                json["hashlist"] = new JArray(appchains.Select(p => (JObject)p.Hash.ToString()));

                if (json != null)
                {
                    JObject hlist = json["hashlist"];
                    if (hlist != null && hlist is JArray jlist)
                    {
                        hashlist.Save(conn, json);

                        List<UInt160> list = new List<UInt160>();
                        foreach (var item in jlist)
                        {
                            list.Add(UInt160.Parse(item.ToString()));
                        }
                        return list;
                    }
                }
            }
            catch (Exception e)
            {
                LogConfig.Log($"error occured when call getappchainhashlist, reason:{e.Message}", LogConfig.LogLevel.Error);
            }

            return null;
        }

        private void GetAppChainState(MySqlConnection conn, UInt160 chainHash)
        {
            try
            {
                AppChainState state = Blockchain.Root.Store.GetAppChains().TryGet(chainHash);
                JObject json = state?.ToJson() ?? throw new RpcException(-100, "Unknown appchain");
                Blockchain blockchain = ZoroChainSystem.Singleton.GetBlockchain(chainHash);
                json["blockcount"] = blockchain != null ? blockchain.Height : 0;
                if (json != null)
                {
                    appchain.Save(conn, json);
                }
            }
            catch (Exception e)
            {
                LogConfig.Log($"error occured when call getappchainstate with chainHash ={chainHash}, reason:{e.Message}", LogConfig.LogLevel.Error);
            }
        }

        private void Process(MySqlConnection conn)
        {
            List<UInt160> list = GetAppChainHashList(conn);

            if (list != null)
            {
                UInt160[] array = list.Where(p => !currentList.Contains(p)).ToArray();

                if (array.Length > 0)
                {
                    currentList.AddRange(array);

                    foreach (var hash in array)
                    {
                        GetAppChainState(conn, hash);
                    }
                }
            }
        }
    }
}
