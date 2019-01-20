using System;
using System.IO;
using System.Text;
using System.Numerics;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;
using Zoro.Wallets;
using Zoro.Ledger;
using Zoro.TxnPool;
using Zoro.IO;
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
        private int transactionType = 0;
        private int concurrencyCount = 0;
        private int transferCount = 0;
        private int waitingNum = 0;
        private int error = 0;
        private bool randomTargetAddress = false;
        private bool randomGasPrice = false;
        private UInt160[] targetAddressList;

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
            if (Settings.Default.EnableManualParam == 1)
            {
                Console.Write("Transaction Type, 0 - NEP5 SmartContract, 1 - NativeNEP5, 2 - BCP:");
                var param1 = Console.ReadLine();
                Console.Write("Concurrency count:");
                var param2 = Console.ReadLine();
                Console.Write("Totoal transaction count:");
                var param3 = Console.ReadLine();
                Console.Write("Transfer value:");
                var param4 = Console.ReadLine();
                Console.Write("Random target address, 0 - no, 1 - yes:");
                var param5 = Console.ReadLine();
                Console.Write("Random gas price, 0 - no, 1 - yes:");
                var param6 = Console.ReadLine();

                transactionType = int.Parse(param1);
                transferCount = int.Parse(param3);
                concurrencyCount = int.Parse(param2);
                transferValue = param4;
                randomTargetAddress = int.Parse(param5) == 1;
                randomGasPrice = int.Parse(param6) == 1;
            }
            else
            {
                transactionType = Settings.Default.TransactionType;
                transferCount = Settings.Default.TransferCount;
                concurrencyCount = Settings.Default.ConcurrencyCount;
                transferValue = Settings.Default.TransferValue.ToString();
                randomTargetAddress = Settings.Default.RandomTargetAddress == 1;
                randomGasPrice = Settings.Default.RandomGasPrice == 1;
            }
        
            string chainHash = Settings.Default.TargetChainHash;
            string WIF = Settings.Default.WIF;
            string targetWIF = Settings.Default.TargetWIF;

            keypair = ZoroHelper.GetKeyPairFromWIF(WIF);
            scriptHash = ZoroHelper.GetPublicKeyHash(keypair.PublicKey);
            targetAddress = ZoroHelper.GetPublicKeyHashFromWIF(targetWIF);

            nep5ContractHash = UInt160.Parse(Settings.Default.NEP5Hash);
            nativeNEP5AssetId = UInt160.Parse(Settings.Default.NativeNEP5Hash);

            TransactionPool txnPool = ZoroChainSystem.Singleton.GetTransactionPool(chainHash);
            if (txnPool == null)
                return true;

            if (randomTargetAddress)
            {
                PluginManager.EnableLog(false);

                InitializeRandomTargetAddressList(transferCount);

                PluginManager.EnableLog(true);
            }

            if (transactionType == 0 || transactionType == 1 || transactionType == 2)
            {
                Console.WriteLine($"From:{WIF}");
                Console.WriteLine($"To:{targetWIF}");
                Console.WriteLine($"Count:{transferCount}");
                Console.WriteLine($"Value:{transferValue}");
            }

            cancelTokenSource = new CancellationTokenSource();

            Task.Run(() => RunTask(chainHash, txnPool));

            Console.WriteLine("Input [enter] to stop:");
            var input = Console.ReadLine();
            cancelTokenSource.Cancel();

            return true;
        }

        protected void InitializeRandomTargetAddressList(int count)
        {
            int maximum = 50000;
            count = Math.Min(maximum, count);

            string filename = "targetaddress.dat";
            if (!LoadTargetAddress(filename, count))
            {
                GenerateRandomTargetAddressList(filename, count);
            }
        }

        protected bool LoadTargetAddress(string filename, int count)
        {
            if (!File.Exists(filename))
                return false;

            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (BinaryReader reader = new BinaryReader(fs, Encoding.ASCII, true))
                {
                    int num = reader.ReadInt32();
                    if (num < count)
                        return false;

                    targetAddressList = new UInt160[num];
                    for (int i = 0; i < num; i++)
                    {
                        targetAddressList[i] = reader.ReadSerializable<UInt160>();
                    }

                    return true;
                }
            }
        }

        protected void GenerateRandomTargetAddressList(string filename, int count)
        {
            Console.WriteLine($"Generating random target address list:{count}");

            DateTime time = DateTime.UtcNow;

            targetAddressList = new UInt160[count];
            for (int i = 0; i < count; i++)
            {
                targetAddressList[i] = GenerateRandomTargetAddress();
                if (i % 100 == 0)
                {
                    Console.Write(".");
                }
            }

            TimeSpan interval = DateTime.UtcNow - time;

            Console.WriteLine($"Target address list completed, time:{interval:hh\\:mm\\:ss\\.ff}");

            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (BinaryWriter writer = new BinaryWriter(fs, Encoding.ASCII, true))
                {
                    writer.Write(count);
                    for (int i = 0; i < count; i++)
                    {
                        writer.Write(targetAddressList[i]);
                    }
                }
            }            
        }

        protected UInt160 GenerateRandomTargetAddress()
        {
            byte[] privateKey = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(privateKey);
            }

            KeyPair key = new KeyPair(privateKey);
            return key.PublicKeyHash;
        }

        protected UInt160 GetRandomTargetAddress(Random rnd)
        {
            int index = rnd.Next(0, targetAddressList.Length);
            return targetAddressList[index];
        }

        protected async void CallTransfer(string chainHash, UInt160 targetAddress, Fixed8 gasPrice)
        {
            Interlocked.Increment(ref waitingNum);

            if (transactionType == 0)
            {
                await NEP5Transfer(chainHash, targetAddress, gasPrice);
            }
            else if (transactionType == 1)
            {
                await NativeNEP5Transfer(chainHash, targetAddress, gasPrice);
            }
            else if (transactionType == 2)
            {
                await BCPTransfer(chainHash, targetAddress, gasPrice);
            }

            Interlocked.Decrement(ref waitingNum);
        }

        public void RunTask(string chainHash, TransactionPool txnPool)
        {
            Random rnd = new Random();
            TimeSpan oneSecond = TimeSpan.FromSeconds(1);
            Fixed8 gasPrice = Fixed8.One;

            int idx = 0;
            int total = 0;

            int cc = concurrencyCount;

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

                if (transferCount > 0)
                {
                    if (total >= transferCount && pendingNum == 0 && waitingNum == 0)
                    {
                        Console.WriteLine($"round:{++idx}, total:{total}, tx:0, pending:{pendingNum}, waiting:{waitingNum}, error:{error}");
                        break;
                    }

                    cc = Math.Min(transferCount - total, concurrencyCount);

                    int mempool_count = txnPool.GetMemoryPoolCount();
                    if (mempool_count + cc >= 50000)
                        cc = 0;
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
                    Task.Run(() =>
                    {
                        Interlocked.Decrement(ref pendingNum);

                        Fixed8 price = gasPrice;

                        if (randomGasPrice)
                            Fixed8.TryParse((rnd.Next(1, 1000) * 0.0001).ToString(), out price);

                        CallTransfer(chainHash, randomTargetAddress ? GetRandomTargetAddress(rnd) : targetAddress, price);
                    });
                }

                TimeSpan span = DateTime.Now - dt;

                if (span < oneSecond)
                {
                    Thread.Sleep(oneSecond - span);
                }
            }
        }

        protected async Task NativeNEP5Transfer(string chainHash, UInt160 targetAddress, Fixed8 gasPrice)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall("Zoro.NativeNEP5.Call", "Transfer", nativeNEP5AssetId, scriptHash, targetAddress, BigInteger.Parse(transferValue));

                RelayResultReason result = await ZoroHelper.SendInvocationTransaction(sb.ToArray(), keypair, chainHash, GasLimit["NativeNEP5Transfer"], gasPrice);

                ParseResult(result);
            }
        }

        protected async Task NEP5Transfer(string chainHash, UInt160 targetAddress, Fixed8 gasPrice)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitAppCall(nep5ContractHash, "transfer", scriptHash, targetAddress, BigInteger.Parse(transferValue));

                RelayResultReason result = await ZoroHelper.SendInvocationTransaction(sb.ToArray(), keypair, chainHash, GasLimit["NEP5Transfer"], gasPrice);

                ParseResult(result);
            }
        }

        protected async Task BCPTransfer(string chainHash, UInt160 targetAddress, Fixed8 gasPrice)
        {
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall("Zoro.NativeNEP5.Call", "Transfer", Genesis.BcpContractAddress, scriptHash, targetAddress, BigInteger.Parse(transferValue));

                RelayResultReason result = await ZoroHelper.SendInvocationTransaction(sb.ToArray(), keypair, chainHash, GasLimit["BCPTransfer"], gasPrice);

                ParseResult(result);
            }
        }

        private void ParseResult(RelayResultReason reason)
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
