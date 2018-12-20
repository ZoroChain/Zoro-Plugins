using Microsoft.Extensions.Configuration;

namespace Zoro.Plugins
{
    internal class Settings
    {
        public string Path { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.Path = section.GetSection("Path").Value;
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
