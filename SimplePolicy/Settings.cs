﻿using Microsoft.Extensions.Configuration;
using Zoro.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Zoro.Plugins
{
    internal class Settings
    {
        public int MaxTransactionsPerBlock { get; }
        public int MaxFreeTransactionsPerBlock { get; }
        public BlockedAccounts BlockedAccounts { get; }
        public string[] DisabledLogSources { get; }

        public static Settings Default { get; }

        static Settings()
        {
            Default = new Settings(Assembly.GetExecutingAssembly().GetConfiguration());
        }

        public Settings(IConfigurationSection section)
        {
            this.MaxTransactionsPerBlock = GetValueOrDefault(section.GetSection("MaxTransactionsPerBlock"), 500, p => int.Parse(p));
            this.MaxFreeTransactionsPerBlock = GetValueOrDefault(section.GetSection("MaxFreeTransactionsPerBlock"), 20, p => int.Parse(p));
            this.BlockedAccounts = new BlockedAccounts(section.GetSection("BlockedAccounts"));
            this.DisabledLogSources = section.GetSection("DisabledLogSources").GetChildren().Select(p => p.Value).ToArray();
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
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
