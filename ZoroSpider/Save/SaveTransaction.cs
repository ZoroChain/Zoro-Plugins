using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Threading;
using System.Linq;
using Zoro.IO.Json;
using MySql.Data.MySqlClient;

namespace Zoro.Plugins
{
    class SaveTransaction : SaveBase
    {
        private SaveUTXO utxo;
        private SaveAsset asset;
        private SaveNotify notify;
        private SaveTxScriptMethod txScriptMethod;

        public SaveTransaction(UInt160 chainHash)
            : base(chainHash)
        {
            InitDataTable(TableType.Transaction);

            utxo = new SaveUTXO(chainHash);
            asset = new SaveAsset(chainHash);
            notify = new SaveNotify(chainHash);
            txScriptMethod = new SaveTxScriptMethod(chainHash);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.Transaction, name);
            return true;
        }

        public void Save(MySqlConnection conn, JObject jObject, uint blockHeight, uint blockTime)
        {
            List<string> slist = new List<string>();
            slist.Add(SpiderHelper.getString(jObject["txid"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["size"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["type"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["version"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["attributes"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["sys_fee"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["blockindex"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["gas_limit"] == null?"": jObject["gas_limit"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["gas_price"] == null ? "" : jObject["gas_price"].ToString()));
            slist.Add(UInt160.Parse(StringRemoveZoro(SpiderHelper.getString(jObject["account"].ToString())).HexToBytes().Reverse().ToHexString()).ToAddress());

            if (jObject["script"] != null)
                txScriptMethod.Save(conn, SpiderHelper.getString(jObject["script"].ToString()), blockHeight, SpiderHelper.getString(jObject["txid"].ToString()));

            if (ChainSpider.checkHeight == int.Parse(blockHeight.ToString()))
            {
                Dictionary<string, string> where = new Dictionary<string, string>();
                where.Add("txid", SpiderHelper.getString(jObject["txid"].ToString()));
                where.Add("blockindex", blockHeight.ToString());
                MysqlConn.Delete(conn, DataTableName, where);
            }
            {
                MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);
            }

            LogConfig.Log($"SaveTransaction {ChainHash} {blockHeight}", LogConfig.LogLevel.Info, ChainHash.ToString());

            utxo.Save(conn, jObject, blockHeight);

            //if (SpiderHelper.getString(jObject["type"].ToString()) == "InvocationTransaction")
            //{
            //    notify.Save(conn, jObject, blockHeight, blockTime, SpiderHelper.getString(jObject["script"].ToString()));
            //}
        }

        private string StringRemoveZoro(string hex) {
            string s = hex;
            if (s.StartsWith("0x")) {
                s = s.Substring(2);
            }
            return s;
        }
    }
}
