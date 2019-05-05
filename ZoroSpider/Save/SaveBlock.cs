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
            List<string> slist = new List<string>();
            slist.Add(SpiderHelper.getString(jObject["hash"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["size"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["version"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["time"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["index"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["script"].ToString()));
            slist.Add((jObject["tx"] as JArray).Count.ToString());

            if (ChainSpider.checkHeight == int.Parse(SpiderHelper.getString(jObject["index"].ToString()))) {
                Dictionary<string, string> where = new Dictionary<string, string>();
                where.Add("indexx", SpiderHelper.getString(jObject["index"].ToString()));
                MysqlConn.Delete(conn, DataTableName, where);
            }
            MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);
            
            uint blockTime = uint.Parse(SpiderHelper.getString(jObject["time"].ToString()));

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
