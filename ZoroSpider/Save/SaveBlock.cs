using System.Net;
using System.Collections.Generic;
using System;
using MySql.Data.MySqlClient;
using Zoro.IO.Json;

namespace Zoro.Plugins
{
    class SaveBlock : SaveBase
    {
        private SaveTransaction trans;

        public SaveBlock(UInt160 chainHash)
            : base(chainHash)
        {
            InitDataTable(TableType.Block);

            trans = new SaveTransaction(chainHash);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.Block, name);
            return true;
        }

        public void Save(MySqlConnection conn, JObject jObject, uint height)
        {
            JObject result = new JObject();
            result["hash"] = jObject["hash"];
            result["size"] = jObject["size"];
            result["version"] = jObject["version"];
            result["previousblockhash"] = jObject["previousblockhash"];
            result["merkleroot"] = jObject["merkleroot"];
            result["time"] = jObject["time"];
            result["index"] = jObject["index"];
            result["nonce"] = jObject["nonce"];
            result["nextconsensus"] = jObject["nextconsensus"];
            result["script"] = jObject["script"];

            List<string> slist = new List<string>();
            slist.Add(SpiderHelper.getString(jObject["hash"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["size"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["version"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["previousblockhash"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["merkleroot"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["time"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["index"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["nonce"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["nextconsensus"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["script"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["tx"].ToString()));
            slist.Add((jObject["tx"] as JArray).Count.ToString());

            if (ChainSpider.checkHeight == int.Parse(SpiderHelper.getString(jObject["index"].ToString()))) {
                Dictionary<string, string> where = new Dictionary<string, string>();
                where.Add("indexx", SpiderHelper.getString(jObject["index"].ToString()));
                MysqlConn.Delete(conn, DataTableName, where);
            }
            MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);
            
            uint blockTime = uint.Parse(SpiderHelper.getString(result["time"].ToString()));

            int numTx = 0;
            foreach (var tx in (jObject["tx"] as JArray))
            {
                trans.Save(conn, tx, height, blockTime);
                numTx++;
            }

            LogConfig.Log($"BlockSaved {ChainHash} height:{height} tx:{numTx}", LogConfig.LogLevel.Warning, ChainHash.ToString());
        }
    }
}
