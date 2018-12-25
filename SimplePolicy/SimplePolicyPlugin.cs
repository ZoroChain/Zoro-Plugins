using Zoro.Network.P2P;
using Zoro.Network.P2P.Payloads;
using Zoro.SmartContract;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace Zoro.Plugins
{
    public class SimplePolicyPlugin : Plugin, ILogPlugin, IPolicyPlugin
    {
        private ConcurrentDictionary<UInt160, string> log_dicts = new ConcurrentDictionary<UInt160, string>();
        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public SimplePolicyPlugin(PluginManager pluginMgr)
            : base(pluginMgr)
        {
        }

        public bool FilterForMemoryPool(Transaction tx)
        {
            if (!VerifySizeLimits(tx)) return false;

            switch (Settings.Default.BlockedAccounts.Type)
            {
                case PolicyType.AllowAll:
                    return true;
                case PolicyType.AllowList:
                    return tx.Witnesses.All(p => Settings.Default.BlockedAccounts.List.Contains(p.VerificationScript.ToScriptHash()));
                case PolicyType.DenyList:
                    return tx.Witnesses.All(p => !Settings.Default.BlockedAccounts.List.Contains(p.VerificationScript.ToScriptHash()));
                default:
                    return false;
            }
        }

        public IEnumerable<Transaction> FilterForBlock(IEnumerable<Transaction> transactions)
        {
            return FilterForBlock_Policy2(transactions);
        }

        private static IEnumerable<Transaction> FilterForBlock_Policy1(IEnumerable<Transaction> transactions)
        {
            int count = 0, count_free = 0;
            foreach (Transaction tx in transactions.OrderByDescending(p => p.NetworkFee / p.Size).ThenByDescending(p => p.NetworkFee))
            {
                if (count++ >= Settings.Default.MaxTransactionsPerBlock - 1) break;
                if (!tx.IsLowPriority || count_free++ < Settings.Default.MaxFreeTransactionsPerBlock)
                    yield return tx;
            }
        }

        private static IEnumerable<Transaction> FilterForBlock_Policy2(IEnumerable<Transaction> transactions)
        {
            if (!(transactions is IReadOnlyList<Transaction> tx_list))
                tx_list = transactions.ToArray();

            Transaction[] free = tx_list.Where(p => p.IsLowPriority)
                .OrderByDescending(p => p.NetworkFee / p.Size)
                .ThenByDescending(p => p.NetworkFee)
                .Take(Settings.Default.MaxFreeTransactionsPerBlock)
                .ToArray();

            Transaction[] non_free = tx_list.Where(p => !p.IsLowPriority)
                .OrderByDescending(p => p.NetworkFee / p.Size)
                .ThenByDescending(p => p.NetworkFee)
                .Take(Settings.Default.MaxTransactionsPerBlock - free.Length - 1)
                .ToArray();

            return non_free.Concat(free);
        }

        private bool VerifySizeLimits(Transaction tx)
        {
            // Not Allow free TX bigger than MaxFreeTransactionSize
            if (tx.IsLowPriority && tx.Size > Settings.Default.MaxFreeTransactionSize) return false;

            // Require proportional fee for TX bigger than MaxFreeTransactionSize
            if (tx.Size > Settings.Default.MaxFreeTransactionSize)
            {
                Fixed8 fee = Settings.Default.FeePerExtraByte * (tx.Size - Settings.Default.MaxFreeTransactionSize);

                if (tx.NetworkFee < fee) return false;
            }
            return true;
        }

        void ILogPlugin.Log(string source, LogLevel level, string message, UInt160 chainHash)
        {
            DateTime now = DateTime.Now;
            string line = $"[{now.TimeOfDay:hh\\:mm\\:ss\\.fff}] {message}";
            Console.WriteLine(line);
            string log_dictionary = GetLogDictionary(chainHash);
            if (string.IsNullOrEmpty(log_dictionary)) return;
            lock (log_dictionary)
            {
                Directory.CreateDirectory(log_dictionary);
                string path = Path.Combine(log_dictionary, $"{now:yyyy-MM-dd}.log");
                File.AppendAllLines(path, new[] { line });
            }
        }

        string GetLogDictionary(UInt160 chainHash)
        {
            if (!log_dicts.TryGetValue(chainHash, out string log_dictionary))
            {
                string path = string.Format("Logs/{0}_{1}", Message.Magic.ToString("X8"), chainHash.ToString());

                log_dictionary = Path.Combine(AppContext.BaseDirectory, path);

                log_dicts.TryAdd(chainHash, log_dictionary);
            }

            return log_dictionary;
        }

        public override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args[0] != "log") return false;
            return OnLogCommand(args);
        }

        private bool OnLogCommand(string[] args)
        {
            switch (args[1].ToLower())
            {
                case "on":
                    return OnLogOnCommand();
                case "off":
                    return OnLogOffCommand();
                case "level":
                    return OnLogLevelCommand(args);
                default:
                    return false;
            }
        }

        private bool OnLogOnCommand()
        {
            PluginManager.EnableLog(true);
            return true;
        }

        private bool OnLogOffCommand()
        {
            PluginManager.EnableLog(false);
            return true;
        }

        private bool OnLogLevelCommand(string[] args)
        {
            if (int.TryParse(args[2], out int level))
            {
                if (level >= 0 && level <= 4)
                {
                    PluginManager.SetLogLevel((LogLevel)level);
                }
            }

            return true;
        }
    }
}
