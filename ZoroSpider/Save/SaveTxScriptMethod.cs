using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Zoro.Plugins
{
    class SaveTxScriptMethod : SaveBase
    {
        List<ScriptMethod> scriptMethods = new List<ScriptMethod>();

        public SaveTxScriptMethod(UInt160 chainHash)
            : base(chainHash)
        {
            InitDataTable(TableType.Tx_Script_Method);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.Tx_Script_Method, name);
            return true;
        }

        public void Save(MySqlConnection conn, string script, uint blockHeight, string txid)
        {
            if (script == null) return;
            Dictionary<string, string> selectWhere = new Dictionary<string, string>();
            selectWhere.Add("txid", txid);
            DataTable dt = MysqlConn.ExecuteDataSet(DataTableName, selectWhere).Tables[0];
            if (dt.Rows.Count != 0)
            {
                Dictionary<string, string> where = new Dictionary<string, string>();
                where.Add("txid", txid);
                MysqlConn.Delete(conn, DataTableName, where);
            }
            scriptMethods.Clear();
            cutString(script);
            for (int i = 0; i < scriptMethods.Count; i++) {
                List<string> slist = new List<string>();
                slist.Add(txid);
                slist.Add(scriptMethods[i].calltype);
                slist.Add(scriptMethods[i].method);
                slist.Add(scriptMethods[i].contract);
                slist.Add(blockHeight.ToString());
                MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);
            }            
        }

        public void cutString(string sb)
        {
            List<string> list = new List<string>();            
            bool isAppCall = false;
            bool create = false;
            if (sb.LastIndexOf("68") > 0 && sb.Substring(sb.LastIndexOf("68")).Length < 50 && sb.Substring(sb.LastIndexOf("68")).Length % 2 == 0)
            {
                string ss = sb.Substring(sb.LastIndexOf("68") + 4);
                ss = Encoding.UTF8.GetString(ss.HexToBytes());
                if (ss == "Zoro.NativeNEP5.Create")
                {
                    create = true;
                    scriptMethods.Add(new ScriptMethod("SysCall", "NativeNEP5.Create", ss));
                }
                else if (ss == "Zoro.Contract.Create")
                {
                    create = true;
                    scriptMethods.Add(new ScriptMethod("SysCall", "Contract.Create", ss));
                }
            }
            if (!create)
                for (var i = 0; i < sb.Length / 2; i++)
            {
                var length = System.Convert.ToInt32(sb.Substring(i * 2, 2), 16);
                if (length == 103)
                {
                    scriptMethods.Add(new ScriptMethod("AppCall", Encoding.UTF8.GetString(list[list.Count - 1].ToString().HexToBytes()), sb.Substring(i * 2 + 2, 20 * 2).HexToBytes().Reverse().ToHexString()));
                    isAppCall = true;
                }
                else if (length == 104)
                {
                    int l = System.Convert.ToInt32(sb.Substring((i + 1) * 2, 2), 16);
                    scriptMethods.Add(new ScriptMethod("SysCall", Encoding.UTF8.GetString(list[list.Count - 1].ToString().HexToBytes()), Encoding.UTF8.GetString(sb.Substring(i * 2 + 4, l * 2).HexToBytes())));
                    length = l + 1;
                }
                else if (length < 1 || (length > 74 && length < 80) || (length > 80 && length < 110) || 
                    (length > 113 && length < 136) || (length > 138 && length < 142) || (length > 142 && length < 157) 
                    || (length > 157 && length < 166) || (length > 166 && length < 171) || (length > 171 && length <175) || (length > 191 && length < 206) 
                    || (length > 223 && length < 229) || (length > 239 && length < 242)) continue;
                if (isAppCall)
                {
                    isAppCall = false;
                    list.Add(sb.Substring(i * 2 + 2, 20 * 2));
                    i += 20;
                }
                else
                {
                    list.Add(sb.Substring(i * 2 + 2, length * 2));
                    i += length;
                }
            }
        }
    }

    class ScriptMethod {
        public string calltype;
        public string method;
        public string contract;
        public ScriptMethod(string calltype, string method, string contract) {
            this.calltype = calltype;
            this.method = method;
            this.contract = contract;
        }
    }
}
