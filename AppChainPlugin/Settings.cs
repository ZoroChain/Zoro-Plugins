using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Linq;

namespace Zoro.Plugins
{
    internal class Settings
    {
        public ushort Port { get; }
        public ushort WsPort { get; }
        public bool SaveJson { get; }
        public string[] KeyNames { get; }

        public static Settings Default { get; }

        static Settings()
        {
            Default = new Settings(Assembly.GetExecutingAssembly().GetConfiguration());
        }

        public Settings(IConfigurationSection section)
        {
            this.Port = ushort.Parse(section.GetSection("Port").Value);
            this.WsPort = ushort.Parse(section.GetSection("WsPort").Value);
            this.SaveJson = bool.Parse(section.GetSection("SaveJson").Value);
            this.KeyNames = section.GetSection("KeyNames").GetChildren().Select(p => p.Value.ToLower()).ToArray();
        }
    }
}
