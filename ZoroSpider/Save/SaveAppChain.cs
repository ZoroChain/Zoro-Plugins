using MySql.Data.MySqlClient;
using System.Collections.Generic;
using Zoro.IO.Json;

namespace Zoro.Plugins
{
    class SaveAppChain : SaveBase
    {
        public SaveAppChain()
            : base(null)
        {
            InitDataTable(TableType.Appchainstate);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.Appchainstate, name);
            return true;
        }

        public void Save(MySqlConnection conn, JObject jObject)
        {
			//JObject hashstateresult = new JObject();

			//hashstateresult["result"] = jObject["result"];

			List<string> slist = new List<string>();

			slist.Add(SpiderHelper.getString(jObject["version"].ToString()));
			slist.Add(SpiderHelper.getString(jObject["hash"].ToString()));
			slist.Add(SpiderHelper.getString(jObject["name"].ToString()));
			slist.Add(SpiderHelper.getString(jObject["owner"].ToString()));
			slist.Add(SpiderHelper.getString(jObject["timestamp"].ToString()));
			slist.Add(SpiderHelper.getString(jObject["seedlist"].ToString()));
			slist.Add(SpiderHelper.getString(jObject["validators"].ToString()));
            
            MysqlConn.SaveAndUpdataAppChainState(conn, DataTableName, slist);

            LogConfig.Log($"SaveAppChain {jObject["hash"]} {jObject["name"]}", LogConfig.LogLevel.Info);
        }
    }
}
