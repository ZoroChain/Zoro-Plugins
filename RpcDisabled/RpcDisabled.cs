using Microsoft.AspNetCore.Http;
using Zoro.IO.Json;
using Zoro.Network.RPC;
using System;
using System.Linq;

namespace Zoro.Plugins
{
    public class RpcDisabled : Plugin, IRpcPlugin
    {
        public RpcDisabled(PluginManager pluginMgr)
            : base(pluginMgr)
        {
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (Settings.Default.DisabledMethods.Contains(method))
                throw new RpcException(-400, "Access denied");
            return null;
        }
    }
}
