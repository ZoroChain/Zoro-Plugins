using Microsoft.AspNetCore.Http;
using Zoro.IO.Data.LevelDB;
using Zoro.IO.Json;
using Zoro.Ledger;
using Zoro.Network.RPC;
using Zoro.Network.P2P;
using Neo.VM;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Snapshot = Zoro.Persistence.Snapshot;

namespace Zoro.Plugins
{
    public class LogReader : Plugin, IRpcPlugin, IPersistencePlugin
    {
        private ConcurrentDictionary<UInt160, DB> dbs = new ConcurrentDictionary<UInt160, DB>();

        public LogReader(PluginManager pluginMgr)
            : base(pluginMgr)
        {
        }

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public override void Dispose()
        {
            foreach (DB db in dbs.Values)
            {
                db.Dispose();
            }
            dbs.Clear();
        }

        // 处理ZoroSystem发来的消息通知
        public override bool OnMessage(object message)
        {
            if (message is ZoroSystem.ChainStarted started)
            {
                // 每次有一条链启动时，打开对应的ApplicationLog数据库文件
                CreateLogger(started.ChainHash);
            }
            return false;
        }

        private void CreateLogger(UInt160 chainHash)
        {
            if (!dbs.ContainsKey(chainHash))
            {
                // 根据链的Hash，获取对应的ZoroSystem对象
                ZoroSystem system = ZoroChainSystem.Singleton.GetZoroSystem(chainHash);
                if (system != null)
                {
                    // 用MagicNumber加上ChainHash作为ApplicationLog数据库的文件名
                    string path = string.Format(Settings.Default.Path, Message.Magic.ToString("X8"), chainHash.ToArray().Reverse().ToHexString());

                    string relativePath = Settings.Default.RelativePath;
                    if (relativePath.Length > 0)
                        path = relativePath + path;

                    Directory.CreateDirectory(path);

                    DB db = DB.Open(Path.GetFullPath(path), new Options { CreateIfMissing = true });

                    dbs.TryAdd(chainHash, db);
                }

                PluginManager.Singleton.SendMessage(dbs);
            }
        }

        public void RemoveLogger(UInt160 chainHash)
        {
            dbs.TryRemove(chainHash, out DB _);
        }

        // 根据ChainHash，获取对应的Db对象
        private bool TryGetDB(JObject param, out DB db)
        {
            string hashString = param.AsString();

            if (ZoroChainSystem.Singleton.TryParseChainHash(hashString, out UInt160 chainHash))
            {
                return dbs.TryGetValue(chainHash, out db);
            }
            db = null;
            return false;
        }
        
        // 第一个参数是ChainHash，第二个参数是交易Id
        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (method != "getapplicationlog") return null;
            if (!TryGetDB(_params[0], out DB db)) return null;
            if (db.IsDisposed) return null;
            UInt256 hash = UInt256.Parse(_params[1].AsString());
            if (!db.TryGet(ReadOptions.Default, hash.ToArray(), out Slice value))
                throw new RpcException(-100, "Unknown transaction");
            return JObject.Parse(value.ToString());
        }

        public void OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (!dbs.TryGetValue(snapshot.Blockchain.ChainHash, out DB db)) return;

            WriteBatch writeBatch = new WriteBatch();

            foreach (var appExec in applicationExecutedList)
            {
                JObject json = new JObject();
                json["txid"] = appExec.Transaction.Hash.ToString();
                json["executions"] = appExec.ExecutionResults.Select(p =>
                {
                    JObject execution = new JObject();
                    execution["trigger"] = p.Trigger;
                    execution["contract"] = p.ScriptHash.ToString();
                    execution["vmstate"] = p.VMState;
                    execution["gas_consumed"] = p.GasConsumed.ToString();
                    try
                    {
                        execution["stack"] = p.Stack.Select(q => q.ToParameter().ToJson()).ToArray();
                    }
                    catch (InvalidOperationException)
                    {
                        execution["stack"] = "error: recursive reference";
                    }
                    execution["notifications"] = p.Notifications.Select(q =>
                    {
                        JObject notification = new JObject();
                        notification["contract"] = q.ScriptHash.ToString();
                        try
                        {
                            notification["state"] = q.State.ToParameter().ToJson();
                        }
                        catch (InvalidOperationException)
                        {
                            notification["state"] = "error: recursive reference";
                        }
                        return notification;
                    }).ToArray();
                    return execution;
                }).ToArray();
                writeBatch.Put(appExec.Transaction.Hash.ToArray(), json.ToString());
            }
            db.Write(WriteOptions.Default, writeBatch);
        }

        public void OnCommit(Snapshot snapshot)
        {
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex)
        {
            return false;
        }
    }
}
