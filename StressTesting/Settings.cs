using Microsoft.Extensions.Configuration;

namespace Zoro.Plugins
{
    internal class Settings
    {        
        public string WIF { get; }
        public string TargetWIF { get; }
        public string TargetChainHash { get; }
        public string NEP5Hash { get; }
        public string NativeNEP5Hash { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.WIF = section.GetSection("WIF").Value;
            this.TargetWIF = section.GetSection("TargetWIF").Value;
            this.TargetChainHash = section.GetSection("TargetChainHash").Value;
            this.NEP5Hash = section.GetSection("NEP5Hash").Value;
            this.NativeNEP5Hash = section.GetSection("NativeNEP5").Value;
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}
