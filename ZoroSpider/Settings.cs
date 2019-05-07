using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Zoro.Plugins
{
    internal class Settings
    {
        public string MysqlConfig { get; }
        public string DataBaseName { get; }
        public List<ChainSettings> ChainSettings { get; }

        public string RelativePath { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.RelativePath = section.GetSection("RelativePath")?.Value ?? "";

            IEnumerable<IConfigurationSection> mysql = section.GetSection("MySql").GetChildren();

            this.MysqlConfig = "";

            foreach (var item in mysql)
            {
                this.MysqlConfig += item.Key + " = " + item.Value;
                this.MysqlConfig += ";";
            }

            DataBaseName = section.GetSection("MySql").GetSection("database").Value;          

            this.ChainSettings = section.GetSection("Chains").GetChildren().Select(p => new ChainSettings(p)).ToList();
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
    }

    internal class ChainSettings
    {
        public string Name { get; }
        public string Hash { get; }
        public int StartHeight { get; }

        public ChainSettings(IConfigurationSection section)
        {
            Name = GetValueOrDefault(section.GetSection("Name"), "", p => p);
            Hash = GetValueOrDefault(section.GetSection("Hash"), "", p => p);
            StartHeight = GetValueOrDefault(section.GetSection("StartHeight"), 0, p => int.Parse(p));
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
    }
}
