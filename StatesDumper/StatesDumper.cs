using Zoro.IO;
using Zoro.IO.Json;
using Zoro.IO.Caching;
using Zoro.Ledger;
using Zoro.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zoro.Plugins
{
    public class StatesDumper : Plugin, IPersistencePlugin
    {
        private readonly JArray bs_cache = new JArray();

        public StatesDumper(PluginManager pluginMgr)
            : base(pluginMgr)
        {
        }
        public override void Configure()
        {
            Settings.Load(GetConfiguration());
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

        public void OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (Settings.Default.PersistAction.HasFlag(PersistActions.StorageChanges))
                OnPersistStorage(snapshot);
        }

        private void OnPersistStorage(Snapshot snapshot)
        {
            uint blockIndex = snapshot.Height;
            if (blockIndex >= Settings.Default.HeightToBegin)
            {
                string dirPath = "./Storage";
                Directory.CreateDirectory(dirPath);
                string path = $"{HandlePaths(dirPath, blockIndex)}/dump-block-{blockIndex.ToString()}.json";

                JArray array = new JArray();

                foreach (DataCache<StorageKey, StorageItem>.Trackable trackable in snapshot.Storages.GetChangeSet())
                {
                    JObject state = new JObject();

                    switch (trackable.State)
                    {

                        case TrackState.Added:
                            state["state"] = "Added";
                            state["key"] = trackable.Key.ToArray().ToHexString();
                            state["value"] = trackable.Item.ToArray().ToHexString();
                            // Here we have a new trackable.Key and trackable.Item
                            break;
                        case TrackState.Changed:
                            state["state"] = "Changed";
                            state["key"] = trackable.Key.ToArray().ToHexString();
                            state["value"] = trackable.Item.ToArray().ToHexString();
                            break;
                        case TrackState.Deleted:
                            state["state"] = "Deleted";
                            state["key"] = trackable.Key.ToArray().ToHexString();
                            break;
                    }
                    array.Add(state);
                }

                JObject bs_item = new JObject();
                bs_item["block"] = blockIndex;
                bs_item["size"] = array.Count;
                bs_item["storage"] = array;
                bs_cache.Add(bs_item);

                if ((blockIndex % Settings.Default.BlockCacheSize == 0) || (blockIndex > Settings.Default.HeightToStartRealTimeSyncing))
                {
                    File.WriteAllText(path, bs_cache.ToString());
                    bs_cache.Clear();
                }
            }
        }

        private static string HandlePaths(string dirPath, uint blockIndex)
        {
            //Default Parameter
            uint storagePerFolder = 100000;
            uint folder = (((blockIndex - 1) / storagePerFolder) + 1) * storagePerFolder;
            if (blockIndex == 0)
                folder = 0;
            string dirPathWithBlock = $"{dirPath}/BlockStorage_{folder}";
            Directory.CreateDirectory(dirPathWithBlock);
            return dirPathWithBlock;
        }
    }
}
