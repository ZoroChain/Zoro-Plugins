using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Data;
using Zoro.IO.Json;

namespace Zoro.Plugins
{
    class SaveAddress : SaveBase
    {
        public SaveAddress(UInt160 chainHash)
            : base(chainHash)
        {
            InitDataTable(TableType.Address);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.Address, name);
            return true;
        }

        public void Save(MySqlConnection conn, JObject jObject, uint blockHeight, uint blockTime)
        {           
            Dictionary<string, string> selectWhere = new Dictionary<string, string>();
            selectWhere.Add("addr", SpiderHelper.getString(jObject["address"].ToString()));
            DataTable dt = MysqlConn.ExecuteDataSet(DataTableName, selectWhere).Tables[0];
            if (dt.Rows.Count != 0)
            {
                Dictionary<string, string> dirs = new Dictionary<string, string>();
                dirs.Add("lastuse", blockTime.ToString());
                dirs.Add("txcount", (int.Parse(dt.Rows[0]["txcount"].ToString()) + 1) + "");
                Dictionary<string, string> where = new Dictionary<string, string>();
                where.Add("addr", dt.Rows[0]["addr"].ToString());
                MysqlConn.Update(conn, DataTableName, dirs, where);
            }
            else
            {
                List<string> slist = new List<string>();
                slist.Add(SpiderHelper.getString(jObject["address"].ToString()));
                slist.Add(blockTime.ToString());
                slist.Add(blockTime.ToString());
                slist.Add("1");
                MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);
            }
        }
    }
}
