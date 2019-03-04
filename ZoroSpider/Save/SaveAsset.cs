using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Data;
using Zoro.IO.Json;

namespace Zoro.Plugins
{
    class SaveAsset : SaveBase
    {
        public SaveAsset(UInt160 chainHash)
            : base(chainHash)
        {
            InitDataTable("asset");
        }

        public override bool CreateTable(string name)
        {
            return true;
        }

        public void Save(MySqlConnection conn, JObject jObject, string path)
        {
            JObject result = new JObject();
            result["version"] = jObject["version"];
            result["id"] = jObject["txid"];
            result["type"] = jObject["asset"]["type"];
            result["name"] = jObject["asset"]["name"];
            result["amount"] = jObject["asset"]["amount"];
            result["available"] = 1;
            result["precision"] = jObject["asset"]["precision"];
            result["owner"] = jObject["asset"]["owner"];
            result["admin"] = jObject["asset"]["admin"];
            result["issuer"] = 1;
            result["expiration"] = 0;
            result["frozen"] = 0;

            List<string> slist = new List<string>();
            slist.Add(SpiderHelper.getString(result["version"].ToString()));
            slist.Add(SpiderHelper.getString(result["id"].ToString()));
            slist.Add(SpiderHelper.getString(result["type"].ToString()));
            slist.Add(SpiderHelper.getString(result["name"].ToString()));
            slist.Add(SpiderHelper.getString(result["amount"].ToString()));
            slist.Add(SpiderHelper.getString(result["available"].ToString()));
            slist.Add(SpiderHelper.getString(result["precision"].ToString()));
            slist.Add(SpiderHelper.getString(result["owner"].ToString()));
            slist.Add(SpiderHelper.getString(result["admin"].ToString()));
            slist.Add(SpiderHelper.getString(result["issuer"].ToString()));
            slist.Add(SpiderHelper.getString(result["expiration"].ToString()));
            slist.Add(SpiderHelper.getString(result["frozen"].ToString()));
           
            {
                MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);
            }

            LogConfig.Log($"SaveAsset {ChainHash} {result["name"]}", LogConfig.LogLevel.Info, ChainHash.ToString());
        }
    }
}
