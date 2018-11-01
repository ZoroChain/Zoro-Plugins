using System;
using System.Linq;
using Zoro.Ledger;
using Zoro.IO;
using Zoro.IO.Json;
using Zoro.Network.RPC;
using Zoro.Network.P2P.Payloads;
using Zoro.SmartContract;
using Zoro.Wallets;
using Neo.VM;
using Akka.Actor;

namespace Zoro
{
    class RpcHandler
    {
        public RpcHandler()
        {
        }

        public JObject HandleRequest(RpcRequestPayload payload)
        {
            JObject result = null;

            switch (payload.Command)
            {
                case "invokescript":
                    {
                        result = OnInvokeScript(payload);
                    }
                    break;
                case "sendrawtransaction":
                    {
                        result = OnRawTransaction(payload);
                    }
                    break;
            }

            return result;
        }

        protected JObject OnRawTransaction(RpcRequestPayload payload)
        {
            ZoroSystem system = GetSystemByHash(payload.ChainHash);
            if (system != null)
            {
                Transaction tx = Transaction.DeserializeFrom(payload.Data);
                RelayResultReason reason = system.Blockchain.Ask<RelayResultReason>(tx).Result;
                return reason;
            }
            return RelayResultReason.Invalid;
        }


        protected JObject OnInvokeScript(RpcRequestPayload payload)
        {
            JObject json = new JObject();
            json["script"] = payload.Data.ToHexString();

            Blockchain blockchain = Blockchain.GetBlockchain(payload.ChainHash);

            if (blockchain == null)
            {
                json["state"] = "Unknown blockchain";
                json["gas_consumed"] = "0";
                return json;
            }

            ApplicationEngine engine = ApplicationEngine.Run(payload.Data, blockchain.GetSnapshot(), null, null, true);

            json["state"] = engine.State;
            json["gas_consumed"] = engine.GasConsumed.ToString();
            try
            {
                json["stack"] = new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()));
            }
            catch (InvalidOperationException)
            {
                json["stack"] = "error: recursive reference";
            }
            
            return json;
        }

        private ZoroSystem GetSystemByHash(UInt160 chain_hash)
        {
            if (chain_hash == UInt160.Zero)
            {
                return ZoroSystem.Root;
            }
            else
            {
                if (ZoroSystem.GetAppChainSystem(chain_hash, out ZoroSystem app_system))
                {
                    return app_system;
                }
            }

            return null;
        }
    }
}
