using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Data;
using Zoro.IO.Json;

namespace Zoro.Plugins
{
    class SaveNEP5Transfer : SaveBase
    {
        public SaveNEP5Transfer(UInt160 chainHash)
            : base(chainHash)
        {
            InitDataTable(TableType.NEP5Transfer);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.NEP5Transfer, name);
            return true;
        }

        public void Save(MySqlConnection conn, JObject jToken)
        {
            List<string> slist = new List<string>();
            slist.Add(SpiderHelper.getString(jToken["blockindex"].ToString()));
            slist.Add(SpiderHelper.getString(jToken["txid"].ToString()));
            slist.Add(SpiderHelper.getString(jToken["n"].ToString()));
            slist.Add(SpiderHelper.getString(jToken["asset"].ToString()));
            slist.Add(SpiderHelper.getString(jToken["from"].ToString()));
            slist.Add(SpiderHelper.getString(jToken["to"].ToString()));
            slist.Add(SpiderHelper.getString(jToken["value"].ToString()));

            if (ChainSpider.checkHeight == int.Parse(jToken["blockindex"].ToString()))
            {
                Dictionary<string, string> where = new Dictionary<string, string>();
                where.Add("txid", SpiderHelper.getString(jToken["txid"].ToString()));
                where.Add("blockindex", SpiderHelper.getString(jToken["blockindex"].ToString()));
                MysqlConn.Delete(conn, DataTableName, where);
            }
            {
                MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);
            }

            LogConfig.Log($"SaveNEP5Transfer {ChainHash} {jToken["blockindex"]} {jToken["txid"]}", LogConfig.LogLevel.Info, ChainHash.ToString());
        }
    }
}
