using Zoro.Ledger;
using Zoro.Wallets;
using Zoro.Network.P2P.Payloads;
using Zoro.SmartContract;
using Neo.VM;
using System;
using System.Linq;
using Akka.Actor;

namespace Zoro.Plugins
{
    internal class CommandHandler
    {
        private AppChainPlugin plugin;

        public CommandHandler(AppChainPlugin plugin)
        {
            this.plugin = plugin;
        }

        public bool OnAppChainCommand(string[] args)
        {
            string command = args[1].ToLower();

            try
            {
                switch (command)
                {
                    case "create":
                        return OnCreateAppChainCommand(args);
                    case "start":
                        return OnStartAppChainCommand(args);
                    case "stop":
                        return OnStopAppChainCommand(args);
                    case "seedlist":
                        return OnChangeAppChainSeedListCommand(args);
                    case "validators":
                        return OnChangeAppChainValidatorsCommand(args);

                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                plugin.Log($"error occured when process appchain {command}." );
                return true;
            }
        }

        private static string ReadString(string prompt)
        {
            Console.Write(prompt);
            Console.Write(": ");

            string line = Console.ReadLine()?.Trim();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();

            return line;
        }

        private static int ReadInt(string prompt)
        {
            Console.Write(prompt);
            Console.Write(": ");

            string line = Console.ReadLine()?.Trim();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();

            if (int.TryParse(line, out int result))
            {
                return result;
            }
            return 0;
        }

        private static string GetRelayResult(RelayResultReason reason)
        {
            switch (reason)
            {
                case RelayResultReason.AlreadyExists:
                    return "Block or transaction already exists and cannot be sent repeatedly.";
                case RelayResultReason.OutOfMemory:
                    return "The memory pool is full and no more transactions can be sent.";
                case RelayResultReason.UnableToVerify:
                    return "The block cannot be validated.";
                case RelayResultReason.Invalid:
                    return "Block or transaction validation failed.";
                default:
                    return "Unkown error.";
            }
        }

        private bool OnCreateAppChainCommand(string[] args)
        {
            if (plugin.NoWallet()) return true;

            KeyPair keyPair = plugin.Wallet.GetAccounts().FirstOrDefault(p => p.HasKey)?.GetKey();
            if (keyPair == null)
            {
                Console.WriteLine("error, can't get pubkey");
                return true;
            }

            string name = ReadString("name");
            if (name.Length == 0)
            {
                Console.WriteLine("cancelled");
                return true;
            }

            int numSeeds = ReadInt("seed count");
            if (numSeeds <= 0)
            {
                Console.WriteLine("cancelled");
                return true;
            }

            string[] seedList = new string[numSeeds];
            for (int i = 0; i < numSeeds; i++)
            {
                seedList[i] = ReadString("seed node address " + (i + 1).ToString());
            }

            int numValidators = ReadInt("validator count");
            if (numValidators < 4)
            {
                Console.WriteLine("cancelled, the input nmber is less then minimum validator count:4.");
                return true;
            }

            string[] validators = new string[numValidators];
            for (int i = 0; i < numValidators; i++)
            {
                validators[i] = ReadString("validator pubkey " + (i + 1).ToString());
            }

            ScriptBuilder sb = new ScriptBuilder();
            for (int i = 0; i < numValidators; i++)
            {
                sb.EmitPush(validators[i]);
            }
            sb.EmitPush(numValidators);
            for (int i = 0; i < numSeeds; i++)
            {
                sb.EmitPush(seedList[i]);
            }
            sb.EmitPush(numSeeds);
            sb.EmitPush(DateTime.UtcNow.ToTimestamp());
            sb.EmitPush(keyPair.PublicKey.EncodePoint(true));
            sb.EmitPush(name);

            UInt160 chainHash = sb.ToArray().ToScriptHash();
            sb.EmitPush(chainHash);
            sb.EmitSysCall("Zoro.AppChain.Create");

            RelayResultReason reason = SubmitInvocationTransaction(keyPair, sb.ToArray());

            if (reason == RelayResultReason.Succeed)
            {
                Console.WriteLine($"Appchain hash: {chainHash.ToArray().Reverse().ToHexString()}");
            }

            return true;
        }

        private bool OnChangeAppChainSeedListCommand(string[] args)
        {
            if (plugin.NoWallet()) return true;

            KeyPair keyPair = plugin.Wallet.GetAccounts().FirstOrDefault(p => p.HasKey)?.GetKey();
            if (keyPair == null)
            {
                Console.WriteLine("error, can't get pubkey");
                return true;
            }

            string hashString = ReadString("appchain hash");
            if (hashString.Length != 40)
            {
                Console.WriteLine("cancelled");
                return true;
            }

            int numSeeds = ReadInt("seed count");
            if (numSeeds <= 0)
            {
                Console.WriteLine("cancelled");
                return true;
            }

            string[] seedList = new string[numSeeds];
            for (int i = 0; i < numSeeds; i++)
            {
                seedList[i] = ReadString("seed node address " + (i + 1).ToString());
            }

            ScriptBuilder sb = new ScriptBuilder();

            for (int i = 0; i < numSeeds; i++)
            {
                sb.EmitPush(seedList[i]);
            }
            sb.EmitPush(numSeeds);

            UInt160 chainHash = UInt160.Parse(hashString);
            sb.EmitPush(chainHash);
            sb.EmitSysCall("Zoro.AppChain.ChangeSeedList");

            SubmitInvocationTransaction(keyPair, sb.ToArray());
            return true;
        }

        private bool OnChangeAppChainValidatorsCommand(string[] args)
        {
            if (plugin.NoWallet()) return true;

            KeyPair keyPair = plugin.Wallet.GetAccounts().FirstOrDefault(p => p.HasKey)?.GetKey();
            if (keyPair == null)
            {
                Console.WriteLine("error, can't get pubkey");
                return true;
            }

            string hashString = ReadString("appchain hash");
            if (hashString.Length != 40)
            {
                Console.WriteLine("cancelled");
                return true;
            }

            int numValidators = ReadInt("validator count");
            if (numValidators < 4)
            {
                Console.WriteLine("cancelled, the input nmber is less then minimum validator count:4.");
                return true;
            }

            string[] validators = new string[numValidators];
            for (int i = 0; i < numValidators; i++)
            {
                validators[i] = ReadString("validator pubkey " + (i + 1).ToString());
            }

            ScriptBuilder sb = new ScriptBuilder();
            for (int i = 0; i < numValidators; i++)
            {
                sb.EmitPush(validators[i]);
            }
            sb.EmitPush(numValidators);

            UInt160 chainHash = UInt160.Parse(hashString);
            sb.EmitPush(chainHash);
            sb.EmitSysCall("Zoro.AppChain.ChangeValidators");

            SubmitInvocationTransaction(keyPair, sb.ToArray());
            return true;
        }

        private RelayResultReason SubmitInvocationTransaction(KeyPair keyPair, byte[] script)
        {
            InvocationTransaction tx = new InvocationTransaction
            {
                ChainHash = UInt160.Zero,
                Version = 1,
                Script = script,
                Gas = Fixed8.Zero,
            };
            tx.Gas -= Fixed8.FromDecimal(10);
            if (tx.Gas < Fixed8.Zero) tx.Gas = Fixed8.Zero;
            tx.Gas = tx.Gas.Ceiling();

            tx.Inputs = new CoinReference[0];
            tx.Outputs = new TransactionOutput[0];

            tx.Attributes = new TransactionAttribute[1];
            tx.Attributes[0] = new TransactionAttribute();
            tx.Attributes[0].Usage = TransactionAttributeUsage.Script;
            tx.Attributes[0].Data = Contract.CreateSignatureRedeemScript(keyPair.PublicKey).ToScriptHash().ToArray();

            ContractParametersContext context = new ContractParametersContext(tx, Blockchain.Root);
            plugin.Wallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();

                RelayResultReason reason = ZoroSystem.Root.Blockchain.Ask<RelayResultReason>(tx).Result;

                if (reason != RelayResultReason.Succeed)
                {
                    Console.WriteLine($"Local Node could not relay transaction: {GetRelayResult(reason)}");
                }
                else
                {
                    Console.WriteLine($"Transaction has been accepted.");
                }
                return reason;
            }

            return RelayResultReason.UnableToVerify;
        }

