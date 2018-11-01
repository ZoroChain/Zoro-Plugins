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
        private readonly Wallet wallet;

        public RpcHandler(Wallet wallet)
        {
            this.wallet = wallet;
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
            if (wallet != null)
            {
                InvocationTransaction tx = new InvocationTransaction
                {
                    Version = 1,
                    ChainHash = blockchain.ChainHash,
                    Script = json["script"].AsString().HexToBytes(),
                    Gas = Fixed8.Parse(json["gas_consumed"].AsString())
                };
                tx.Gas -= Fixed8.FromDecimal(10);
                if (tx.Gas < Fixed8.Zero) tx.Gas = Fixed8.Zero;
                tx.Gas = tx.Gas.Ceiling();
                tx = wallet.MakeTransaction(tx);
                if (tx != null)
                {
                    ContractParametersContext context = new ContractParametersContext(tx, blockchain);
                    wallet.Sign(context);
                    if (context.Completed)
                        tx.Witnesses = context.GetWitnesses();
                    else
                        tx = null;
                }
                json["tx"] = tx?.ToArray().ToHexString();
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
