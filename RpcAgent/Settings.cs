using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Zoro.Plugins
{
    internal class Settings
    {
        public ushort Port { get; }
        public ushort MaxConnections { get; }

        public static Settings Default { get; }

        static Settings()
        {
            Default = new Settings(Assembly.GetExecutingAssembly().GetConfiguration());
        }

        public Settings(IConfigurationSection section)
        {
            this.Port = ushort.Parse(section.GetSection("Port").Value);
            this.MaxConnections = ushort.Parse(section.GetSection("MaxConnections").Value);
        }
    }
}
