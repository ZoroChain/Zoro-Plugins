using System;
using System.Net;
using System.Text;
using System.Data;
using System.Collections.Generic;
using System.Numerics;
using System.Globalization;
using System.Threading.Tasks;
using Zoro.Wallets;
using System.Linq;
using MySql.Data.MySqlClient;
using Zoro.IO.Json;
using Zoro.IO.Data.LevelDB;
using System.IO;
using Zoro.Network.P2P;
using Zoro.Network.RPC;
using System.Collections.Concurrent;

namespace Zoro.Plugins
{
    class SaveNotify : SaveBase
    {
        private SaveAddress address;
        private SaveAddressAsset addressAsset;
        private SaveAddressTransaction address_tx;
        private SaveNEP5Asset nep5Asset;
        private SaveNEP5Transfer nep5Transfer;

        private static ConcurrentDictionary<UInt160, DB> dbs = new ConcurrentDictionary<UInt160, DB>();

        public SaveNotify(UInt160 chainHash)
            : base(chainHash)
        {
            InitDataTable(TableType.Notify);

            address = new SaveAddress(chainHash);
            addressAsset = new SaveAddressAsset(chainHash);
            address_tx = new SaveAddressTransaction(chainHash);
            nep5Asset = new SaveNEP5Asset(chainHash);
            nep5Transfer = new SaveNEP5Transfer(chainHash);
        }

        public override bool CreateTable(string name)
        {
            MysqlConn.CreateTable(TableType.Notify, name);
            return true;
        }

        private bool TryGetDB(UInt160 chainHash, out DB db)
        {
            return dbs.TryGetValue(chainHash, out db);
        }

        public static void setDBs(ConcurrentDictionary<UInt160, DB> db)
        {
            dbs = db;
        }

        public void Save(MySqlConnection conn, JObject jToken)
        {
            JObject executions = null;
            try
            {
                if (jToken != null)
                {
                    executions = jToken["executions"];
                }
            }
            catch (Exception e)
            {

            }
            //try
            //{
            //    if (!TryGetDB(ChainHash, out DB db)) return;
            //    if (db.IsDisposed) return;
            //    UInt256 hash = UInt256.Parse(SpiderHelper.getString(jToken["txid"].ToString()));
            //    if (!db.TryGet(ReadOptions.Default, hash.ToArray(), out Slice value))
            //        throw new RpcException(-100, "Unknown transaction");
            //    result = JObject.Parse(value.ToString());
            //    if (result != null)
            //    executions = result["executions"];
            //}
            //catch (Exception e)
            //{
            //    LogConfig.Log($"error occured when call getapplicationlog, chain:{ChainHash} height:{blockHeight}, reason:{e.Message}", LogConfig.LogLevel.Error);
            //    //throw e;
            //}

            if (jToken != null && executions != null)
            {
                foreach (var execution in executions as JArray)
                {
                    List<string> slist = new List<string>();
                    slist.Add(SpiderHelper.getString(jToken["txid"].ToString()));
                    slist.Add(SpiderHelper.getString(execution["vmstate"].ToString()));
                    slist.Add(SpiderHelper.getString(execution["gas_consumed"].ToString()));
                    slist.Add(SpiderHelper.getString(execution["stack"].ToString()));
                    slist.Add(SpiderHelper.getString(execution["notifications"].ToString().Replace(@"[/n/r]", "")));

                    Dictionary<string, string> where = new Dictionary<string, string>();
                    where.Add("txid", SpiderHelper.getString(jToken["txid"].ToString()));
                    MysqlConn.Delete(conn, DataTableName, where);

                    MysqlConn.ExecuteDataInsert(conn, DataTableName, slist);

                    LogConfig.Log($"SaveNotify {ChainHash} {jToken["txid"]}", LogConfig.LogLevel.Info, ChainHash.ToString());

                    if (execution["vmstate"].ToString().Contains("FAULT"))
                    {
                        return;
                    }

                    JObject notifications = execution["notifications"];

                    foreach (JObject notify in notifications as JArray)
                    {
                        JArray values = notify["state"]["value"] as JArray;

                        if (values[0]["type"].ToString() == "ByteArray")
                        {
                            string transfer = Encoding.UTF8.GetString(SpiderHelper.HexString2Bytes(SpiderHelper.getString(values[0]["value"].ToString())));
                            string contract = SpiderHelper.getString(notify["contract"].ToString());

                            if (transfer == "transfer")
                            {
                                JObject nep5 = new JObject();
                                nep5["assetid"] = contract;
                                nep5Asset.Save(conn, nep5);

                                //存储Nep5Transfer内容
                                JObject tx = new JObject();
                                tx["txid"] = SpiderHelper.getString(jToken["txid"].ToString());
                                tx["n"] = 0;
                                tx["asset"] = contract;
                                if (SpiderHelper.getString(values[1]["value"].ToString()) == "")
                                {
                                    tx["from"] = "";
                                }
                                else
                                {
                                    tx["from"] = UInt160.Parse(SpiderHelper.getString(values[1]["value"].ToString())).ToAddress();
                                }

                                tx["to"] = UInt160.Parse(SpiderHelper.getString(values[2]["value"].ToString())).ToAddress();
                                if (SpiderHelper.getString(values[3]["type"].ToString()) == "ByteArray")
                                {
                                    tx["value"] = new BigInteger(SpiderHelper.HexString2Bytes(SpiderHelper.getString(values[3]["value"].ToString()))).ToString();
                                }
                                else
                                {
                                    tx["value"] = BigInteger.Parse(SpiderHelper.getString(values[3]["value"].ToString()), NumberStyles.AllowHexSpecifier).ToString();
                                }
                                JObject j = new JObject();
                                j["address"] = SpiderHelper.getString(tx["to"].ToString());
                                j["txid"] = SpiderHelper.getString(tx["txid"].ToString());
                                address.Save(conn, j);
                                addressAsset.Save(conn, SpiderHelper.getString(tx["to"].ToString()), contract, "");
                                address_tx.Save(conn, j);
                                nep5Transfer.Save(conn, tx);
                            }
                        }
                    }
                }
            }
        }
    }
}
