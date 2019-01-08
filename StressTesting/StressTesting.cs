using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Zoro.Wallets;
using Zoro.Ledger;
using Neo.VM;

namespace Zoro.Plugins
{
    public class StressTesting : Plugin
    {
        private UInt160 scriptHash;
        private KeyPair keypair;
        private UInt160 targetAddress;
        private UInt160 nep5ContractHash;
        private UInt160 nativeNEP5AssetId;
        private string transferValue;
        private int transType = 0;
        private int cocurrentNum = 0;
        private int transNum = 0;
        private int waitingNum = 0;
        private int step = 0;
        private int error = 0;

        private Fixed8 GasPrice = Fixed8.One;
        private Dictionary<string, Fixed8> GasLimit = new Dictionary<string, Fixed8>();

        private CancellationTokenSource cancelTokenSource;

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public StressTesting(PluginManager pluginMgr)
            : base(pluginMgr)
        {
            GasLimit["NEP5Transfer"] = Fixed8.FromDecimal((decimal)4.5);
            GasLimit["NativeNEP5Transfer"] = Fixed8.FromDecimal(1);
            GasLimit["BCPTransfer"] = Fixed8.FromDecimal(1);
        }

        public override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args[0] != "stress") return false;
            if (args[1] != "testing") return false;
            return OnStressTestingCommand(args);
        }

        private bool OnStressTestingCommand(string[] args)
        {
            Console.Write("Transaction Type, 0 - NEP5 SmartContract, 1 - NativeNEP5, 2 - BCP:");
            var param1 = Console.ReadLine();
            Console.Write("Concurrency number:");
            var param2 = Console.ReadLine();
            Console.Write("Totoal transaction count:");
            var param3 = Console.ReadLine();
            Console.Write("Transfer value:");
            var param4 = Console.ReadLine();
            Console.Write("Automatic concurrency adjustment, 0 - no, 1 - yes:");
            var param5 = Console.ReadLine();

            transType = int.Parse(param1);
            transNum = int.Parse(param3);
            cocurrentNum = int.Parse(param2);
            transferValue = param4;
            step = int.Parse(param5) == 1 ? Math.Max(cocurrentNum / 5, 10) : 0;
        
            string chainHash = Settings.Default.TargetChainHash;
            string WIF = Settings.Default.WIF;
            string targetWIF = Settings.Default.TargetWIF;

            keypair = ZoroHelper.GetKeyPairFromWIF(WIF);
            scriptHash = ZoroHelper.GetPublicKeyHash(keypair.PublicKey);
            targetAddress = ZoroHelper.GetPublicKeyHashFromWIF(targetWIF);

            string contractHash = Settings.Default.NEP5Hash;
            nep5ContractHash = UInt160.Parse(contractHash);

            string nativeNEP5Hash = Settings.Default.NativeNEP5Hash;
            nativeNEP5AssetId = UInt160.Parse(nativeNEP5Hash);

            if (transType == 0 || transType == 1 || transType == 2)
            {
                Console.WriteLine($"From:{WIF}");
                Console.WriteLine($"To:{targetWIF}");
                Console.WriteLine($"Count:{transNum}");
                Console.WriteLine($"Value:{transferValue}");
            }

            cancelTokenSource = new CancellationTokenSource();

            Task.Run(() => RunTask(chainHash));

            Console.WriteLine("Input [enter] to stop:");
            var input = Console.ReadLine();
            cancelTokenSource.Cancel();

            return true;
        }

        protected async void CallTransfer(string chainHash)
        {
            Interlocked.Increment(ref waitingNum);

            if (transType == 0)
            {
                await NEP5Transfer(chainHash);
            }
            else if (transType == 1)
            {
                await NativeNEP5Transfer(chainHash);
            }
            else if (transType == 2)
            {
                await BCPTransfer(chainHash);
            }

            Interlocked.Decrement(ref waitingNum);
        }

        public void RunTask(string chainHash)
        {
            TimeSpan oneSecond = TimeSpan.FromSeconds(1);

            int idx = 0;
            int total = 0;

            int cc = step > 0 ? Math.Min(cocurrentNum, step) : cocurrentNum;

            int lastWaiting = 0;
            int pendingNum = 0;

            waitingNum = 0;
            error = 0;

            while (true)
            {
                if (cancelTokenSource.IsCancellationRequested)
                {
                    Console.WriteLine("Stress testing stopped.");
                    break;
                }

                if (transNum > 0)
                {
                    if (total >= transNum && pendingNum == 0 && waitingNum == 0)
                    {
                        Console.WriteLine($"round:{++idx}, total:{total}, tx:0, pending:{pendingNum}, waiting:{waitingNum}, error:{error}");
                        break;
                    }

                    cc = Math.Min(transNum - total, cc);
                }

                Console.WriteLine($"round:{++idx}, total:{total}, tx:{cc}, pending:{pendingNum}, waiting:{waitingNum}, error:{error}");

                lastWaiting = waitingNum;

                if (cc > 0)
                {
                    Interlocked.Add(ref pendingNum, cc);
                    Interlocked.Add(ref total, cc);
                }

                DateTime dt = DateTime.Now;

                for (int i = 0; i < cc; i++)
                {
                    int j = i;
                    Task.Run(() =>
                    {
                        Interlocked.Decrement(ref pendingNum);

                        CallTransfer(chainHash);
                    });
                }

                TimeSpan span = DateTime.Now - dt;

                if (span < oneSecond)
                {
                    Thread.Sleep(oneSecond - span);
                }

                if (step > 0)
                {
                    if (pendingNum > cocurrentNum)
                    {
                        cc = Math.Max(cc - step, 0);
                    }
                    else if (pendingNum < cocurrentNum)
                    {
                        cc = Math.Min(cc + step, cocurrentNum);
                    }
                }
            }
        }

        protected async Task NativeNEP5Transfer(string chainHash)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall("Zoro.NativeNEP5.Call", "Transfer", nativeNEP5AssetId, scriptHash, targetAddress, BigInteger.Parse(transferValue));

                RelayResultReason result = await ZoroHelper.SendInvocationTransaction(sb.ToArray(), keypair, chainHash, GasLimit["NativeNEP5Transfer"], GasPrice);

                OnRelayResult(result);
            }
        }

        protected async Task NEP5Transfer(string chainHash)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitAppCall(nep5ContractHash, "transfer", scriptHash, targetAddress, BigInteger.Parse(transferValue));

                RelayResultReason result = await ZoroHelper.SendInvocationTransaction(sb.ToArray(), keypair, chainHash, GasLimit["NEP5Transfer"], GasPrice);

                OnRelayResult(result);
            }
        }

        protected async Task BCPTransfer(string chainHash)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall("Zoro.NativeNEP5.Call", "Transfer", Genesis.BcpContractAddress, scriptHash, targetAddress, BigInteger.Parse(transferValue));

                RelayResultReason result = await ZoroHelper.SendInvocationTransaction(sb.ToArray(), keypair, chainHash, GasLimit["BCPTransfer"], GasPrice);

                OnRelayResult(result);
            }
        }

        private void OnRelayResult(RelayResultReason reason)
        {
            if (reason != RelayResultReason.Succeed)
            {
                Interlocked.Increment(ref error);

                switch (reason)
                {
                    case RelayResultReason.AlreadyExists:
                        Console.WriteLine("Block or transaction already exists and cannot be sent repeatedly.");
                        break;
                    case RelayResultReason.OutOfMemory:
                        Console.WriteLine("The memory pool is full and no more transactions can be sent.");
                        break;
                    case RelayResultReason.UnableToVerify:
                        Console.WriteLine("The block cannot be validated.");
                        break;
                    case RelayResultReason.Invalid:
                        Console.WriteLine("Block or transaction validation failed.");
                        break;
                    default:
                        Console.WriteLine("Unknown error.");
                        break;
                }
            }
        }
    }
}
