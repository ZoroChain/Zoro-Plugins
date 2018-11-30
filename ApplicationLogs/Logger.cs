using Akka.Actor;
using Zoro.IO;
using Zoro.IO.Data.LevelDB;
using Zoro.IO.Json;
using Zoro.Ledger;
using Neo.VM;
using System;
using System.Linq;

namespace Zoro.Plugins
{
    internal class Logger : UntypedActor
    {
        private readonly DB db;

        public Logger(IActorRef blockchain, DB db)
        {
            this.db = db;
            
            // 注册Blockchain的分发事件
            blockchain.Tell(new Blockchain.Register());
        }

        protected override void PostStop()
        {
            // 关闭对应的ApplicationLog数据库
            db.Dispose();            
        }

        protected override void OnReceive(object message)
        {
            // 处理Blockchain发来的消息通知
            if (message is Blockchain.ApplicationExecuted e)
            {
                JObject json = new JObject();
                json["txid"] = e.Transaction.Hash.ToString();
                json["executions"] = e.ExecutionResults.Select(p =>
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
                db.Put(WriteOptions.Default, e.Transaction.Hash.ToArray(), json.ToString());
            }
        }

        public static Props Props(IActorRef blockchain, DB db)
        {
            return Akka.Actor.Props.Create(() => new Logger(blockchain, db));
        }
    }
}
