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

        public SimplePolicyPlugin(PluginManager pluginMgr)
            : base(pluginMgr)
        {
        }

        public bool FilterForMemoryPool(Transaction tx)
        {
            switch (Settings.Default.BlockedAccounts.Type)
            {
                case PolicyType.AllowAll:
                    return true;
                case PolicyType.AllowList:
                    return tx.Witnesses.All(p => Settings.Default.BlockedAccounts.List.Contains(p.VerificationScript.ToScriptHash())) || tx.Outputs.All(p => Settings.Default.BlockedAccounts.List.Contains(p.ScriptHash));
                case PolicyType.DenyList:
                    return tx.Witnesses.All(p => !Settings.Default.BlockedAccounts.List.Contains(p.VerificationScript.ToScriptHash())) && tx.Outputs.All(p => !Settings.Default.BlockedAccounts.List.Contains(p.ScriptHash));
                default:
                    return false;
            }
        }

        public IEnumerable<Transaction> FilterForBlock(IEnumerable<Transaction> transactions)
        {
            Transaction[] array = transactions.ToArray();
            if (array.Length + 1 <= Settings.Default.MaxTransactionsPerBlock)
                return array;
            transactions = array.OrderByDescending(p => p.NetworkFee / p.Size).ThenByDescending(p => p.NetworkFee).Take(Settings.Default.MaxTransactionsPerBlock - 1);
            return FilterFree(transactions);
        }

        private IEnumerable<Transaction> FilterFree(IEnumerable<Transaction> transactions)
        {
            int count = 0;
            foreach (Transaction tx in transactions)
                if (tx.NetworkFee > Fixed8.Zero || tx.SystemFee > Fixed8.Zero)
                    yield return tx;
                else if (count++ < Settings.Default.MaxFreeTransactionsPerBlock)
                    yield return tx;
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
                case "enable":
                    return OnEnableLogCommand(args);
                case "disable":
                    return OnDisableLogCommand(args);
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

        private bool OnEnableLogCommand(string[] args)
        {
            string source = args[2];
            if (source == "all")
            {
                PluginManager.EnableAllLogSources();
            }
            else
            {
                PluginManager.EnableLogSource(source);
            }

            return true;
        }

        private bool OnDisableLogCommand(string[] args)
        {
            PluginManager.DisableLogSource(args[2]);
            return true;
        }
    }
}
