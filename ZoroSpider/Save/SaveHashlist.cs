using MySql.Data.MySqlClient;
using Zoro.IO.Json;

namespace Zoro.Plugins
{
    class SaveHashlist : SaveBase
    {
        public SaveHashlist()
            : base(null)
        {
            InitDataTable(TableType.Hash_List);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.Hash_List, name);
            return true;
        }

        public void Save(MySqlConnection conn, JObject jObject)
        {
            MysqlConn.SaveAndUpdataHashList(conn, DataTableName, SpiderHelper.getString(jObject["hashlist"].ToString()));
        }
    }
}