        private bool OnStartAppChainCommand(string[] args)
        {
            string hashString = ReadString("appchain hash");
            ushort port = (ushort)ReadInt("port");
            ushort wsport = (ushort)ReadInt("websocket port");
            bool startConsensus = ReadInt("start consensus") == 1;
            bool saveJson = ReadInt("save to json file") == 1;

            bool succeed = ZoroSystem.Root.StartAppChain(hashString, port, wsport);

            if (succeed)
            {
                Console.WriteLine($"starting appchain, hash={hashString}");
            }
            else
            {
                Console.WriteLine($"failed to start appchain, hash={hashString}");
            }

            if (startConsensus && plugin.Wallet != null)
            {
                ZoroSystem.Root.StartAppChainConsensus(hashString, plugin.Wallet);

                Console.WriteLine($"starting consensus service, hash={hashString}");
            }

            if (succeed && saveJson)
            {
                UInt160 chainHash = UInt160.Parse(hashString);
                if (AppChainsSettings.Default.AddSettings(chainHash, port, wsport, startConsensus))
                {
                    AppChainsSettings.Default.SaveJsonFile();

                    Console.WriteLine($"save to json file, hash={hashString}");
                }
                else
                {
                    Console.WriteLine($"already exists in json file, hash={hashString}");
                }
            }

            return true;
        }

        private bool OnStopAppChainCommand(string[] args)
        {
            string hashString = ReadString("appchain hash");

            UInt160 chainHash = UInt160.Parse(hashString);

            bool succeed = ZoroSystem.StopAppChainSystem(chainHash);

            if (succeed)
            {
                Console.WriteLine($"stopping appchain, hash={hashString}");
            }
            else
            {
                Console.WriteLine($"failed to stop appchain, hash={hashString}");
            }

            return true;
        }
    }
}
