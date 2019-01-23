using Microsoft.Extensions.Configuration;
using System;

namespace Zoro.Plugins
{
    internal class Settings
    {        
        public string WIF { get; }
        public string TargetWIF { get; }
        public string TargetChainHash { get; }
        public string NEP5Hash { get; }
        public string NativeNEP5Hash { get; }
        public int EnableManualParam { get; }
        public int TransactionType { get; }
        public int ConcurrencyCount { get; }
        public int TransferCount { get; }
        public int TransferValue { get; }
        public int RandomTargetAddress { get; }
        public int RandomGasPrice { get; }
        public int PreventOverflow { get; }
        public int PrintErrorReason { get; }
        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.WIF = section.GetSection("WIF").Value;
            this.TargetWIF = section.GetSection("TargetWIF").Value;
            this.TargetChainHash = section.GetSection("TargetChainHash").Value;
            this.NEP5Hash = section.GetSection("NEP5Hash").Value;
            this.NativeNEP5Hash = section.GetSection("NativeNEP5").Value;
            this.EnableManualParam = GetValueOrDefault(section.GetSection("EnableManualParam"), 1, p => int.Parse(p));
            this.TransactionType = GetValueOrDefault(section.GetSection("TransactionType"), 0, p => int.Parse(p));
            this.ConcurrencyCount = GetValueOrDefault(section.GetSection("ConcurrencyCount"), 0, p => int.Parse(p));
            this.TransferCount = GetValueOrDefault(section.GetSection("TransferCount"), 0, p => int.Parse(p));
            this.TransferValue = GetValueOrDefault(section.GetSection("TransferValue"), 0, p => int.Parse(p));
            this.RandomTargetAddress = GetValueOrDefault(section.GetSection("RandomTargetAddress"), 0, p => int.Parse(p));
            this.RandomGasPrice = GetValueOrDefault(section.GetSection("RandomGasPrice"), 0, p => int.Parse(p));
            this.PreventOverflow = GetValueOrDefault(section.GetSection("PreventOverflow"), 0, p => int.Parse(p));
            this.PrintErrorReason = GetValueOrDefault(section.GetSection("PrintErrorReason"), 0, p => int.Parse(p));
        }

        internal T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
