using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Data;
using Zoro.IO.Json;

namespace Zoro.Plugins
{
    class SaveAddressTransaction : SaveBase
    {
        public SaveAddressTransaction(UInt160 chainHash)
            : base(chainHash)
        {
            InitDataTable(TableType.Address_tx);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.Address_tx, name);
            return true;
        }

        public void Save(MySqlConnection conn, JObject jObject)
        {                      
            List<string> slist = new List<string>();
            slist.Add(SpiderHelper.getString(jObject["address"].ToString()));
            slist.Add(SpiderHelper.getString(jObject["txid"].ToString()));
                

            Dictionary<string, string> where = new Dictionary<string, string>();
            where.Add("addr", SpiderHelper.getString(jObject["address"].ToString()));
            where.Add("txid", SpiderHelper.getString(jObject["txid"].ToString()));
            MysqlConn.Delete(conn, DataTableName, where);

            MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);
        }
    }
}
