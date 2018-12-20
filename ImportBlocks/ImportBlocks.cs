using Akka.Actor;
using Zoro.IO;
using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using Zoro.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Zoro.Plugins
{
    public class ImportBlocks : Plugin
    {
        public ImportBlocks(PluginManager pluginMgr)
            : base(pluginMgr)
        {
        }

        private static bool CheckMaxOnImportHeight(uint currentImportBlockHeight)
        {
            if (Settings.Default.MaxOnImportHeight == 0 || Settings.Default.MaxOnImportHeight >= currentImportBlockHeight)
                return true;
            return false;
        }
        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        private string GetChainHash(string filename)
        {
            string prefix = "chain_";
            int start = prefix.Length;
            int npos = filename.IndexOf('.');

            string hashString = filename.Substring(start, npos - start);
            return hashString;
        }

        private IEnumerable<Block> GetBlocks(Blockchain blockchain, Stream stream, bool read_start = false)
        {
            using (BinaryReader r = new BinaryReader(stream))
            {
                uint start = read_start ? r.ReadUInt32() : 0;
                uint count = r.ReadUInt32();
                uint end = start + count - 1;
                if (end <= blockchain.Height) yield break;
                for (uint height = start; height <= end; height++)
                {
                    byte[] array = r.ReadBytes(r.ReadInt32());
                    if (!CheckMaxOnImportHeight(height)) yield break;
                    if (height > blockchain.Height)
                    {
                        Block block = array.AsSerializable<Block>();
                        yield return block;
                    }
                }
            }
        }

        private bool OnExport(string[] args)
        {
            if (args.Length < 3) return false;
            if (!string.Equals(args[1], "block", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(args[1], "blocks", StringComparison.OrdinalIgnoreCase))
                return false;

            // 用输入的第三个参数，获取Blockchain对象
            Blockchain blockchain = ZoroChainSystem.Singleton.GetBlockchain(args[2]);
            if (blockchain == null)
                return false;

            if (args.Length >= 4 && uint.TryParse(args[3], out uint start))
            {
                if (start > blockchain.Height)
                    return true;
                uint count = args.Length >= 4 ? uint.Parse(args[3]) : uint.MaxValue;
                count = Math.Min(count, blockchain.Height - start + 1);
                uint end = start + count - 1;
                string path = $"chain_{blockchain.ChainHash.ToString()}.{start}.acc";
                using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    if (fs.Length > 0)
                    {
                        fs.Seek(sizeof(uint), SeekOrigin.Begin);
                        byte[] buffer = new byte[sizeof(uint)];
                        fs.Read(buffer, 0, buffer.Length);
                        start += BitConverter.ToUInt32(buffer, 0);
                        fs.Seek(sizeof(uint), SeekOrigin.Begin);
                    }
                    else
                    {
                        fs.Write(BitConverter.GetBytes(start), 0, sizeof(uint));
                    }
                    if (start <= end)
                        fs.Write(BitConverter.GetBytes(count), 0, sizeof(uint));
                    fs.Seek(0, SeekOrigin.End);
                    using (Snapshot snapshot = blockchain.GetSnapshot())
                        for (uint i = start; i <= end; i++)
                        {
                            Block block = snapshot.GetBlock(i);
                            byte[] array = block.ToArray();
                            fs.Write(BitConverter.GetBytes(array.Length), 0, sizeof(int));
                            fs.Write(array, 0, array.Length);
                            Console.SetCursorPosition(0, Console.CursorTop);
                            Console.Write($"[{i}/{end}]");
                        }
                }
            }
            else
            {
                start = 0;
                uint end = blockchain.Height;
                uint count = end - start + 1;
                string path = args.Length >= 4 ? args[3] : $"chain_{blockchain.ChainHash.ToString()}.acc";
                using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    if (fs.Length > 0)
                    {
                        byte[] buffer = new byte[sizeof(uint)];
                        fs.Read(buffer, 0, buffer.Length);
                        start = BitConverter.ToUInt32(buffer, 0);
                        fs.Seek(0, SeekOrigin.Begin);
                    }
                    if (start <= end)
                        fs.Write(BitConverter.GetBytes(count), 0, sizeof(uint));
                    fs.Seek(0, SeekOrigin.End);
                    using (Snapshot snapshot = blockchain.GetSnapshot())
                        for (uint i = start; i <= end; i++)
                        {
                            Block block = snapshot.GetBlock(i);
                            byte[] array = block.ToArray();
                            fs.Write(BitConverter.GetBytes(array.Length), 0, sizeof(int));
                            fs.Write(array, 0, array.Length);
                            Console.SetCursorPosition(0, Console.CursorTop);
                            Console.Write($"[{i}/{end}]");
                        }
                }
            }
            Console.WriteLine();
            return true;
        }

        private bool OnHelp(string[] args)
        {
            if (args.Length < 2) return false;
            if (!string.Equals(args[1], Name, StringComparison.OrdinalIgnoreCase))
                return false;
            Console.Write($"{Name} Commands:\n" + "\texport block[s] <chainhash> <index>\n");
            return true;
        }

        public override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args.Length == 0) return false;
            switch (args[0].ToLower())
            {
                case "help":
                    return OnHelp(args);
                case "export":
                    return OnExport(args);
                case "import":
                    return OnImport(args);
            }
            return false;
        }

        private bool OnImport(string[] args)
        {
            if (args.Length < 3) return false;
            if (!string.Equals(args[1], "block", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(args[1], "blocks", StringComparison.OrdinalIgnoreCase))
                return false;

            string filename = args[2];

            Task.Run(() =>
            {
                var paths = Directory.EnumerateFiles(".", filename, SearchOption.TopDirectoryOnly).Select(p => new
                {
                    FileName = Path.GetFileName(p),
                    Start = uint.Parse(Regex.Match(p, @"\d+").Value),
                    IsCompressed = p.EndsWith(".zip")
                }).OrderBy(p => p.Start);
                foreach (var path in paths)
                {
                    Blockchain blockchain = ZoroChainSystem.Singleton.GetBlockchain(GetChainHash(path.FileName));
                    if (blockchain == null)
                        continue;

                    if (path.Start > blockchain.Height + 1) break;
                    if (path.IsCompressed)
                    {
                        using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                        using (Stream zs = zip.GetEntry(Path.GetFileNameWithoutExtension(path.FileName)).Open())
                        {
                            ZoroSystem system = ZoroChainSystem.Singleton.GetZoroSystem(GetChainHash(path.FileName));
                            if (system != null)
                            {
                                system.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                                {
                                    Blocks = GetBlocks(blockchain, zs, true)
                                }).Wait();
                            }

                        }
                    }
                    else
                    {
                        using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            ZoroSystem system = ZoroChainSystem.Singleton.GetZoroSystem(GetChainHash(path.FileName));
                            if (system != null)
                            {
                                system.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                                {
                                    Blocks = GetBlocks(blockchain, fs, true)
                                }).Wait();
                            }
                        }
                    }
                }
            });

            return true;
        }
    }
}
