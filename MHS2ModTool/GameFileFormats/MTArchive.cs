using MHS2ModTool.Cryptography;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace MHS2ModTool.GameFileFormats
{
    internal class MTArchive
    {
        private const string Key = "QZHaM;-5:)dV#";
        public const string OrderLogFileName = "orderlog.txt";

        private const uint ArcMagic = 0x435241;
        private const uint ArccMagic = 0x43435241;
        private const ushort Version = 7;

        private const uint IsCompressedFlag = 1u << 30;

        private const int BlockAlignment = 0x8000;

        private struct MTArchiveHeader
        {
            public uint Magic;
            public ushort Version;
            public ushort FilesCount;
        }

        private struct MTArchiveEntry
        {
            public MTName Path;
            public uint ExtensionHash;
            public uint CompressedLength;
            public uint DecompressedLength;
            public uint Offset;
        }

        private readonly record struct FileEntry(byte[] Data, string Path);
        private readonly record struct FileEntryWithExtHash(byte[] Data, string Path, uint ExtensionHash);

        private readonly List<FileEntry> _entries = new();

        public static bool IsValidArchiveDirectory(string inputFolder)
        {
            return File.Exists(Path.Combine(inputFolder, OrderLogFileName));
        }

        public static MTArchive Load(string fileName)
        {
            using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);

            return Load(fs);
        }

        public static MTArchive Load(Stream stream)
        {
            var output = new MTArchive();

            long start = stream.Position;

            using var reader = new BinaryReader(stream);

            var header = reader.Read<MTArchiveHeader>();
            var crypto = header.Magic == ArccMagic ? new Blowfish(Key) : null;

            for (int i = 0; i < header.FilesCount; i++)
            {
                stream.Seek(start + Unsafe.SizeOf<MTArchiveHeader>() + i * Unsafe.SizeOf<MTArchiveEntry>(), SeekOrigin.Begin);

                var entry = reader.Read<MTArchiveEntry>(crypto);

                if (!MTArchiveFormatTable.TryGetExtensionFromHash(entry.ExtensionHash, out string? ext) || ext == null)
                {
                    continue;
                }

                stream.Seek(start + entry.Offset, SeekOrigin.Begin);

                byte[] data = reader.ReadBytes((int)entry.CompressedLength, crypto);

                if ((entry.DecompressedLength & IsCompressedFlag) != 0)
                {
                    data = ZlibDecompress(data, entry.DecompressedLength & ~IsCompressedFlag);
                }

                output._entries.Add(new(data, entry.Path + ext));
            }

            return output;
        }

        public void Save(string fileName, bool encrypt)
        {
            using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);

            Save(fs, encrypt);
        }

        public void Save(Stream stream, bool encrypt)
        {
            long start = stream.Position;

            using var writer = new BinaryWriter(stream);

            var validFiles = GetValidFiles();
            int filesCount = validFiles.Count();

            writer.Write(new MTArchiveHeader()
            {
                Magic = encrypt ? ArccMagic : ArcMagic,
                Version = Version,
                FilesCount = (ushort)filesCount
            });

            Blowfish? crypto = encrypt ? new Blowfish(Key) : null;

            long dataOffset = Unsafe.SizeOf<MTArchiveHeader>() + Unsafe.SizeOf<MTArchiveEntry>() * filesCount;
            dataOffset = (dataOffset + (BlockAlignment - 1)) & ~(BlockAlignment - 1);

            int i = 0;

            foreach (var file in validFiles)
            {
                stream.Seek(start + Unsafe.SizeOf<MTArchiveHeader>() + i * Unsafe.SizeOf<MTArchiveEntry>(), SeekOrigin.Begin);

                byte[] compressedData = ZlibCompress(file.Data);

                uint compressedLength = (uint)compressedData.Length;

                if (crypto != null)
                {
                    // Compressed length must be 8 bytes aligned when using blowfish crypto.
                    compressedLength = (compressedLength + 7) & ~7u;
                }

                writer.Write(new MTArchiveEntry()
                {
                    Path = new(file.Path),
                    ExtensionHash = file.ExtensionHash,
                    CompressedLength = compressedLength,
                    DecompressedLength = (uint)file.Data.Length | IsCompressedFlag,
                    Offset = (uint)dataOffset
                }, crypto);

                stream.Seek(start + dataOffset, SeekOrigin.Begin);

                writer.Write(compressedData, crypto);

                dataOffset = stream.Position - start;

                i++;
            }
        }

        private IEnumerable<FileEntryWithExtHash> GetValidFiles()
        {
            foreach (var file in _entries)
            {
                string ext = Path.GetExtension(file.Path).ToLowerInvariant();

                if (MTArchiveFormatTable.TryGetHashFromExtension(ext, out uint extHash))
                {
                    yield return new(file.Data, file.Path[..^ext.Length], extHash);
                }
            }
        }

        public static MTArchive CreateFromFolder(string inputFolder)
        {
            var output = new MTArchive();

            string orderLogFileName = Path.Combine(inputFolder, OrderLogFileName);

            if (File.Exists(orderLogFileName))
            {
                string[] orderLog = File.ReadAllLines(orderLogFileName);

                foreach (var file in orderLog)
                {
                    string fileName = Path.Combine(inputFolder, file);

                    if (File.Exists(fileName))
                    {
                        string path = file;

                        if (Path.PathSeparator != '\\')
                        {
                            path = path.Replace(Path.PathSeparator, '\\');
                        }

                        if (path.StartsWith('\\'))
                        {
                            path = path[1..];
                        }

                        output._entries.Add(new(File.ReadAllBytes(fileName), path));
                    }
                }
            }

            return output;
        }

        public void Extract(string outputFolder)
        {
            var orderLog = new List<string>();

            foreach (var file in _entries)
            {
                string outputPath = Path.Combine(outputFolder, file.Path);
                string? parentDir = Path.GetDirectoryName(outputPath);
                if (parentDir != null)
                {
                    Directory.CreateDirectory(parentDir);
                }

                File.WriteAllBytes(outputPath, file.Data);

                orderLog.Add(file.Path);
            }

            File.WriteAllLines(Path.Combine(outputFolder, OrderLogFileName), orderLog);
        }

        public IEnumerable<string> GetRelativePaths()
        {
            return _entries.Select(e => e.Path);
        }

        private static byte[] ZlibCompress(ReadOnlySpan<byte> data)
        {
            using var ms = new MemoryStream();
            using (var stream = new ZLibStream(ms, CompressionLevel.Optimal))
            {
                stream.Write(data);
            }

            return ms.ToArray();
        }

        private static byte[] ZlibDecompress(byte[] data, uint decompressedLength)
        {
            using var ms = new MemoryStream(data);
            using var stream = new ZLibStream(ms, CompressionMode.Decompress);

            byte[] output = new byte[decompressedLength];

            stream.ReadExactly(output);

            return output;
        }
    }
}
