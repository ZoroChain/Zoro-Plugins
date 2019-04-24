using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Net;
using System.Numerics;
using System.Globalization;
using System.Threading.Tasks;
using Neo.VM;
using MySql.Data.MySqlClient;
using Zoro.IO.Json;
using Zoro.SmartContract;
using Zoro.Ledger;
using System.Linq;

namespace Zoro.Plugins
{
    class SaveNEP5Asset : SaveBase
    {
        public SaveNEP5Asset(UInt160 chainHash)
            : base(chainHash)
        {
            InitDataTable(TableType.NEP5Asset);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.NEP5Asset, name);
            return true;
        }

        public void Save(MySqlConnection conn, JObject jToken)
        {
            string contract = SpiderHelper.getString(jToken["assetid"].ToString());
            Dictionary<string, string> where = new Dictionary<string, string>();
            where.Add("assetid", contract);
            bool exist = MysqlConn.CheckExist(DataTableName, where);
            if (!exist)
            {
                Start(conn, contract);
            }
        }

        public async void Start(MySqlConnection conn, string contract)
        {
            getNativeNEP5Asset(conn, UInt160.Parse(contract));
            getNEP5Asset(conn, UInt160.Parse(contract));        
        }

        public void getNativeNEP5Asset(MySqlConnection conn, UInt160 Contract)
        {
            try
            {
                ScriptBuilder sb = new ScriptBuilder();

                sb.EmitSysCall("Zoro.NativeNEP5.Call", "TotalSupply", Contract);
                sb.EmitSysCall("Zoro.NativeNEP5.Call", "Name", Contract);
                sb.EmitSysCall("Zoro.NativeNEP5.Call", "Symbol", Contract);               
                sb.EmitSysCall("Zoro.NativeNEP5.Call", "Decimals", Contract);

                IO.Json.JObject jObject;

                Blockchain blockchain = ZoroChainSystem.Singleton.GetBlockchain(ChainHash); ;
                ApplicationEngine engine = ApplicationEngine.Run(sb.ToArray(), blockchain, testMode: true);
                if (engine.State.HasFlag(VMState.FAULT)) {
                    return;
                }

                JArray jStack = new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()));

                string totalSupply = jStack[0]["type"].AsString() == "ByteArray" ? new BigInteger(SpiderHelper.HexString2Bytes(jStack[0]["value"].AsString())).ToString() : jStack[0]["value"].AsString();
                string name = jStack[1]["type"].AsString() == "ByteArray" ? Encoding.UTF8.GetString(SpiderHelper.HexString2Bytes(jStack[1]["value"].AsString())) : jStack[1]["value"].AsString();
                string symbol = jStack[2]["type"].AsString() == "ByteArray" ? Encoding.UTF8.GetString(SpiderHelper.HexString2Bytes(jStack[2]["value"].AsString())) : jStack[2]["value"].AsString();
                string decimals = jStack[3]["type"].AsString() == "ByteArray" ? BigInteger.Parse(jStack[3]["value"].AsString()).ToString() : jStack[3]["value"].AsString();

                //BCT没有限制，可以随意增发
                if (symbol == "BCT") { totalSupply = "0"; }

                List<string> slist = new List<string>();
                slist.Add(Contract.ToString());
                slist.Add(totalSupply);
                slist.Add(name);
                slist.Add(symbol);
                slist.Add(decimals);

                
                //这里有个bug，我们的bcp会因为转账而增长          
                {
                    MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);
                }

                LogConfig.Log($"SaveNEP5Asset {ChainHash} {Contract}", LogConfig.LogLevel.Info, ChainHash.ToString());
            }
            catch (Exception e)
            {
                LogConfig.Log($"error occured when call invokescript, chainhash:{ChainHash}, nep5contract:{Contract.ToString()}, reason:{e.Message}", LogConfig.LogLevel.Error);
                throw e;
            }
        }

        public void getNEP5Asset(MySqlConnection conn, UInt160 Contract)
        {            
            try
            {
                ScriptBuilder sb = new ScriptBuilder();

                sb.EmitAppCall(Contract, "totalSupply");
                sb.EmitAppCall(Contract, "name");
                sb.EmitAppCall(Contract, "symbol");
                sb.EmitAppCall(Contract, "decimals");

                Blockchain blockchain = ZoroChainSystem.Singleton.GetBlockchain(ChainHash); ;
                ApplicationEngine engine = ApplicationEngine.Run(sb.ToArray(), blockchain, testMode: true);
                if (engine.State.HasFlag(VMState.FAULT))
                {
                    return;
                }

                JArray jStack = new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()));
                
                string totalSupply = new BigInteger(SpiderHelper.HexString2Bytes(jStack[0]["value"].ToString())).ToString();
                string name = Encoding.UTF8.GetString(SpiderHelper.HexString2Bytes(jStack[1]["value"].ToString()));
                string symbol = Encoding.UTF8.GetString(SpiderHelper.HexString2Bytes(jStack[2]["value"].ToString()));
                string decimals = BigInteger.Parse(jStack[3]["value"].ToString()).ToString();

                List<string> slist = new List<string>();
                slist.Add(Contract.ToString());
                slist.Add(totalSupply);
                slist.Add(name);
                slist.Add(symbol);
                slist.Add(decimals);

                //Dictionary<string, string> dictionary = new Dictionary<string, string>();
                //dictionary.Add("assetid", Contract.ToString());
                //bool exist = MysqlConn.CheckExist(DataTableName, dictionary);
                //if (!exist)
                //这里有个bug，我们的bcp会因为转账而增长          
                {
                    MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);
                }

                LogConfig.Log($"SaveNEP5Asset {ChainHash} {Contract}", LogConfig.LogLevel.Info, ChainHash.ToString());
            }
            catch (Exception e)
            {
                LogConfig.Log($"error occured when call invokescript, chainhash:{ChainHash}, nep5contract:{Contract.ToString()}, reason:{e.Message}", LogConfig.LogLevel.Error);
                throw e;
            }
        }
    }
}
