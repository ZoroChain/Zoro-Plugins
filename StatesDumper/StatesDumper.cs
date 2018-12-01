using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Ledger;
using Zoro.AppChain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zoro.Plugins
{
    public class StatesDumper : Plugin
    {
        public StatesDumper(PluginManager pluginMgr)
            : base(pluginMgr)
        {
        }

        private static void Dump<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> states)
            where TKey : ISerializable
            where TValue : ISerializable
        {
            const string path = "dump.json";
            JArray array = new JArray(states.Select(p =>
            {
                JObject state = new JObject();
                state["key"] = p.Key.ToArray().ToHexString();
                state["value"] = p.Value.ToArray().ToHexString();
                return state;
            }));
            File.WriteAllText(path, array.ToString());
            Console.WriteLine($"States ({array.Count}) have been dumped into file {path}");
        }

        public override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args.Length == 0) return false;
            switch (args[0].ToLower())
            {
                case "help":
                    return OnHelp(args);
                case "dump":
                    return OnDump(args);
            }
            return false;
        }

        private bool OnDump(string[] args)
        {
            if (args.Length < 3) return false;
            switch (args[1].ToLower())
            {
                case "storage":
                    DumpStorage(args);
                    return true;
                default:
                    return false;
            }
        }

        private void DumpStorage(string[] args)
        {
            // 用输入的第三个参数，获取Blockchain对象
            Blockchain blockchain = ZoroChainSystem.Singleton.GetBlockchain(args[2]);
            Dump(args.Length >= 4 ? blockchain.Store.GetStorages().Find(UInt160.Parse(args[3]).ToArray()) : blockchain.Store.GetStorages().Find());
        }

        private bool OnHelp(string[] args)
        {
            if (args.Length < 2) return false;
            if (!string.Equals(args[1], Name, StringComparison.OrdinalIgnoreCase))
                return false;
            Console.Write($"{Name} Commands:\n" + "\tdump storage <chainhash> <key>\n");
            return true;
        }
    }
}
