using Microsoft.Extensions.Configuration;

namespace Zoro.Plugins
{
    internal class Settings
    {
        public string Path { get; }
        public string RelativePath { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.Path = section.GetSection("Path").Value;
            this.RelativePath = section.GetSection("RelativePath")?.Value ?? "";
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
