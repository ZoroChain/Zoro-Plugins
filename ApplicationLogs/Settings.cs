using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Zoro.Plugins
{
    internal class Settings
    {
        public string Path { get; }

        public static Settings Default { get; }

        static Settings()
        {
            Default = new Settings(Assembly.GetExecutingAssembly().GetConfiguration());
        }

        public Settings(IConfigurationSection section)
        {
            this.Path = section.GetSection("Path").Value;
        }
    }
}
