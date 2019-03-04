using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Zoro.Plugins
{
    class SaveAddressAsset:SaveBase
    {
        public SaveAddressAsset(UInt160 chainHash)
            : base(chainHash)
        {
            InitDataTable(TableType.Address_Asset);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.Address_Asset, name);
            return true;
        }

        public void Save(MySqlConnection conn, string addr, string asset, string script)
        {
            Dictionary<string, string> selectWhere = new Dictionary<string, string>();
            selectWhere.Add("addr", addr);
            selectWhere.Add("asset", asset);
            DataTable dt = MysqlConn.ExecuteDataSet(DataTableName, selectWhere).Tables[0];
            if (dt.Rows.Count == 0)
            {
                string type = "";
                if (script.EndsWith(SpiderHelper.ZoroNativeNep5Call)) type = "NativeNep5";
                else type = "Nep5";
                List<string> slist = new List<string>();
                slist.Add(addr);
                slist.Add(asset);
                slist.Add(type);
                MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);
            }           
        }
    }
}
