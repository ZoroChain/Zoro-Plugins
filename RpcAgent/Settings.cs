using Microsoft.Extensions.Configuration;
using System;

namespace Zoro.Plugins
{
    internal class Settings
    {
        public ushort Port { get; }
        public ushort MaxConnections { get; }
        public bool DebugMode { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.Port = ushort.Parse(section.GetSection("Port").Value);
            this.MaxConnections = ushort.Parse(section.GetSection("MaxConnections").Value);
            this.DebugMode = GetValueOrDefault(section.GetSection("DebugMode"), false, p => bool.Parse(p));
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
}
