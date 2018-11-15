﻿using Akka.Actor;
using Zoro.IO;
using Zoro.Ledger;
using Zoro.Network.P2P.Payloads;
using Zoro.Persistence;
using Zoro.AppChain;
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
        private Blockchain blockchain;

        public ImportBlocks(PluginManager pluginMgr)
            : base(pluginMgr)
        {
            blockchain = Blockchain.Root;

            Task.Run(() =>
            {
                const string path_acc = "chain.acc";
                if (File.Exists(path_acc))
                    using (FileStream fs = new FileStream(path_acc, FileMode.Open, FileAccess.Read, FileShare.Read))
                        pluginMgr.System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                        {
                            Blocks = GetBlocks(fs)
                        }).Wait();
                const string path_acc_zip = path_acc + ".zip";
                if (File.Exists(path_acc_zip))
                    using (FileStream fs = new FileStream(path_acc_zip, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                    using (Stream zs = zip.GetEntry(path_acc).Open())
                        pluginMgr.System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                        {
                            Blocks = GetBlocks(zs)
                        }).Wait();
                var paths = Directory.EnumerateFiles(".", "chain.*.acc", SearchOption.TopDirectoryOnly).Concat(Directory.EnumerateFiles(".", "chain.*.acc.zip", SearchOption.TopDirectoryOnly)).Select(p => new
                {
                    FileName = Path.GetFileName(p),
                    Start = uint.Parse(Regex.Match(p, @"\d+").Value),
                    IsCompressed = p.EndsWith(".zip")
                }).OrderBy(p => p.Start);
                foreach (var path in paths)
                {
                    if (path.Start > blockchain.Height + 1) break;
                    if (path.IsCompressed)
                        using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                        using (Stream zs = zip.GetEntry(Path.GetFileNameWithoutExtension(path.FileName)).Open())
                            pluginMgr.System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                            {
                                Blocks = GetBlocks(zs, true)
                            }).Wait();
                    else
                        using (FileStream fs = new FileStream(path.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                            pluginMgr.System.Blockchain.Ask<Blockchain.ImportCompleted>(new Blockchain.Import
                            {
                                Blocks = GetBlocks(fs, true)
                            }).Wait();
                }
            });
        }

        private IEnumerable<Block> GetBlocks(Stream stream, bool read_start = false)
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

        private bool CheckMaxOnImportHeight(uint currentImportBlockHeight)
        {
            if (Settings.Default.MaxOnImportHeight == 0 || Settings.Default.MaxOnImportHeight >= currentImportBlockHeight)
                return true;
            return false;
        }

        public override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args.Length < 3) return false;
            if (args[0] != "export") return false;
            if (args[1] != "block" && args[1] != "blocks") return false;

            // 用输入的第三个参数，获取Blockchain对象
            Blockchain blockchain = AppChainManager.Singleton.GetBlockchain(args[2]);

            if (args.Length >= 4 && uint.TryParse(args[3], out uint start))
            {
                if (start > blockchain.Height)
                    return true;
                uint count = args.Length >= 4 ? uint.Parse(args[3]) : uint.MaxValue;
                count = Math.Min(count, blockchain.Height - start + 1);
                uint end = start + count - 1;
                string path = $"chain.{start}.acc";
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
                string path = args.Length >= 3 ? args[2] : "chain.acc";
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
    }
}
