using Microsoft.Extensions.Configuration;
using Zoro.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zoro.Plugins
{
    internal class Settings
    {
        public int MaxTransactionsPerBlock { get; }
        public BlockedAccounts BlockedAccounts { get; }
        public string RelativePath { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.MaxTransactionsPerBlock = GetValueOrDefault(section.GetSection("MaxTransactionsPerBlock"), 500, p => int.Parse(p));
            this.BlockedAccounts = new BlockedAccounts(section.GetSection("BlockedAccounts"));
            this.RelativePath = section.GetSection("RelativePath")?.Value ?? "";
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }

    internal enum PolicyType : byte
    {
        AllowAll,
        DenyAll,
        AllowList,
        DenyList
    }

    internal class BlockedAccounts
    {
        public PolicyType Type { get; }
        public HashSet<UInt160> List { get; }

        public BlockedAccounts(IConfigurationSection section)
        {
            this.Type = (PolicyType)Enum.Parse(typeof(PolicyType), section.GetSection("Type").Value, true);
            this.List = new HashSet<UInt160>(section.GetSection("List").GetChildren().Select(p => p.Value.ToScriptHash()));
        }
    }
}
