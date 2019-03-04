using System.Data;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using Zoro.IO.Json;

namespace Zoro.Plugins
{
    class SaveUTXO : SaveBase
    {
        public SaveUTXO(UInt160 chainHash)
            : base(chainHash)
        {
            InitDataTable(TableType.UTXO);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.UTXO, name);
            return true;
        }

        public void Save(MySqlConnection conn, JObject jObject, uint blockHeight)
        {
            if (null == jObject["vout"]) {
                return;
            }
            foreach (JObject vout in jObject["vout"] as JArray)
            {
                JObject result = new JObject();
                result["addr"] = vout["address"];
                result["txid"] = jObject["txid"];
                result["n"] = vout["n"];
                result["asset"] = vout["asset"];
                result["value"] = vout["value"];
                result["createHeight"] = blockHeight;
                result["used"] = 0;
                result["useHeight"] = 0;
                result["claimed"] = "";

                List<string> slist = new List<string>();
                slist.Add(SpiderHelper.getString(result["addr"].ToString()));
                slist.Add(SpiderHelper.getString(result["txid"].ToString()));
                slist.Add(SpiderHelper.getString(result["n"].ToString()));
                slist.Add(SpiderHelper.getString(result["asset"].ToString()));
                slist.Add(SpiderHelper.getString(result["value"].ToString()));
                slist.Add(SpiderHelper.getString(result["createHeight"].ToString()));
                slist.Add(SpiderHelper.getString(result["used"].ToString()));
                slist.Add(SpiderHelper.getString(result["useHeight"].ToString()));
                slist.Add(SpiderHelper.getString(result["claimed"].ToString()));

                //Dictionary<string, string> dictionary = new Dictionary<string, string>();
                //dictionary.Add("txid", result["txid"].ToString());
                //dictionary.Add("createHeight", blockHeight.ToString());
                //bool exist = MysqlConn.CheckExist(DataTableName, dictionary);
                //if (!exist)  
                if (ChainSpider.checkHeight == int.Parse(SpiderHelper.getString(result["createHeight"].ToString())))
                {
                    Dictionary<string, string> where = new Dictionary<string, string>();
                    where.Add("addr", SpiderHelper.getString(result["addr"].ToString()));
                    where.Add("createHeight", SpiderHelper.getString(result["createHeight"].ToString()));
                    MysqlConn.Delete(conn,DataTableName, where);
                }
                {
                    MysqlConn.ExecuteDataInsert(conn,DataTableName, slist);
                }
                //var utxoPath = "utxo" + Path.DirectorySeparatorChar + result["txid"] + "_" + result["n"] + "_" + result["addr"] + ".txt";
                //File.Delete(utxoPath);
                //File.WriteAllText(utxoPath, result.ToString(), Encoding.UTF8);
            }
            foreach (JObject vin in jObject["vin"] as JArray)
            {
                ChangeUTXO(conn, SpiderHelper.getString(vin["txid"].ToString()), SpiderHelper.getString(vin["vout"].ToString()), blockHeight);
            }
        }

        public void ChangeUTXO(MySqlConnection conn, string txid, string voutNum, uint blockHeight)
        {
            Dictionary<string, string> dirs = new Dictionary<string, string>();
            dirs.Add("used", "1");
            dirs.Add("useHeight", blockHeight.ToString());
            Dictionary<string, string> where = new Dictionary<string, string>();
            where.Add("txid", txid);
            where.Add("n", voutNum);
            MysqlConn.Update(conn, DataTableName, dirs, where);

            //JObject result = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            //result["used"] = 1;
            //result["useHeight"] = Helper.blockHeight;
            //File.WriteAllText(path, result.ToString(), Encoding.UTF8);
        }
    }
}
